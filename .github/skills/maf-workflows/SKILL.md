---
name: maf-workflows
description: "Microsoft Agent Framework (MAF) workflows, agents, and DevUI. Use when: implementing MAF agents, creating fan-out/fan-in workflows, building sequential pipelines, wiring DevUI, debugging executor routing, composing agent-as-a-tool patterns, understanding WorkflowBuilder API, adding new executors or workflow steps, troubleshooting IWorkflowContext state, registering agents and workflows with DI, or any task involving Microsoft.Agents.AI.* packages. ALWAYS fetch fresh docs from https://learn.microsoft.com/en-us/agent-framework/ and https://learn.microsoft.com/en-us/agent-framework/workflows/ before generating code. Supports both OpenRouter (current) and Azure OpenAI backends."
argument-hint: "Describe the workflow or agent you want to build, or paste the error to diagnose."
---

# Microsoft Agent Framework (MAF) — Workflows & DevUI

## Purpose

This skill guides implementation, debugging, and extension of **Microsoft Agent Framework** agents and workflows inside this repository — including the DevUI visualiser and OpenRouter/Azure OpenAI connectivity.

All MAF code lives in `src/MVCAllOptions.AgentWorkflows/`.

---

## When to Use

- Adding or modifying an `AIAgent` (tool-calling, agent-as-a-tool)
- Creating or extending a `Workflow` (fan-out/fan-in, sequential pipeline)
- Wiring up `DevUI` (registration, route mapping, live visualisation)
- Diagnosing executor routing, state accumulation, or output emission bugs
- Understanding any `Microsoft.Agents.AI.*` API
- Choosing between executor patterns (`Executor<TIn,TOut>` vs multi-handler `Executor`)

---

## Quick Orientation — Project Layout

```
src/MVCAllOptions.AgentWorkflows/
├── Program.cs                          — Host setup, DevUI, DI registration
├── ChatClientFactory.cs                — OpenRouter ChatClient factory
├── appsettings.json                    — OpenRouter:ApiKey, OpenRouter:Model
├── Agents/
│   ├── BookCatalogAgentFactory.cs      — Tool-calling agent (AIFunctionFactory)
│   └── BookRecommenderAgentFactory.cs  — Agent-as-a-tool (catalogAgent.AsAIFunction())
├── Workflows/
│   ├── BookReviewWorkflow/             — Fan-out / fan-in pattern
│   │   ├── BookReviewWorkflowFactory.cs
│   │   ├── BookReviewDispatcher.cs     — Entry executor (Executor<string,BookNameMessage>)
│   │   ├── ContentReviewExecutor.cs    — Branch executor
│   │   ├── PricingAnalysisExecutor.cs  — Branch executor
│   │   ├── GenreClassificationExecutor.cs — Branch executor
│   │   ├── ReviewAggregatorExecutor.cs — Fan-in aggregator (multi-handler Executor)
│   │   └── BookReviewMessages.cs       — Message/result record types
│   └── BookRecommendationWorkflow/     — Sequential pipeline pattern
│       ├── BookRecommendationWorkflowFactory.cs
│       ├── PreferenceAnalyzerExecutor.cs  — Step 1: string → StructuredPreferences
│       ├── BookFinderExecutor.cs          — Step 2: StructuredPreferences → BookMatches
│       ├── RecommendationFormatterExecutor.cs — Step 3: BookMatches → RecommendationCard
│       └── BookRecommendationMessages.cs
├── Tools/
│   └── BookstoreTools.cs               — Static methods wrapped with AIFunctionFactory
└── Data/
    └── BookstoreData.cs                — In-memory book catalogue
```

---

## Step 1 — Fetch Up-to-Date MAF Documentation (MANDATORY)

**Do this every time this skill is invoked**, before writing or editing any MAF code. MAF is in public preview — APIs, package names, and method signatures change between preview drops.

### Required fetches (always fetch all of these)

