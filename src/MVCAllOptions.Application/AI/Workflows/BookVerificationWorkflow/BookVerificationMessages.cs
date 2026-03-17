namespace MVCAllOptions.AI.Workflows.BookVerificationWorkflow;

/// <summary>
/// Intermediate result produced by <see cref="BookExistenceVerifierExecutor"/>
/// and consumed by <see cref="BookSuggestionExecutor"/>.
/// </summary>
internal record BookVerificationResult(
    string BookName,
    bool ExistsInRealWorld,
    string PublisherName,
    string PublicationDate,
    string RawResponseText);

/// <summary>
/// Final result emitted by the workflow — contains verification data and,
/// when the book was not found, real-world alternative suggestions.
/// </summary>
internal record BookVerificationFinalResult(
    string BookName,
    bool ExistsInRealWorld,
    string PublisherName,
    string PublicationDate,
    string SuggestedAlternatives,
    string Summary);
