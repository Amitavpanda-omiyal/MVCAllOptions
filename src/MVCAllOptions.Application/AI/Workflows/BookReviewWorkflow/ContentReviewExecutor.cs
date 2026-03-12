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
/// Branch executor 1: Reviews the literary content and readability of a book.
/// Fan-out branch: receives BookNameMessage, emits ContentReviewResult.
/// </summary>
internal sealed class ContentReviewExecutor(IConfiguration config)
    : Executor<BookNameMessage, ContentReviewResult>("content-review")
{
    public override async ValueTask<ContentReviewResult> HandleAsync(BookNameMessage msg, IWorkflowContext context, CancellationToken ct = default)
    {
        var book = BookstoreData.GetByNameExact(msg.BookName)!;
        Console.WriteLine($"  [ContentReview] Analysing content for: {book.Name}");

        var client = ChatClientFactory.Create(config);

        var prompt = $"""
            Analyse the following book and provide a concise content review:
            
            Title: {book.Name}
            Author: {book.Author}
            Genre: {book.Genre}
            Description: {book.Description}
            
            Provide:
            1. Literary merit (1-2 sentences)
            2. Readability score: Easy / Moderate / Advanced
            3. Target audience
            4. One-sentence summary of your review
            
            Be concise, each point in 1-2 sentences.
            """;

        var response = await client.CompleteChatAsync(
            [new UserChatMessage(prompt)], cancellationToken: ct);

        var text = response.Value.Content[0].Text;

        return new ContentReviewResult(
            BookName:        book.Name,
            LiteraryMerit:   ExtractSection(text, "literary merit", "1.") ?? "N/A",
            ReadabilityScore: ExtractSection(text, "readability", "2.") ?? "N/A",
            TargetAudience:  ExtractSection(text, "target audience", "3.") ?? "N/A",
            Summary:         ExtractSection(text, "summary", "4.") ?? text);
    }

    private static string? ExtractSection(string text, string keyword, string fallback)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return lines.FirstOrDefault(l =>
            l.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            l.TrimStart().StartsWith(fallback))
            ?? lines.LastOrDefault();
    }
}