Fetch each URL using the `fetch-webpage` tool (or context7 if indexed) and read the content before proceeding:

| URL | Purpose |
|-----|---------|
| `https://learn.microsoft.com/en-us/agent-framework/` | Main MAF reference — agents, hosting, overview |
| `https://learn.microsoft.com/en-us/agent-framework/workflows/` | Workflow API — WorkflowBuilder, Executor, IWorkflowContext |
| `https://github.com/microsoft/agent-framework/issues/2865` | Known DevUI issue tracker — check before wiring DevUI |
| `https://github.com/webmaxru/awesome-microsoft-agent-framework` | Community samples — patterns, workarounds, latest examples |

### context7 MCP (try first, fall back to fetch-webpage)

```
resolve-library-id → "microsoft agent framework"
get-library-docs   → topic: "workflows" | "agents" | "devui" | "executors"
```

If context7 does not have the library indexed (MAF is preview), fall back to direct URL fetches above — do **not** skip the step.

### What to extract from the fetches

- Current package versions on NuGet (preview tags change frequently)
- Any breaking API changes since the last skill use
- Currently open DevUI bugs (from the GitHub issue link) that may affect the task
- New community patterns or workarounds from the awesome-list

---

## Step 2 — Choose the Right Pattern

| Goal | Pattern | Reference file |
|------|---------|----------------|
| Tool-calling agent | `chatClient.AsAIAgent(tools: [...])` + `AIFunctionFactory.Create(method)` | [API cheatsheet](./references/api-cheatsheet.md) |
| One agent calling another | `innerAgent.AsAIFunction(options)` as a tool in the outer agent | [Patterns](./references/patterns.md) |
| Sequential multi-step pipeline | `WorkflowBuilder` with `AddEdge` chained linearly | [Patterns](./references/patterns.md) |
| Parallel branches + merge | Fan-out dispatcher + multi-handler aggregator (`ConfigureRoutes`) | [Patterns](./references/patterns.md) |
| Accumulate partial results | `IWorkflowContext.QueueStateUpdateAsync` + `ReadStateAsync` | [API cheatsheet](./references/api-cheatsheet.md) |
| Emit final output | `IWorkflowContext.YieldOutputAsync(result, ct)` + `WithOutputFrom(executor)` | [API cheatsheet](./references/api-cheatsheet.md) |
| Visualise in browser | DevUI registration + `app.MapDevUI()` + named DI keys | [API cheatsheet](./references/api-cheatsheet.md) |

---

## Step 3 — Implement

### 3a. Adding a Tool-Calling Agent

1. Create a static factory class in `Agents/`.
2. Call `ChatClientFactory.Create(config)` to get a `ChatClient`.
3. Wrap static tool methods with `AIFunctionFactory.Create(MyTools.MethodName)`.
4. Return `chatClient.AsAIAgent(instructions, name, description, tools: [...])`.
5. Register in `Program.cs`:
   ```csharp
   builder.AddAIAgent("AgentName", (_, _) => MyAgentFactory.Create(config));
   ```

### 3b. Composing Agent-as-a-Tool

1. Build the inner agent first (Step 3a).
2. In the outer agent factory, convert it:
   ```csharp
   AIFunction innerTool = innerAgent.AsAIFunction(
       options: new AIFunctionFactoryOptions { Name = "tool_name", Description = "..." });
   ```
3. Pass `innerTool` in the outer agent's `tools` array.

### 3c. Adding a Sequential Workflow

1. Create a folder under `Workflows/NewWorkflow/`.
2. For each step, create `Executor<TInput, TOutput>("step-id")` and implement `HandleAsync`.
3. Create a factory:
   ```csharp
   return new WorkflowBuilder(step1)
       .AddEdge(step1, step2)
       .AddEdge(step2, step3)
       .WithOutputFrom(step3)
       .WithName("step1-id")   // must match first executor's id
       .Build();
   ```
