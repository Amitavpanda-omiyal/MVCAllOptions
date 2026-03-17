using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace MVCAllOptions.AI.Workflows.BookVerificationWorkflow;

/// <summary>
/// Step 1 of the BookVerification pipeline.
/// Receives the book name (string) and asks the LLM whether the book is real,
/// who published it, and when it was first published.
/// Returns a <see cref="BookVerificationResult"/> for Step 2.
/// </summary>
internal sealed class BookExistenceVerifierExecutor(ChatClient chatClient, ILogger logger)
    : Executor<string, BookVerificationResult>("book-existence-verifier")
{
    private const string SystemPrompt = """
        You are a knowledgeable librarian with encyclopaedic knowledge of published books.

        When given a book title, determine whether it is a real, published book.
        Respond with ONLY a valid JSON object — no markdown fences, no explanation:

        {
          "exists": true or false,
          "publisher": "<publisher name or empty string>",
          "publicationDate": "<year or full date, e.g. 1965 or 1965-08-01, or empty string>",
          "notes": "<one sentence about the book if it exists, otherwise empty string>"
        }

        Rules:
        - "exists" must be a JSON boolean (not a string).
        - If the book does not exist, set "publisher" and "publicationDate" to empty strings.
        - Base your answer on your training knowledge — do not make up publishers.
        - Self-published or indie titles count as existing if you are confident they are real.
        """;

    public override async ValueTask<BookVerificationResult> HandleAsync(
        string bookName,
        IWorkflowContext ctx,
        CancellationToken ct = default)
    {
        logger.LogInformation(
            "[MAF Step 1/2 ► BookExistenceVerifier] Querying LLM — does '{BookName}' exist as a real published book?",
            bookName);

        var messages = new List<ChatMessage>
        {
            ChatMessage.CreateSystemMessage(SystemPrompt),
            ChatMessage.CreateUserMessage($"Book title: \"{bookName}\""),
        };

        var response = await chatClient.CompleteChatAsync(messages, cancellationToken: ct);
        var rawText = response.Value.Content[0].Text ?? string.Empty;

        logger.LogDebug(
            "[MAF Step 1/2 ► BookExistenceVerifier] Raw LLM response for '{BookName}': {Raw}",
            bookName, rawText);

        // Parse JSON — be tolerant if the LLM wraps in markdown fences
        var json = rawText.Trim();
        if (json.StartsWith("```", StringComparison.Ordinal))
        {
            var start = json.IndexOf('{');
            var end = json.LastIndexOf('}');
            if (start >= 0 && end >= start)
                json = json[start..(end + 1)];
        }

        bool exists = false;
        string publisher = string.Empty;
        string pubDate = string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            exists    = root.TryGetProperty("exists", out var ep) && ep.GetBoolean();
            publisher = root.TryGetProperty("publisher", out var pp) ? pp.GetString() ?? string.Empty : string.Empty;
            pubDate   = root.TryGetProperty("publicationDate", out var dp) ? dp.GetString() ?? string.Empty : string.Empty;
        }
        catch (JsonException)
        {
            // The LLM did not return valid JSON — treat as not found
            exists = rawText.Contains("true", StringComparison.OrdinalIgnoreCase) &&
                     !rawText.Contains("\"exists\": false", StringComparison.OrdinalIgnoreCase);
        }

        logger.LogInformation(
            "[MAF Step 1/2 ► BookExistenceVerifier] Result for '{BookName}': Exists={Exists}, Publisher='{Publisher}', Date='{Date}'",
            bookName, exists, publisher, pubDate);

        return new BookVerificationResult(bookName, exists, publisher, pubDate, rawText);
    }
}
