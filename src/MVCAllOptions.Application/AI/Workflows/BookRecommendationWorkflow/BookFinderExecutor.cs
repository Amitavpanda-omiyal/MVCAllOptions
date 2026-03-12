using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Configuration;
using MVCAllOptions.AI.Data;
using OpenAI.Chat;

namespace MVCAllOptions.AI.Workflows.BookRecommendationWorkflow;

/// <summary>
/// Step 2 of 3: searches the catalogue and selects the best 2-3 matching books.
/// Input: StructuredPreferences → Output: BookShortlist
/// </summary>
internal sealed class BookFinderExecutor(IConfiguration config)
    : Executor<StructuredPreferences, BookShortlist>("book-finder")
{
    public override async ValueTask<BookShortlist> HandleAsync(StructuredPreferences prefs, IWorkflowContext context, CancellationToken ct = default)
    {
        Console.WriteLine($"[BookFinder] Searching catalogue for: genres={prefs.PreferredGenres}, budget={prefs.BudgetRange}");

        // ── Build candidate set from in-memory catalogue ──────────────────────
        var candidates = BookstoreData.Catalogue.AsEnumerable();

        // Apply genre filter if specific genres requested
        if (prefs.PreferredGenres != "any")
        {
            var genres = prefs.PreferredGenres.Split(',', StringSplitOptions.TrimEntries);
            candidates = candidates.Where(b =>
                genres.Any(g => b.Genre.Contains(g, StringComparison.OrdinalIgnoreCase)));
        }

        // Apply budget filter if provided
        if (prefs.BudgetRange != "any" && TryParseBudget(prefs.BudgetRange, out float min, out float max))
            candidates = candidates.Where(b => b.Price >= min && b.Price <= max);

        var candidateList = candidates.ToList();
        if (candidateList.Count == 0)
            candidateList = BookstoreData.Catalogue.ToList(); // fall back to full catalogue

        // Format candidate list for LLM selection
        var catalogueSummary = string.Join("\n", candidateList.Select(b =>
            $"- {b.Name} by {b.Author} | {b.Genre} | ${b.Price:F2} | {b.Description[..Math.Min(80, b.Description.Length)]}..."));

        var client = ChatClientFactory.Create(config);

        var prompt = $"""
            A reader has these preferences:
            - Preferred genres: {prefs.PreferredGenres}
            - Avoid genres: {prefs.AvoidedGenres}
            - Budget: {prefs.BudgetRange}
            - Reading level: {prefs.ReadingLevel}
            - Themes of interest: {prefs.Themes}
            
            From this catalogue:
            {catalogueSummary}
            
            Select the BEST 2-3 books that match the preferences.
            Return only the exact book titles, one per line, preceded by a dash.
            Then add one sentence explaining the selection rationale.
            """;

        var response = await client.CompleteChatAsync(
            [new UserChatMessage(prompt)], cancellationToken: ct);

        var text = response.Value.Content[0].Text;
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var titles = lines
            .Where(l => l.TrimStart().StartsWith("-") && !l.Contains("rationale", StringComparison.OrdinalIgnoreCase))
            .Select(l => l.TrimStart('-', ' ').Trim())
            .ToList();

        var rationale = lines.LastOrDefault(l => !l.TrimStart().StartsWith("-")) ?? text;

        Console.WriteLine($"[BookFinder] → Selected {titles.Count} books: {string.Join(", ", titles)}");

        return new BookShortlist(
            PreferencesSummary: $"Genres: {prefs.PreferredGenres}, Budget: {prefs.BudgetRange}, Level: {prefs.ReadingLevel}",
            SelectedTitles:     titles,
            SelectionRationale: rationale);
    }

    private static bool TryParseBudget(string budget, out float min, out float max)
    {
        min = 0; max = float.MaxValue;
        var clean = budget.Replace("$", "").Replace(" ", "");
        var parts = clean.Split('-');
        if (parts.Length == 2 && float.TryParse(parts[0], out min) && float.TryParse(parts[1], out max))
            return true;
        if (clean.StartsWith('>') && float.TryParse(clean.TrimStart('>'), out min))
        {
            max = float.MaxValue;
            return true;
        }
        if (clean.StartsWith('<') && float.TryParse(clean.TrimStart('<'), out max))
        {
            min = 0;
            return true;
        }
        if (float.TryParse(clean, out max))
        {
            min = 0;
            return true;
        }
        return false;
    }
}