4. Register in `Program.cs`:
   ```csharp
   builder.AddWorkflow("step1-id", (_, _) => MyWorkflowFactory.Create(config));
   ```

### 3d. Adding a Fan-Out / Fan-In Workflow

1. **Dispatcher** — `Executor<TInput, TBranchMessage>("dispatcher-id")`. Return the same `TBranchMessage` — MAF sends it to **all** outgoing edges simultaneously.
2. **Branch executors** — each is `Executor<TBranchMessage, TBranchResult>("branch-id")`.
3. **Aggregator** — derive from base `Executor("aggregator-id")` and override `ConfigureRoutes`:
   ```csharp
   protected override RouteBuilder ConfigureRoutes(RouteBuilder rb) => rb
       .AddHandler<BranchResultA>(HandleAAsync)
       .AddHandler<BranchResultB>(HandleBAsync);
   ```
4. In each handler, call `QueueStateUpdateAsync` to store the partial result, then attempt `TryEmitAsync` which calls `YieldOutputAsync` once all branches are present.
5. Wire with `WorkflowBuilder` + `WithOutputFrom(aggregator)`.

### 3e. Using IWorkflowContext

```csharp
// Store partial state (safe for concurrent fan-in)
await ctx.QueueStateUpdateAsync("key", value, cancellationToken: ct);

// Read accumulated state (null if not yet received)
var result = await ctx.ReadStateAsync<TResult>("key", cancellationToken: ct);

// Emit final output (triggers WorkflowOutputEvent for callers)
await ctx.YieldOutputAsync(finalResult, ct);
```

### 3f. Running Programmatically (Console Demo / Tests)

```csharp
await using var run = await InProcessExecution.RunAsync<TInput>(workflow, input, Guid.NewGuid().ToString(), ct);

var outputEvent = run.OutgoingEvents
    .OfType<WorkflowOutputEvent>()
    .FirstOrDefault(e => e.Is<TOutput>());

if (outputEvent?.Is<TOutput>(out var result) == true)
    Console.WriteLine(result);
```

---

## Step 4 — DevUI Wiring Checklist

The DevUI **requires** specific registration order in `Program.cs`:

```csharp
// 1. Build host
var builder = WebApplication.CreateBuilder(args);

// 2. DevUI + OpenAI-compatible endpoints
builder.AddDevUI();
builder.AddOpenAIResponses();   // POST /v1/responses
builder.AddOpenAIConversations(); // GET+POST /v1/conversations

// 3. Register agents with EXACT matching names
builder.AddAIAgent("AgentName", (_, _) => agentInstance);

// 4. Register workflows — name must match WithName() in WorkflowBuilder
builder.AddWorkflow("first-executor-id", (_, _) => WorkflowFactory.Create(config));

// 5. Build and map
var app = builder.Build();
app.MapDevUI();             // serves /devui SPA
app.MapOpenAIResponses();
app.MapOpenAIConversations();
```

Open `http://localhost:5000/devui` after `dotnet run`.

---

## Step 5 — Configuration & LLM Backend

This project currently uses **OpenRouter** as the LLM backend. Azure OpenAI is the alternative.
See [LLM backends reference](./references/llm-backends.md) for full `ChatClientFactory` implementations for both.

### Active: OpenRouter

`appsettings.json`:
```json
{
  "OpenRouter": {
    "ApiKey": "sk-or-v1-...",
    "Model": "openai/gpt-4o-mini"
  }
}
```

Environment variables (override appsettings):
```bash
export OPENROUTER__APIKEY="sk-or-v1-..."
export OPENROUTER__MODEL="openai/gpt-4o-mini"
```

Models available at: `https://openrouter.ai/models`

### Alternative: Azure OpenAI

`appsettings.json`:
```json
{
  "AzureOpenAI": {
    "Endpoint": "https://<resource>.openai.azure.com/",
    "DeploymentName": "gpt-4o-mini"
  }
}
```

