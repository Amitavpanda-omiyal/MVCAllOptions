using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Configuration;
using MVCAllOptions.AI.Data;
using OpenAI.Chat;

namespace MVCAllOptions.AI.Workflows.BookRecommendationWorkflow;

/// <summary>
/// Step 3 of 3: formats the book shortlist into a polished, personalised recommendation card.
/// Input: BookShortlist → Output: RecommendationCard
/// </summary>
internal sealed class RecommendationFormatterExecutor(IConfiguration config)
    : Executor<BookShortlist, RecommendationCard>("recommendation-formatter")
{
    public override async ValueTask<RecommendationCard> HandleAsync(BookShortlist shortlist, IWorkflowContext context, CancellationToken ct = default)
    {
        Console.WriteLine($"[RecommendationFormatter] Formatting {shortlist.SelectedTitles.Count} recommendations...");

        // Enrich with catalogue data
        var bookDetails = shortlist.SelectedTitles
            .Select(t => BookstoreData.GetByNameExact(t) ?? BookstoreData.SearchByName(t).FirstOrDefault())
            .Where(b => b is not null)
            .Select(b => b!)
            .ToList();

        var catalogueContext = string.Join("\n\n", bookDetails.Select(b => $"""
            Title: {b.Name}
            Author: {b.Author}
            Genre: {b.Genre}
            Price: ${b.Price:F2}
            Description: {b.Description}
            """));

        var client = ChatClientFactory.Create(config);

        var prompt = $$"""
            Write personalised book recommendations based on these selections:

            Reader profile: {{shortlist.PreferencesSummary}}
            Selection rationale: {{shortlist.SelectionRationale}}
            
            Books to recommend:
            {{catalogueContext}}
            
            For EACH book, write a "WhyItFits" personalised explanation (1-2 sentences) explaining why THIS reader will enjoy it.
            
            Return ONLY valid JSON:
            {
              "Recommendations": [
                {
                  "Title": "...",
                  "WhyItFits": "..."
                }
              ],
              "ClosingNote": "A warm 1-sentence closing note encouraging the reader."
            }
            """;

        var response = await client.CompleteChatAsync(
            [new UserChatMessage(prompt)], cancellationToken: ct);

        var json = response.Value.Content[0].Text
            .Replace("```json", "").Replace("```", "").Trim();

        List<RecommendedBook> recs;
        string closing;

        try
        {
            using var doc = JsonDocument.Parse(json);
            closing = doc.RootElement.GetProperty("ClosingNote").GetString() ?? "Happy reading!";

            recs = doc.RootElement.GetProperty("Recommendations")
                .EnumerateArray()
                .Select(el =>
                {
                    var title  = el.GetProperty("Title").GetString() ?? "";
                    var why    = el.GetProperty("WhyItFits").GetString() ?? "";
                    var book   = BookstoreData.GetByNameExact(title) ?? bookDetails.FirstOrDefault();
                    return new RecommendedBook(
                        Title:     title,
                        Author:    book?.Author ?? "Unknown",
                        Genre:     book?.Genre  ?? "Unknown",
                        Price:     book is not null ? $"${book.Price:F2}" : "Unknown",
                        WhyItFits: why);
                })
                .ToList();
        }
        catch
        {
            // Fallback: plain recommendations without JSON parsing
            recs = bookDetails.Select(b => new RecommendedBook(
                Title:     b.Name,
                Author:    b.Author,
                Genre:     b.Genre,
                Price:     $"${b.Price:F2}",
                WhyItFits: shortlist.SelectionRationale))
                .ToList();
            closing = "Happy reading!";
        }

        Console.WriteLine($"[RecommendationFormatter] ✓ {recs.Count} personalised recommendations ready.");

        return new RecommendationCard(
            UserPreferenceSummary: shortlist.PreferencesSummary,
            Recommendations:       recs,
            ClosingNote:           closing);
    }
}
