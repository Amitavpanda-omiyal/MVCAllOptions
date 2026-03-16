---
name: abp-ai-management
description: "ABP AI Management module (Pro). Use when: installing the AI Management module; configuring AI workspaces (system or dynamic); adding AI providers (OpenAI, Azure OpenAI, Ollama, custom); using IChatClient<TWorkspace> in application services; embedding chat widgets in MVC/Razor Pages; creating custom IChatClientFactory implementations; seeding workspaces via IWorkspaceRepository; using IWorkspaceConfigurationStore for Semantic Kernel or custom clients; assigning workspace permissions; setting up remote/microservice AI client scenarios; integrating MAF workflows with workspace-backed chat clients; or debugging workspace resolution and caching issues. Triggers: 'AI module', 'workspace', 'IChatClient', 'AIManagement', 'add AI', 'chat widget', 'custom provider', 'OpenAI workspace', 'Ollama workspace'."
argument-hint: "Describe what you want to build: install module, add workspace, embed chat widget, custom provider, Semantic Kernel integration, or remote client scenario."
---

# ABP AI Management Module — Working Guide

## Purpose

This skill guides you through every aspect of the **ABP AI Management (Pro)** module — from first install to custom provider factories, chat widget embedding, and Semantic Kernel / MAF workflow integration — within the `MVCAllOptions` layered DDD solution.

> **License gate**: Requires an ABP **Team** license or higher.  
> **Docs**: https://abp.io/docs/latest/modules/ai-management  
> **AI Infrastructure base**: https://abp.io/docs/latest/framework/infrastructure/artificial-intelligence

---

## When to Use

- Installing `Volo.AIManagement` into this project
- Adding or configuring AI provider packages (OpenAI, Ollama, Azure, custom)
- Creating system workspaces (code-defined) or dynamic workspaces (UI/data-seeder)
- Injecting `IChatClient<TWorkspace>` into application services / domain services
- Embedding `ChatClientChatViewComponent` (MVC) chat widget on Razor Pages
- Accessing workspace config raw via `IWorkspaceConfigurationStore` (for Semantic Kernel / MAF)
- Implementing custom `IChatClientFactory` for any provider
- Setting up `Volo.AIManagement.Client.*` packages for microservice/remote scenarios
- Adding workspace-level permissions (`RequiredPermissionName`)
- Diagnosing workspace caching or startup resolution issues

---

## Quick Orientation — Where Things Live in This Solution

```
src/
  MVCAllOptions.Domain/                   — Define workspace marker types here (if using typed workspaces)
  MVCAllOptions.Application.Contracts/    — Permission constants for workspace access
  MVCAllOptions.Application/             — Inject IChatClient<T> in AppServices; workspace seeding
  MVCAllOptions.EntityFrameworkCore/      — AIManagement DB context wiring (Volo.AIManagement.EntityFrameworkCore)
  MVCAllOptions.Web/                      — Embed ChatClientChatViewComponent in Razor Pages
  MVCAllOptions.AgentWorkflows/           — Standalone MAF host — uses ChatClientFactory (OpenRouter)
                                            NOT on AIManagement module — uses direct OpenAI SDK
```

> **Note**: `MVCAllOptions.AgentWorkflows` currently uses a custom `ChatClientFactory` pointing to  
> **OpenRouter** via the OpenAI-compatible API. It does NOT use `Volo.AIManagement` packages.  
> When wiring MAF workflows to an AIManagement workspace, use `IWorkspaceConfigurationStore`  
> to pull config dynamically instead of hard-coding in `appsettings.json`.

---

## Step 1 — Install the Module

### 1a. Install via ABP CLI

```bash
# In the solution root
abp add-module Volo.AIManagement

# Then add a provider (pick one or more)
abp add-package Volo.AIManagement.OpenAI   # OpenAI + Azure OpenAI-compatible endpoints
abp add-package Volo.AIManagement.Ollama   # Local Ollama models
```

### 1b. Install via ABP Studio

Right-click the solution → **Import Module** → search `Volo.AIManagement` on the NuGet tab → check **Install this Module** → OK.

### 1c. Run Migration

