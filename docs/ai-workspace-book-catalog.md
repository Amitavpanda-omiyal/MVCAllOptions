# Book Catalog Injection (AI Management Chat Workspaces)

This document explains how the project was wired so the **ABP AI Management Chat Playground** (`/AIManagement/Workspaces/{workspace}`) can answer questions about books from the database.

## What this integration does

- When a workspace uses the **OpenAI** provider, the chat client is wrapped so that every request is preceded by a **system message containing the full book catalog**.
- This allows the model to answer questions like “What books do you have? List all with prices.” without any manual prompt edits.

## Key files changed/added

### 1) `BookContextChatClient.cs` (new)
**Location:** `src/MVCAllOptions.Application/AI/BookContextChatClient.cs`

This is a `DelegatingChatClient` that:
- Reads `Book` entities from the DB via `IRepository<Book, Guid>`
- Builds a system prompt listing every book with price + genre + publication date
- Prepends that system message to every outgoing chat request

### 2) `BookAwareOpenAIChatClientFactory.cs` (new)
**Location:** `src/MVCAllOptions.Application/AI/BookAwareOpenAIChatClientFactory.cs`

This is a custom `IChatClientFactory` registered for provider `"OpenAI"`.

It does three things:
1. Creates the real OpenAI `IChatClient`.
2. Wraps it with `BookContextChatClient` when the workspace name is `OpenAIRAGWorkspace`.

   ```csharp
   if (string.Equals(configuration.Name, "OpenAIRAGWorkspace", StringComparison.Ordinal))
   {
       innerClient = new BookContextChatClient(innerClient, _scopeFactory);
   }
   ```

   That conditional is the *exact place* to add support for additional workspaces.
3. Wraps everything with `FunctionInvokingChatClient` (required by ABP’s RAG/document tools pipeline).


### 3) `MVCAllOptionsApplicationModule.cs` (modified)
**Location:** `src/MVCAllOptions.Application/MVCAllOptionsApplicationModule.cs`

Registers the factory in ABP:

```csharp
Configure<ChatClientFactoryOptions>(options =>
{
    options.AddFactory<BookAwareOpenAIChatClientFactory>("OpenAI");
});
```

### 4) `OpenAIRAGWorkspace.cs` (new)
**Location:** `src/MVCAllOptions.Domain/OpenAIRAGWorkspace.cs`

A marker class used by `IChatClient<OpenAIRAGWorkspace>` and to identify which AI Management workspace should receive the catalog injection.

-----------------------

## How to add a new workspace that behaves the same way

### Step 1 — Add a new workspace marker class
Create a new class in `src/MVCAllOptions.Domain/`:

```csharp
using Volo.Abp.AI;

[WorkspaceName("MyNewBookWorkspace")]
public class MyNewBookWorkspace { }
```

### Step 2 — Create the workspace in the UI
Go to **AI Management → Workspaces → New Workspace** and set:
- **Name**: `MyNewBookWorkspace` (must match the marker)
- **Provider**: `OpenAI`
- **Model**: e.g. `gpt-5`
- Set API key / system prompt etc.

### Step 3 — Update the factory to apply to the new workspace
In `BookAwareOpenAIChatClientFactory.cs`, extend the `if` condition:

```csharp
if (configuration.Name == "OpenAIRAGWorkspace" || configuration.Name == "MyNewBookWorkspace")
{
    innerClient = new BookContextChatClient(innerClient, _scopeFactory);
}
```

This ensures the new workspace also gets the book catalog injected.

---

## Alternative: apply catalog injection to *all* workspaces
If you want every OpenAI workspace to automatically receive the catalogue, change the factory to **always** wrap the client:

```csharp
innerClient = new BookContextChatClient(innerClient, _scopeFactory);
```

(Then you don’t need to update the `if` condition when adding a new workspace.)

---

## How to verify it works
1. Run the application.
2. Visit `https://localhost:44324/AIManagement/Workspaces/OpenAIRAGWorkspace`.
3. Send a prompt like: **“What books do you have? List all with prices.”**
4. Confirm the response includes your DB books.

---

## Line-by-line runtime walkthrough (what happens when you send a chat)

### 1) Send message from the UI
The browser calls ABP’s chat endpoint (e.g. `/api/chat-completion/stream/start`) with:
- `workspaceName = "OpenAIRAGWorkspace"`
- the user message
- any chat options (streaming, etc.)

### 2) ABP loads the workspace and chooses the Chat Client factory
ABP AI Management:
1. Loads the workspace record `OpenAIRAGWorkspace` from the DB.
2. Reads `Provider = "OpenAI"`.
3. Looks up `ChatClientFactoryOptions.Factories["OpenAI"]`.

That factory is our custom class:
`BookAwareOpenAIChatClientFactory`.

### 3) The factory builds the pipeline
**File:** `BookAwareOpenAIChatClientFactory.cs`

The factory does:
1. Creates the real OpenAI chat client.
2. If `configuration.Name == "OpenAIRAGWorkspace"`, it wraps it in `BookContextChatClient`.
3. Wraps everything in `FunctionInvokingChatClient` (required by ABP’s RAG/tool support).

So the runtime chain becomes:

```
FunctionInvokingChatClient
  └─ BookContextChatClient
       └─ OpenAI ChatClient
```

### 4) BookContextChatClient injects the catalogue
**File:** `BookContextChatClient.cs`

`BookContextChatClient` intercepts the message before it hits OpenAI.

- It creates a DI scope using `IServiceScopeFactory`.
- It loads all books using `IRepository<Book, Guid>.GetListAsync()`.
- It formats a system message containing the full catalog.
- It prepends that system message to the user message.

So the model receives a message list like:
1) System: "Here is the full catalog..."
2) User: "What books do you have?"

### 5) OpenAI generates a response
Because the model sees the catalog in the system message, it can answer accurately (e.g., list titles + prices).

---

## Notes
- The `FunctionInvokingChatClient` wrapper is required by ABP’s RAG/document search tooling, even if you’re not using file upload or RAG.
- If you want the model to actually create new books, you’ll need a tool/function integration (not covered here).