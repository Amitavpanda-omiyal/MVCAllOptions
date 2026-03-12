namespace MVCAllOptions.AI.Workflows.BookReviewWorkflow;

// ── Shared message types used across the fan-out workflow ─────────────────────

/// <summary>Book name routed to every branch of the fan-out.</summary>
public record BookNameMessage(string BookName);

/// <summary>Result from the content-quality review branch.</summary>
public record ContentReviewResult(
    string BookName,
    string LiteraryMerit,
    string ReadabilityScore,
    string TargetAudience,
    string Summary);

/// <summary>Result from the pricing & value analysis branch.</summary>
public record PricingAnalysisResult(
    string BookName,
    string ValueForMoney,
    string PriceCategory,
    string MarketComparison,
    string Summary);

/// <summary>Result from the genre / classification branch.</summary>
public record GenreClassificationResult(
    string BookName,
    string PrimaryGenre,
    string SubGenres,
    string Themes,
    string Summary);

/// <summary>Final aggregated report sent out of the workflow.</summary>
public record BookReviewReport(
    string BookName,
    ContentReviewResult         ContentReview,
    PricingAnalysisResult       PricingAnalysis,
    GenreClassificationResult   GenreClassification,
    string                      CombinedVerdict);