```bash
cd src/MVCAllOptions.EntityFrameworkCore
dotnet ef migrations add Added_AIManagement
# Then apply via DbMigrator (preferred — also seeds data)
dotnet run --project ../MVCAllOptions.DbMigrator
```

---

## Step 2 — Choose Your Usage Scenario

| Scenario | Use When | Key Package |
|----------|----------|-------------|
| **1. No Module** | Simple app, config in code, no UI management | `Volo.Abp.AI` + provider |
| **2. Full Module (Local)** | Monolith — manage workspaces in DB/UI | `Volo.AIManagement.EntityFrameworkCore` + `Volo.AIManagement.Web` |
| **3. Client (Remote)** | Microservice consuming a central AI service | `Volo.AIManagement.Client.HttpApi.Client` |
| **4. Proxy** | API Gateway — expose AI endpoints to other clients | + `Volo.AIManagement.Client.HttpApi` |

**This project (MVCAllOptions) uses Scenario 2** — full module locally hosted.

---

## Step 3 — Configure Workspaces

### 3a. System Workspaces (Code-Defined, Cannot Be Deleted via UI)

Define in your `AbpModule.ConfigureServices` — always use `PreConfigure`:

```csharp
// In MVCAllOptionsWebModule.cs (or the module that owns the host)
PreConfigure<AbpAIWorkspaceOptions>(options =>
{
    options.Workspaces.Configure<MyAssistantWorkspace>(configuration =>
    {
        configuration.ConfigureChatClient(chatClientConfig =>
        {
            // Hard-coded builder — used for guaranteed availability
            chatClientConfig.Builder = new ChatClientBuilder(
                sp => new OpenAIClient(apiKey).GetChatClient("gpt-4o-mini")
            );
        });
    });
});
```

Set `OverrideSystemConfiguration = true` to allow DB/UI values to override the code-level config.

### 3b. Dynamic Workspaces (UI-Managed or Data-Seeded)

No code config needed. Either use the **AI Management → Workspaces** UI, or seed:

```csharp
public class AIWorkspaceDataSeeder : IDataSeedContributor, ITransientDependency
{
    private readonly IWorkspaceRepository _workspaceRepo;
    private readonly ApplicationWorkspaceManager _workspaceManager;

    public AIWorkspaceDataSeeder(
        IWorkspaceRepository workspaceRepo,
        ApplicationWorkspaceManager workspaceManager)
    {
        _workspaceRepo = workspaceRepo;
        _workspaceManager = workspaceManager;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (await _workspaceRepo.FindByNameAsync("CustomerSupport") != null) return;

        var ws = await _workspaceManager.CreateAsync(
            name: "CustomerSupport",   // no spaces — use camelCase or underscores
            provider: "OpenAI",
            modelName: "gpt-4o-mini");

        ws.ApiKey = "sk-...";
        ws.SystemPrompt = "You are a helpful customer support agent.";
        ws.Temperature = 0.7f;
        ws.RequiredPermissionName = MVCAllOptionsPermissions.AI.CustomerSupportWorkspace;

        await _workspaceRepo.InsertAsync(ws);
    }
}
```

**Workspace naming rules** (strictly enforced):
- Must be **unique**
- **No spaces** — use `camelCase` or `under_score`
- **Case-sensitive**

---

## Step 4 — Using AI in Application Services

### 4a. Typed Workspace Marker Class

Define one empty marker class **per workspace** in `MVCAllOptions.Domain/AI/`:

```csharp
// MVCAllOptions.Domain/AI/CustomerSupportWorkspace.cs
namespace MVCAllOptions.Domain.AI;

public class CustomerSupportWorkspace { }
```

### 4b. Inject `IChatClient<TWorkspace>` in an Application Service

```csharp
// MVCAllOptions.Application/Support/SupportAppService.cs
using Microsoft.Extensions.AI;
using Volo.Abp.AI;

public class SupportAppService : ApplicationService, ISupportAppService
{
    private readonly IChatClient<CustomerSupportWorkspace> _chatClient;

    public SupportAppService(IChatClient<CustomerSupportWorkspace> chatClient)
    {
        _chatClient = chatClient;
    }

    public async Task<string> GetSupportResponseAsync(string userMessage)
    {
        var response = await _chatClient.CompleteAsync(userMessage);
        return response.Message.Text;
    }

    public async IAsyncEnumerable<string> StreamSupportResponseAsync(string userMessage)
    {
        await foreach (var update in _chatClient.CompleteStreamingAsync(userMessage))
        {
            if (update.Text is not null)
                yield return update.Text;
        }
    }
}
```

