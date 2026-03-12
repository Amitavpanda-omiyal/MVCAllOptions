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
/// Branch executor 2: Analyses pricing and market value of a book.
/// Fan-out branch: receives BookNameMessage, emits PricingAnalysisResult.
/// </summary>
internal sealed class PricingAnalysisExecutor(IConfiguration config)
    : Executor<BookNameMessage, PricingAnalysisResult>("pricing-analysis")
{
    public override async ValueTask<PricingAnalysisResult> HandleAsync(BookNameMessage msg, IWorkflowContext context, CancellationToken ct = default)
    {
        var book = BookstoreData.GetByNameExact(msg.BookName)!;
        Console.WriteLine($"  [PricingAnalysis] Analysing pricing for: {book.Name} (${book.Price:F2})");

        var client = ChatClientFactory.Create(config);

        var catalogContext = BookstoreData.Catalogue
            .Where(b => b.Genre == book.Genre && b.Name != book.Name)
            .Select(b => $"  {b.Name}: ${b.Price:F2}")
            .Take(4);

        var prompt = $"""
            Analyse the pricing and value of the following book:
            
            Title: {book.Name}
            Author: {book.Author}
            Genre: {book.Genre}
            Published: {book.PublishDate:yyyy}
            Price: ${book.Price:F2}
            
            Other books in the same genre priced at:
            {string.Join("\n", catalogContext)}
            
            Provide:
            1. Value for money assessment (1-2 sentences)
            2. Price category: Budget / Mid-range / Premium
            3. Market comparison (vs similar genre books)
            4. One-sentence summary
            
            Be concise.
            """;

        var response = await client.CompleteChatAsync(
            [new UserChatMessage(prompt)], cancellationToken: ct);

        var text = response.Value.Content[0].Text;

        return new PricingAnalysisResult(
            BookName:          book.Name,
            ValueForMoney:     ExtractLine(text, "value", "1.") ?? "N/A",
            PriceCategory:     ExtractLine(text, "price category", "2.") ?? "N/A",
            MarketComparison:  ExtractLine(text, "market", "3.") ?? "N/A",
            Summary:           ExtractLine(text, "summary", "4.") ?? text);
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
