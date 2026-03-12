using System.Collections.Generic;

namespace MVCAllOptions.AI.Workflows.BookRecommendationWorkflow;

// ── Message types flowing through the sequential pipeline ─────────────────────

/// <summary>Raw user preference string entering the workflow.</summary>
public record UserPreferences(string Text);

/// <summary>Structured preferences extracted from free-text by the first executor.</summary>
public record StructuredPreferences(
    string PreferredGenres,
    string AvoidedGenres,
    string BudgetRange,
    string ReadingLevel,
    string Themes,
    string OriginalText);

/// <summary>Curated shortlist of matching books selected by the second executor.</summary>
public record BookShortlist(
    string PreferencesSummary,
    IReadOnlyList<string> SelectedTitles,
    string SelectionRationale);

/// <summary>Final polished recommendation card emitted by the workflow.</summary>
public record RecommendationCard(
    string UserPreferenceSummary,
    IReadOnlyList<RecommendedBook> Recommendations,
    string ClosingNote);

public record RecommendedBook(
    string Title,
    string Author,
    string Genre,
    string Price,
    string WhyItFits);