---

## Step 5 — Embed a Chat Widget (MVC / Razor Pages)

### 5a. Package Required

Make sure `Volo.AIManagement.Client.Web` is installed in `MVCAllOptions.Web`.

### 5b. Basic Chat Widget in a Razor Page

```cshtml
@* Pages/Support/Index.cshtml *@
@using Volo.AIManagement.Client.Web.Widgets

@await Component.InvokeAsync(typeof(ChatClientChatViewComponent), new ChatClientChatViewModel
{
    WorkspaceName    = "CustomerSupport",
    ComponentId      = "support-chat",
    ConversationId   = "support-" + CurrentUser.Id,   // persists in browser storage
    Title            = "Customer Support",
    ShowStreamCheckbox = true,
    UseStreaming     = true
})
```

### 5c. Widget Properties Reference

| Property | Required | Default | Description |
|----------|----------|---------|-------------|
| `WorkspaceName` | ✅ | — | Name of the workspace to use |
| `ComponentId` | No | — | JS API handle — access via `abp.chatComponents.get(id)` |
| `ConversationId` | No | `null` | Persist history in browser storage. `null` = ephemeral |
| `Title` | No | Workspace name | Widget header text |
| `ShowStreamCheckbox` | No | `false` | Let users toggle streaming on/off |
| `UseStreaming` | No | `false` | Default streaming state |

### 5d. JavaScript API

Always get the component inside event handlers — **never at page load time**:

```javascript
// ✅ Correct — get inside handler
$('#clear-btn').on('click', function () {
    var chat = abp.chatComponents.get('support-chat');
    chat.clearConversation();
});

// ✅ Switch conversation
chat.switchConversation(newConversationId);

// ✅ Listen to events
chat.on('messageSent',     function(data) { console.log(data.message); });
chat.on('messageReceived', function(data) { console.log(data.message, data.isStreaming); });
chat.on('streamStarted',   function(data) { /* streaming started */ });

// ❌ Never do this — component may not be initialized yet
var chat = abp.chatComponents.get('support-chat');  // at page load
```

---

## Step 6 — Using Workspace Config with Semantic Kernel / MAF

Use `IWorkspaceConfigurationStore` when you need the raw API key / model / endpoint — e.g., to build a `Kernel` or pass credentials to the MAF `ChatClientFactory`:

```csharp
public class BookEnrichmentService : ApplicationService
{
    private readonly IWorkspaceConfigurationStore _configStore;

    public BookEnrichmentService(IWorkspaceConfigurationStore configStore)
    {
        _configStore = configStore;
    }

    public async Task DoWorkAsync()
    {
        var config = await _configStore.GetAsync("BookEnrichment");

        // Pass to Semantic Kernel
        var kernel = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(
                modelId: config.ModelName!,
                apiKey: config.ApiKey!)
            .Build();

        // — or pass to MAF ChatClientFactory in AgentWorkflows —
        // store config.ApiKey / config.ApiBaseUrl / config.ModelName
        // then call OLlama or OpenAI client with those values
    }
}
```

Cache key format (auto-invalidated on workspace CRUD):
```
WorkspaceConfiguration:{ApplicationName}:{WorkspaceName}
```

---

## Step 7 — Permissions

### 7a. Module-Level Permissions (Built-in)

| Permission | Grants | Default Role |
|-----------|--------|-------------|
| `AIManagement.Workspaces` | View workspaces | Admin |
| `AIManagement.Workspaces.Create` | Create new workspaces | Admin |
| `AIManagement.Workspaces.Update` | Edit workspaces | Admin |
| `AIManagement.Workspaces.Delete` | Delete workspaces | Admin |

### 7b. Workspace-Level Permission (Restrict Access to a Specific Workspace)

```csharp
workspace.RequiredPermissionName = MVCAllOptionsPermissions.AI.PremiumWorkspace;
```

Define the permission in `MVCAllOptions.Application.Contracts/Permissions/MVCAllOptionsPermissions.cs`:

```csharp
public static class AI
{
    public const string Default = GroupName + ".AI";
    public const string PremiumWorkspace = Default + ".PremiumWorkspace";
    public const string CustomerSupportWorkspace = Default + ".CustomerSupportWorkspace";
}
```

Register via `MVCAllOptionsPermissionDefinitionProvider`.

---

## Step 8 — Implement a Custom AI Provider Factory

Use when your provider is not OpenAI or Ollama (e.g., Azure OpenAI, Anthropic, Groq, or OpenRouter).

### 8a. Implement `IChatClientFactory`

```csharp
// MVCAllOptions.Application/AI/OpenRouterChatClientFactory.cs
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using Volo.AIManagement.Factory;
using Volo.Abp.DependencyInjection;

namespace MVCAllOptions.Application.AI;

public class OpenRouterChatClientFactory : IChatClientFactory, ITransientDependency
{
    // Must match the Provider string stored in the workspace DB record
    public string Provider => "OpenRouter";

    public Task<IChatClient> CreateAsync(ChatClientCreationConfiguration config)
    {
        var client = new OpenAIClient(
            new ApiKeyCredential(config.ApiKey
                ?? throw new ArgumentNullException(nameof(config.ApiKey))),
            new OpenAIClientOptions
            {
                Endpoint = new Uri(config.ApiBaseUrl ?? "https://openrouter.ai/api/v1")
            });

        return Task.FromResult<IChatClient>(
            client.GetChatClient(config.ModelName).AsIChatClient());
    }
}
```

### 8b. Register the Factory

```csharp
// In MVCAllOptionsApplicationModule.ConfigureServices
Configure<ChatClientFactoryOptions>(options =>
{
    options.AddFactory<OpenRouterChatClientFactory>("OpenRouter");
});
```

### 8c. Available `ChatClientCreationConfiguration` Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Workspace name |
| `Provider` | `string` | Provider identifier (must match `AddFactory(...)` name) |
| `ApiKey` | `string?` | API key |
| `ModelName` | `string` | Model ID (e.g., `gpt-4o-mini`, `mistral`) |
| `ApiBaseUrl` | `string?` | Custom endpoint URL |
| `SystemPrompt` | `string?` | Default system prompt |
| `Temperature` | `float?` | 0.0–1.0 |
| `IsActive` | `bool` | Whether workspace is enabled |
| `IsSystem` | `bool` | Code-defined workspace flag |
| `RequiredPermissionName` | `string?` | Permission gate |

### 8d. Create a Workspace Using Your Custom Provider

Via data seeding or UI (select "OpenRouter" from the Provider dropdown):

```csharp
var ws = await _workspaceManager.CreateAsync(
    name: "BookEnrichment",
    provider: "OpenRouter",
    modelName: "openai/gpt-4o-mini");

ws.ApiKey = "sk-or-v1-...";
ws.ApiBaseUrl = "https://openrouter.ai/api/v1";
ws.SystemPrompt = "You are a professional book metadata enrichment agent.";
await _workspaceRepo.InsertAsync(ws);
```

> **Tip**: The provider name in `AddFactory<T>("OpenRouter")` must **exactly match** the `Provider` value stored in the workspace record.

---

## Step 9 — Remote / Microservice Scenario (Client Packages)

Configure in `appsettings.json` of the consuming application:

```json
{
  "RemoteServices": {
    "AIManagementClient": {
      "BaseUrl": "https://ai-management-service.yourdomain.com/"
    }
  }
}
```

Then inject `IChatCompletionClientAppService` to call the remote service:

```csharp
private readonly IChatCompletionClientAppService _chatService;

var response = await _chatService.ChatCompletionsAsync("WorkspaceName",
    new ChatClientCompletionRequestDto
    {
        Messages = [new ChatMessageDto { Role = "user", Content = prompt }]
    });
```

For streaming:
```csharp
await foreach (var chunk in _chatService.StreamChatCompletionsAsync("WorkspaceName", request))
{
    yield return chunk.Content;
}
```

---

## Step 10 — RAG with File Upload

> Source: https://abp.io/docs/10.2/modules/ai-management#rag-with-file-upload