Requires:
```bash
az login   # AzureCliCredential used — no API key needed in config
```

To switch backends, replace the body of `ChatClientFactory.Create()` — see [LLM backends reference](./references/llm-backends.md).

---

## Step 6 — Packages (check versions before adding)

```xml
<PackageReference Include="Microsoft.Agents.AI.OpenAI"    Version="1.0.0-preview.*" />
<PackageReference Include="Microsoft.Agents.AI.Workflows" Version="1.0.0-preview.*" />
<PackageReference Include="Microsoft.Agents.AI.DevUI"     Version="1.0.0-preview.*" />
```

Target framework: `net10.0` (required by preview packages).

> Always check nuget.org or the GitHub releases for the latest preview version before scaffolding.

---

## Step 7 — Validate

- [ ] `dotnet build src/MVCAllOptions.AgentWorkflows` passes with no errors
- [ ] `dotnet run` starts without exceptions and prints the console demo
- [ ] DevUI at `http://localhost:5000/devui` shows all registered agents and workflows
- [ ] Agent/workflow names shown in DevUI exactly match the names in `AddAIAgent` / `AddWorkflow`
- [ ] `WorkflowOutputEvent` is emitted for each workflow run
- [ ] Fan-in aggregator emits output only once (all branches received)

---

## Common Pitfalls

| Symptom | Cause | Fix |
|---------|-------|-----|
| Workflow name mismatch in DevUI | `WithName("id")` ≠ first executor's constructor id | Ensure both strings match exactly |
| Aggregator emits multiple outputs | `YieldOutputAsync` called before all branches arrive | Guard with null checks on all `ReadStateAsync` calls |
| DevUI shows no agents/workflows | Missing `AddAIAgent` / `AddWorkflow` registration | Check `Program.cs` registrations |
| `WorkflowOutputEvent.Is<T>()` returns false | Output type mismatch | Verify the `TOutput` generic matches `YieldOutputAsync<T>` call |
| ChatClient returns null / empty | Missing or wrong `OpenRouter:ApiKey` config | Check `appsettings.json` or env vars |
| Preview package not found | Old NuGet feed | Add `https://pkgs.dev.azure.com/azure-sdk/...` or check README |

---

## References

- [MAF API Cheatsheet](./references/api-cheatsheet.md) — Key types, method signatures, generic constraints
- [Patterns Reference](./references/patterns.md) — Full code skeletons for every pattern
- [LLM Backends](./references/llm-backends.md) — OpenRouter vs Azure OpenAI `ChatClientFactory` implementations
- MAF main docs: `https://learn.microsoft.com/en-us/agent-framework/`
- MAF workflow docs: `https://learn.microsoft.com/en-us/agent-framework/workflows/`
- DevUI known issues: `https://github.com/microsoft/agent-framework/issues/2865`
- Community samples: `https://github.com/webmaxru/awesome-microsoft-agent-framework`

### ABP AI Integration

- ABP AI Infrastructure overview: `https://abp.io/docs/10.2/framework/infrastructure/artificial-intelligence`
  — Workspace concept, `Volo.Abp.AI` install, `IChatClient` / `IChatClientAccessor`, supported AI libraries
- ABP Microsoft Agent Framework integration: `https://abp.io/docs/latest/framework/infrastructure/artificial-intelligence/microsoft-agent-framework`
  — Using `IChatClient.CreateAIAgent()` inside ABP app services, typed workspace injection (`IChatClient<TWorkspace>`), `AbpAIWorkspaceOptions` configuration
- Key pattern from docs: MAF is the **recommended choice** for ABP applications (successor to AutoGen + Semantic Kernel).
  Inject `IChatClient` (or `IChatClient<TWorkspace>`) directly into Application Services and call `_chatClient.CreateAIAgent(instructions)` — no separate host required for agent logic.
  The `AgentWorkflows` host in this project adds **DevUI visualisation** on top of that baseline.
