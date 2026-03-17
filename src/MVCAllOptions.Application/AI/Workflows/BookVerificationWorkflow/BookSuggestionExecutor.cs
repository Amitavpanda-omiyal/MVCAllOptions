using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace MVCAllOptions.AI.Workflows.BookVerificationWorkflow;

/// <summary>
/// Step 2 (final) of the BookVerification pipeline.
/// If the book was found, it just builds a summary and emits the output.
/// If the book was NOT found, it first asks the LLM for 3 real alternative
/// book titles (similar genre/theme) before emitting.
/// </summary>
internal sealed class BookSuggestionExecutor(ChatClient chatClient, ILogger logger)
    : Executor<BookVerificationResult, BookVerificationFinalResult>("book-suggestion")
{
    private const string SuggestionSystemPrompt = """
        You are a knowledgeable librarian.

        The user tried to add a book to their catalogue but it does not exist as a published title.
        Suggest exactly 3 REAL, published books that are similar in genre or theme.

        Respond with ONLY a comma-separated list of book titles, for example:
        "Dune, Foundation, The Left Hand of Darkness"

        Rules:
        - Only include real, confirmed published titles.
        - No markdown, no numbering, no explanations — just the comma-separated list.
        """;

    public override async ValueTask<BookVerificationFinalResult> HandleAsync(
        BookVerificationResult input,
        IWorkflowContext ctx,
        CancellationToken ct = default)
    {
        logger.LogInformation(
            "[MAF Step 2/2 ► BookSuggestion] Received result for '{BookName}' — ExistsInRealWorld={Exists}",
            input.BookName, input.ExistsInRealWorld);

        string suggestions = string.Empty;

        if (!input.ExistsInRealWorld)
        {
            logger.LogInformation(
                "[MAF Step 2/2 ► BookSuggestion] '{BookName}' not found — asking LLM for 3 similar real books",
                input.BookName);
            var messages = new List<ChatMessage>
            {
                ChatMessage.CreateSystemMessage(SuggestionSystemPrompt),
                ChatMessage.CreateUserMessage(
                    $"The book title \"{input.BookName}\" was not found. " +
                    "Suggest 3 real published books that are similar."),
            };

            var response = await chatClient.CompleteChatAsync(messages, cancellationToken: ct);
            suggestions = response.Value.Content[0].Text?.Trim() ?? string.Empty;
            logger.LogInformation(
                "[MAF Step 2/2 ► BookSuggestion] Suggestions for '{BookName}': {Suggestions}",
                input.BookName, suggestions);
        }

        var summary = input.ExistsInRealWorld
            ? $"\"{input.BookName}\" EXISTS — Publisher: {input.PublisherName}, Published: {input.PublicationDate}"
            : $"\"{input.BookName}\" NOT FOUND — Suggestions: {suggestions}";

        var result = new BookVerificationFinalResult(
            BookName:              input.BookName,
            ExistsInRealWorld:    input.ExistsInRealWorld,
            PublisherName:        input.PublisherName,
            PublicationDate:      input.PublicationDate,
            SuggestedAlternatives: suggestions,
            Summary:              summary);

        logger.LogInformation(
            "[MAF Step 2/2 ► BookSuggestion] ✔ Workflow complete for '{BookName}' — {Summary}",
            input.BookName, summary);

        await ctx.YieldOutputAsync(result, ct);
        return result;
    }
}