The AI Management module supports RAG (Retrieval-Augmented Generation), which enables workspaces to answer questions based on the content of uploaded documents. When RAG is configured, the AI model searches the uploaded documents for relevant information before generating a response.

### Prerequisites

RAG requires **both** an embedder and a vector store configured on the workspace:

- **Embedder**: Converts documents and queries into vector embeddings. Any provider supporting embedding generation (e.g., OpenAI `text-embedding-3-small`, Ollama `nomic-embed-text`).
- **Vector Store**: Stores and retrieves vector embeddings. Supported built-in providers: `MongoDb`, `Pgvector`, `Qdrant`.

> **Important**: Available provider names in workspace settings come from **registered factories at startup**. Built-in names are `OpenAI` and `Ollama` for embedding; `MongoDb`, `Pgvector`, and `Qdrant` for vector stores.  
> These names only appear in the UI dropdowns when the corresponding packages are installed.

### Installing RAG Dependencies

Install embedding provider (already needed for chat, Ollama covers both chat + embeddings):

```bash
abp add-package Volo.AIManagement.OpenAI
# or
abp add-package Volo.AIManagement.Ollama
```

Install vector store package (pick one):

```bash
abp add-package Volo.AIManagement.VectorStores.Pgvector
# or
abp add-package Volo.AIManagement.VectorStores.MongoDB
# or
abp add-package Volo.AIManagement.VectorStores.Qdrant
```

> ❌ **Do NOT** create custom `IVectorStoreFactory` / `IEmbeddingClientFactory` implementations when official packages exist — install the relevant package instead.

### Supported File Formats (defaults)

| Extension | MIME Type |
|-----------|-----------|
| `.pdf` | `application/pdf` |
| `.md` | `text/markdown` |
| `.txt` | `text/plain` |

Default max file size: **10 MB**. Both are configurable via `WorkspaceDataSourceOptions`.

### Configuring RAG on a Workspace (UI)

1. Navigate to **AI Management → Workspaces** → Create or Edit a workspace.
2. On the **Embedder** tab:
   - **Embedder Provider**: e.g., `Ollama` or `OpenAI`
   - **Embedder Model Name**: e.g., `nomic-embed-text` (Ollama), `text-embedding-3-small` (OpenAI)
   - **Embedder Base URL**: e.g., `http://localhost:11434` (only needed for non-default endpoints)
3. On the **Vector Store** tab:
   - **Vector Store Provider**: e.g., `Pgvector`
   - **Vector Store Settings**: Provider-specific connection string:
     - **Pgvector**: Standard Npgsql connection string — `Host=localhost;Port=5432;Database=ragdb;Username=postgres;Password=postgres`
     - **MongoDB**: Standard MongoDB connection string including database name
     - **Qdrant**: Endpoint string — `http://host:port`, `https://host:port`, or `host:port`

> ⚠️ `VectorStoreSettings` for Pgvector is a **plain PostgreSQL/Npgsql connection string**, NOT JSON.

### Document Processing Pipeline

When a file is uploaded as a workspace data source:

1. File stored in blob storage.
2. `IndexDocumentJob` queued.
3. `DocumentProcessingManager` extracts text via content-type extractors.
4. Text chunked (default: chunk size `1000`, overlap `200`).
5. Embeddings generated in batches and stored via the configured vector store.
6. Data source marked as `IsProcessed = true`.

### Chat Integration Behavior

- AI Management wraps the chat client with a tool named `search_workspace_documents`.
- The tool delegates to `IDocumentSearchService` with `TopK = 5` chunks.
- If RAG retrieval fails, chat continues without injected context.
- RAG metadata returned: `HasRagContext`, `RagChunkCount`.

### Uploading Documents via UI

Navigate to the workspace then click **Data Sources** (or `/AIManagement/WorkspaceDataSources?WorkspaceId={id}`). Uploaded files are auto-processed and indexed.

### Workspace Data Source HTTP API

All endpoints under `/api/ai-management/workspace-data-sources`:

