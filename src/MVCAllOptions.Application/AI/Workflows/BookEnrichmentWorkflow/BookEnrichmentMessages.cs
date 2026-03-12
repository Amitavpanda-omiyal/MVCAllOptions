namespace MVCAllOptions.AI.Workflows.BookEnrichmentWorkflow;

// ── Message types flowing through the book-enrichment pipeline ────────────────

/// <summary>
/// Input fired when a book is created in the ABP bookstore.
/// </summary>
public record BookCreatedInput(
    string Name,
    string Type,
    float  Price,
    string PublishDate);

/// <summary>
/// Intermediate message: book data + AI-generated description.
/// Produced by Step 1 (BookDescriptionExecutor), consumed by Step 2.
/// </summary>
public record BookWithDescription(
    string Name,
    string Type,
    float  Price,
    string PublishDate,
    string Description);

/// <summary>
/// Final output emitted by the workflow.
/// Contains everything the ABP app needs to show the user the AI enrichment.
/// </summary>
public record BookEnrichmentResult(
    string BookName,
    string Description,
    string TargetAudience,
    string KeyThemes,
    string MarketingBlurb);
