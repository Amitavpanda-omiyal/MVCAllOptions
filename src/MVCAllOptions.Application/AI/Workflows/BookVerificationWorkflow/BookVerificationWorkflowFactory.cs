using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace MVCAllOptions.AI.Workflows.BookVerificationWorkflow;

/// <summary>
/// Builds the BookVerification workflow — a 2-step sequential pipeline:
///
///   string (book name)
///       → <see cref="BookExistenceVerifierExecutor"/> — checks existence via LLM knowledge
///       → <see cref="BookSuggestionExecutor"/>        — adds alternatives if not found, emits output
/// </summary>
public static class BookVerificationWorkflowFactory
{
    /// <summary>
    /// Creates a new <see cref="Workflow"/> instance using the supplied <paramref name="chatClient"/>.
    /// The caller is responsible for building <paramref name="chatClient"/> from the workspace
    /// configuration obtained via <c>IWorkspaceConfigurationStore</c>.
    /// </summary>
    public static Workflow Create(ChatClient chatClient, ILogger logger)
    {
        var verifier   = new BookExistenceVerifierExecutor(chatClient, logger);
        var suggester  = new BookSuggestionExecutor(chatClient, logger);

        return new WorkflowBuilder(verifier)
            .AddEdge(verifier, suggester)
            .WithOutputFrom(suggester)
            .WithName("book-existence-verifier") // must match the first executor's id
            .Build();
    }
}
