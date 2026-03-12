using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Configuration;
using MVCAllOptions.AI.Data;
using OpenAI.Chat;

namespace MVCAllOptions.AI.Workflows.BookReviewWorkflow;

/// <summary>
/// Branch executor 3: Classifies a book's genre, sub-genres and key themes.
/// Fan-out branch: receives BookNameMessage, emits GenreClassificationResult.
/// </summary>
internal sealed class GenreClassificationExecutor(IConfiguration config)
    : Executor<BookNameMessage, GenreClassificationResult>("genre-classification")
{
    public override async ValueTask<GenreClassificationResult> HandleAsync(BookNameMessage msg, IWorkflowContext context, CancellationToken ct = default)
    {
        var book = BookstoreData.GetByNameExact(msg.BookName)!;
        Console.WriteLine($"  [GenreClassification] Classifying: {book.Name}");

        var client = ChatClientFactory.Create(config);

        var prompt = $"""
            Classify the following book in detail:
            
            Title: {book.Name}
            Author: {book.Author}
            Catalogue genre: {book.Genre}
            Published: {book.PublishDate:yyyy}
            Description: {book.Description}
            
            Provide:
            1. Primary genre (confirm or refine the catalogue genre)
            2. Sub-genres (comma separated, e.g. "Psychological thriller, Coming-of-age")
            3. Key themes (2-4 themes, comma separated)
            4. One-sentence classification summary
            
            Be concise.
            """;

        var response = await client.CompleteChatAsync(
            [new UserChatMessage(prompt)], cancellationToken: ct);

        var text = response.Value.Content[0].Text;

        return new GenreClassificationResult(
            BookName:     book.Name,
            PrimaryGenre: ExtractLine(text, "primary genre", "1.") ?? "N/A",
            SubGenres:    ExtractLine(text, "sub-genre", "2.") ?? "N/A",
            Themes:       ExtractLine(text, "theme", "3.") ?? "N/A",
            Summary:      ExtractLine(text, "summary", "4.") ?? text);
    }

    private static string? ExtractLine(string text, string keyword, string fallback)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return lines.FirstOrDefault(l =>
            l.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            l.TrimStart().StartsWith(fallback))
            ?? lines.LastOrDefault();
    }
}
