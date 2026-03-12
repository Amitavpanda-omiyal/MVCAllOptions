using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace MVCAllOptions.AI.Workflows.BookRecommendationWorkflow;

/// <summary>
/// Step 1 of 3 in the sequential recommendation pipeline.
/// Parses free-text preferences into a structured form.
/// Input: string (free-text) → Output: StructuredPreferences
/// </summary>
internal sealed class PreferenceAnalyzerExecutor(IConfiguration config)
    : Executor<string, StructuredPreferences>("preference-analyzer")
{
    public override async ValueTask<StructuredPreferences> HandleAsync(string userText, IWorkflowContext context, CancellationToken ct = default)
    {
        Console.WriteLine($"[PreferenceAnalyzer] Parsing preferences: \"{userText}\"");

        var client = ChatClientFactory.Create(config);

        var prompt = $$"""
            Extract structured reading preferences from the following user input.
            Return ONLY valid JSON matching this schema:
            {
              "PreferredGenres": "comma-separated genres or 'any'",
              "AvoidedGenres": "comma-separated genres or 'none'",
              "BudgetRange": "e.g. '$10-$20' or 'any'",
              "ReadingLevel": "Easy | Moderate | Advanced | any",
              "Themes": "comma-separated themes or 'any'"
            }
            
            User input: "{userText}"
            
            Available genres: Adventure, Biography, Dystopia, Horror, Science, ScienceFiction
            """;

        var response = await client.CompleteChatAsync(
            [new UserChatMessage(prompt)], cancellationToken: ct);

        var json = response.Value.Content[0].Text
            .Replace("```json", "").Replace("```", "").Trim();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string Get(string key) =>
                root.TryGetProperty(key, out var el) ? el.GetString() ?? "any" : "any";

            var structured = new StructuredPreferences(
                PreferredGenres: Get("PreferredGenres"),
                AvoidedGenres:   Get("AvoidedGenres"),
                BudgetRange:     Get("BudgetRange"),
                ReadingLevel:    Get("ReadingLevel"),
                Themes:          Get("Themes"),
                OriginalText:    userText);

            Console.WriteLine($"[PreferenceAnalyzer] → Genres: {structured.PreferredGenres} | Budget: {structured.BudgetRange}");
            return structured;
        }
        catch
        {
            // Fallback: pass raw text through
            return new StructuredPreferences("any", "none", "any", "any", "any", userText);
        }
    }
}
