# MVCAllOptions

## About this solution

This is a layered startup solution based on [Domain Driven Design (DDD)](https://abp.io/docs/latest/framework/architecture/domain-driven-design) practises. All the fundamental ABP modules are already installed. Check the [Application Startup Template](https://abp.io/docs/latest/solution-templates/layered-web-application) documentation for more info.

### Pre-requirements

* [.NET10.0+ SDK](https://dotnet.microsoft.com/download/dotnet)
* [Node v18 or 20](https://nodejs.org/en)

### Configurations

The solution comes with a default configuration that works out of the box. However, you may consider to change the following configuration before running your solution:

* Check the `ConnectionStrings` in `appsettings.json` files under the `MVCAllOptions.Web` and `MVCAllOptions.DbMigrator` projects and change it if you need.
* Check the `ConnectionStrings` in `appsettings.json` files under the `MVCAllOptions.Web.Public` as well.

### Before running the application

* Run `abp install-libs` command on your solution folder to install client-side package dependencies. This step is automatically done when you create a new solution, if you didn't especially disabled it. However, you should run it yourself if you have first cloned this solution from your source control, or added a new client-side package dependency to your solution.
* Run `MVCAllOptions.DbMigrator` to create the initial database. This step is also automatically done when you create a new solution, if you didn't especially disabled it. This should be done in the first run. It is also needed if a new database migration is added to the solution later.

#### Generating a Signing Certificate

In the production environment, you need to use a production signing certificate. ABP Framework sets up signing and encryption certificates in your application and expects an `openiddict.pfx` file in your application.

To generate a signing certificate, you can use the following command:

```bash
dotnet dev-certs https -v -ep openiddict.pfx -p 5e32e7af-8c42-443d-99c3-40ddfe33b72b
```

> `5e32e7af-8c42-443d-99c3-40ddfe33b72b` is the password of the certificate, you can change it to any password you want.

It is recommended to use **two** RSA certificates, distinct from the certificate(s) used for HTTPS: one for encryption, one for signing.

For more information, please refer to: [OpenIddict Certificate Configuration](https://documentation.openiddict.com/configuration/encryption-and-signing-credentials.html#registering-a-certificate-recommended-for-production-ready-scenarios)

> Also, see the [Configuring OpenIddict](https://abp.io/docs/latest/Deployment/Configuring-OpenIddict#production-environment) documentation for more information.

### Solution structure

This is a layered monolith application that consists of the following applications:

* `MVCAllOptions.DbMigrator`: A console application which applies the migrations and also seeds the initial data. It is useful on development as well as on production environment.
* `MVCAllOptions.Web`: ASP.NET Core MVC / Razor Pages application that is the essential web application of the solution.
* `MVCAllOptions.Web.Public`: ASP.NET Core MVC / Razor Pages application that is the public web application of the solution.

#### Test Projects

The `test` folder contains the following test projects:

* `MVCAllOptions.Application.Tests`: Application layer tests.
* `MVCAllOptions.Domain.Tests`: Domain layer tests.
* `MVCAllOptions.EntityFrameworkCore.Tests`: Entity Framework Core integration tests.




## AI Management Integration

This solution integrates the **ABP AI Management (Pro)** module to enable workspace-based AI configuration, RAG (Retrieval-Augmented Generation), and multi-agent workflows.

### What Was Done

#### 1. Packages Installed

Added to `MVCAllOptions.Domain`:

| Package | Version | Purpose |
|---|---|---|
| `Volo.AIManagement.Domain` | 10.2.0-rc.1 | Core AI Management domain |
| `Volo.AIManagement.OpenAI` | 10.2.0-rc.1 | OpenAI chat provider |
| `Volo.AIManagement.OpenAI.Embeddings` | 10.2.0-rc.1 | OpenAI embedding support |
| `Volo.AIManagement.Ollama` | 10.2.0-rc.1 | Ollama (local LLM) chat provider |
| `Volo.AIManagement.Ollama.Embeddings` | 10.2.0-rc.1 | Ollama embedding support |
| `Volo.AIManagement.VectorStores.Pgvector` | 10.2.0-rc.1 | pgvector (PostgreSQL) vector store |

#### 2. Module Configuration (`MVCAllOptionsDomainModule`)

Added `DependsOn` entries:

```csharp
typeof(AIManagementDomainModule),
typeof(AIManagementOpenAIModule),
typeof(AIManagementOllamaModule),
typeof(AIManagementPgvectorModule),
```

Registered embedder and vector store factories so they appear in the Workspace UI:

```csharp
Configure<EmbeddingClientFactoryOptions>(options =>
{
    options.AddFactory<OllamaEmbeddingClientFactory>("Ollama");
    options.AddFactory<OpenAIEmbeddingClientFactory>("OpenAI");
});

Configure<VectorStoreFactoryOptions>(options =>
{
    options.AddFactory<PgvectorStoreFactory>("Pgvector");
});
```

#### 3. Startup Bug Fix (`MVCAllOptionsWebModule`)

The ABP AI Management module v10.0.x has a known bug where `InitialWorkspaceUpdater` at startup only writes **chat** providers to the `AIManagementApplicationAIProviders` table, leaving the embedder and vector store provider columns empty (so they never appear in the Workspace edit UI).

A temporary workaround already existed in `OnPostApplicationInitializationAsync`, but it was only passing chat providers. Fixed it to also pass embedder and vector store providers:

```csharp
// TODO: Remove this method after v10.0.2 is released.
public override async Task OnPostApplicationInitializationAsync(ApplicationInitializationContext context)
{
    var appAIProviderManager = context.ServiceProvider
        .GetRequiredService<ApplicationAIProviderManager>();
    var appInfoAccessor = context.ServiceProvider
        .GetRequiredService<IApplicationInfoAccessor>();
    var chatFactoryOptions = context.ServiceProvider
        .GetRequiredService<IOptions<ChatClientFactoryOptions>>();
    var embeddingFactoryOptions = context.ServiceProvider
        .GetRequiredService<IOptions<EmbeddingClientFactoryOptions>>();
    var vectorStoreFactoryOptions = context.ServiceProvider
        .GetRequiredService<IOptions<VectorStoreFactoryOptions>>();

    await appAIProviderManager.UpdateProvidersAsync(
        appInfoAccessor.ApplicationName!,
        chatFactoryOptions.Value.Factories.Keys.ToArray(),
        embeddingFactoryOptions.Value.Factories.Keys.ToArray(),
        vectorStoreFactoryOptions.Value.Factories.Keys.ToArray()
    );
}
```

> This fix becomes unnecessary once ABP AI Management > v10.0.1 releases the internal update.

#### 4. Docker-Managed Infrastructure

Two containers are required for full RAG functionality:

**Ollama (local LLM)**

```bash
# Pull and run Ollama
docker run -d --name ollama -p 11434:11434 ollama/ollama
docker exec ollama ollama pull llama3:8b
docker exec ollama ollama pull nomic-embed-text
```

**pgvector (vector store)**

```bash
docker run -d --name pgvector-rag \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=ragdb \
  -p 5433:5432 \
  pgvector/pgvector:pg16
```

#### 5. Agents & Workflows (MAF)

AI agent factories and Microsoft Agent Framework (MAF) workflows are implemented in `MVCAllOptions.Application/AI/`:

```
AI/
  Agents/
    BookCatalogAgentFactory.cs      # Agent for book catalog queries
    BookRecommenderAgentFactory.cs  # Agent for book recommendations
  Tools/
    BookstoreTools.cs               # Semantic Kernel tools exposed to agents
  Data/
    BookstoreData.cs                # Data seeding/loader for RAG pipeline
  Workflows/
    BookEnrichmentWorkflow/         # Fan-out workflow to enrich book metadata
    BookRecommendationWorkflow/     # Sequential recommendation pipeline
    BookReviewWorkflow/             # Review generation workflow
  ChatClientFactory.cs              # Custom IChatClientFactory implementation
```

### Workspaces Created

Workspaces are managed at runtime via **AI Management → Workspaces** in the admin UI.

| Workspace | Provider | Model | Embedder | Vector Store |
|---|---|---|---|---|
| `OllamaRAGWorkspace` | Ollama | llama3:8b | Ollama (nomic-embed-text) | Pgvector |
| `OpenAIRAGWorkspace` | OpenAI | gpt-5 | OpenAI (text-embedding-3-small) | — |
| `OpenAIAssistant` | OpenAI | gpt-5 | — | — |
| `OllamaAssistant` | Ollama | llama3.2 | — | — |

### Using `IChatClient` in Application Services

Inject the workspace-backed chat client via the AI Management module:

```csharp
public class MyAppService : ApplicationService
{
    private readonly IChatClientFactory _chatClientFactory;

    public MyAppService(IChatClientFactory chatClientFactory)
    {
        _chatClientFactory = chatClientFactory;
    }

    public async Task<string> AskAsync(string question)
    {
        var client = await _chatClientFactory.CreateAsync("OpenAIRAGWorkspace");
        var response = await client.CompleteAsync(question);
        return response.Message.Text;
    }
}
```

---

## Deploying the application

Deploying an ABP application follows the same process as deploying any .NET or ASP.NET Core application. However, there are important considerations to keep in mind. For detailed guidance, refer to ABP's [deployment documentation](https://abp.io/docs/latest/Deployment/Index).

### Additional resources


#### Internal Resources

You can find detailed setup and configuration guide(s) for your solution below:

* [Docker-Compose](./etc/docker-compose/README.md)
* [Docker-Compose for Infrastructure Dependencies](./etc/docker/README.md)
* [Local Kubernetes Guide](./etc/helm/README.md)

#### External Resources
You can see the following resources to learn more about your solution and the ABP Framework:

* [Web Application Development Tutorial](https://abp.io/docs/latest/tutorials/book-store/part-1)
* [Application Startup Template](https://abp.io/docs/latest/startup-templates/application/index)
* [LeptonX Theme Module](https://abp.io/docs/latest/themes/lepton-x/index)
* [LeptonX MVC UI](https://abp.io/docs/latest/themes/lepton-x/mvc)