| Method | Path | Action |
|--------|------|--------|
| `POST` | `/workspace/{workspaceId}` | Upload new file |
| `GET` | `/by-workspace/{workspaceId}` | List data sources |
| `GET` | `/{id}` | Get data source |
| `PUT` | `/{id}` | Update metadata |
| `DELETE` | `/{id}` | Delete (removes embeddings, chunks, blob) |
| `GET` | `/{id}/download` | Download original file |
| `POST` | `/{id}/reindex` | Re-index single file |
| `POST` | `/workspace/{workspaceId}/reindex-all` | Re-index all files |

### Customising Upload Options

```csharp
Configure<WorkspaceDataSourceOptions>(options =>
{
    options.AllowedFileExtensions = new[] { ".txt", ".md", ".pdf", ".docx", ".csv" };
    options.MaxFileSize = 50 * 1024 * 1024; // 50 MB
    options.ContentTypeMap[".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    options.ContentTypeMap[".csv"] = "text/csv";
});
```

> Adding new file extensions also requires a matching content extractor — built-in extractors cover `.txt`, `.md`, `.pdf`.

### Automatic Reindexing on Configuration Changes

When workspace embedder or vector store config changes, AI Management automatically:
- Initializes the new vector store configuration (if needed).
- Deletes existing embeddings when embedder provider/model changes.
- Re-queues all workspace data sources for re-indexing.

---

## Anti-Patterns / Common Mistakes

| ❌ Don't | ✅ Do Instead |
|----------|--------------|
| Use `DateTime.Now` for timestamps | Use `Clock.Now` from base class |
| Access `abp.chatComponents.get(id)` at page load | Access inside event handlers (deferred) |
| Use workspace names with spaces | Use `camelCase` or `under_score` |
| Use `includeAllEntities: true` in EF options | Use default repos (aggregate roots only) |
| Hard-code API keys in code | Store in workspace DB record or `appsettings.secrets.json` |
| Create repositories for non-aggregate entities | Only `IWorkspaceRepository` is provided |
| Access `DbContext` directly in app services | Use `IRepository<T>` or `IWorkspaceRepository` |
| Re-use the same `ConversationId` across users | Append `CurrentUser.Id` to make it unique |
| Skip `ConfigureByConvention()` in EF config | Always call `b.ConfigureByConvention()` |

---

## Key Domain Services (Internal Reference)

| Service | Purpose |
|---------|---------|
| `ApplicationWorkspaceManager` | Create / validate / manage workspace entities |
| `IWorkspaceRepository` | Custom repo with `FindByNameAsync` etc. |
| `IWorkspaceConfigurationStore` | Get raw workspace config (with caching) |
| `ChatClientResolver` | Resolves the correct `IChatClient` for a workspace |
| `WorkspaceAppService` | CRUD application service — wire to permissions |
| `ChatCompletionClientAppService` | Client-side chat completion (Scenario 3/4) |

---

## CLI Quick Reference

```bash
# Install the module
abp add-module Volo.AIManagement

# Add AI provider packages
abp add-package Volo.AIManagement.OpenAI
abp add-package Volo.AIManagement.Ollama

# Client packages (microservice consumer)
abp add-package Volo.AIManagement.Client.HttpApi.Client

# Create EF migration after module install
dotnet ef migrations add Added_AIManagement \
  --project src/MVCAllOptions.EntityFrameworkCore

# Run DbMigrator (preferred — applies migration + seeds data)
dotnet run --project src/MVCAllOptions.DbMigrator
```

---

## Key References

| Resource | URL |
|----------|-----|
| AI Management (Pro) Docs (10.2) | https://abp.io/docs/10.2/modules/ai-management |
| AI Management RAG with File Upload (10.2) | https://abp.io/docs/10.2/modules/ai-management#rag-with-file-upload |
| AI Management (Pro) Docs (latest) | https://abp.io/docs/latest/modules/ai-management |
| AI Infrastructure Base | https://abp.io/docs/latest/framework/infrastructure/artificial-intelligence |
| Microsoft.Extensions.AI | https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai |
| Microsoft Agent Framework | https://learn.microsoft.com/en-us/agent-framework/ |
| Semantic Kernel | https://learn.microsoft.com/en-us/semantic-kernel/ |
| ABP Package List | https://abp.io/packages?moduleName=Volo.AIManagement |
| Community: Multi-Workspace Article | https://abp.io/community/articles/eghgty3j |
| Community: SK in ABP | https://abp.io/community/articles/qo5cnuzs |
