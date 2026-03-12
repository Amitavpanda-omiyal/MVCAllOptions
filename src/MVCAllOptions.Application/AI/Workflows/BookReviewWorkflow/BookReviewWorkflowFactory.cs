using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Configuration;

namespace MVCAllOptions.AI.Workflows.BookReviewWorkflow;

/// <summary>
/// Builds the fan-out Book Review Workflow.
///
/// Graph topology:
///
///                  ┌─→ ContentReviewExecutor    ─┐
///                  │                              │
///   Dispatcher ────┼─→ PricingAnalysisExecutor  ─┼──→ ReviewAggregatorExecutor ──→ [output]
///                  │                              │
///                  └─→ GenreClassificationExecutor┘
///
/// The three branch executors run in parallel (fan-out).
/// The aggregator collects all three results and calls YieldOutputAsync when all three arrive (fan-in).
/// </summary>
public static class BookReviewWorkflowFactory
{
    public static Workflow Create(IConfiguration config)
    {
        var dispatcher = new BookReviewDispatcher();
        var content    = new ContentReviewExecutor(config);
        var pricing    = new PricingAnalysisExecutor(config);
        var genre      = new GenreClassificationExecutor(config);
        var aggregator = new ReviewAggregatorExecutor(config);

        return new WorkflowBuilder(dispatcher)
            .AddEdge(dispatcher, content)
            .AddEdge(dispatcher, pricing)
            .AddEdge(dispatcher, genre)
            .AddEdge(content,    aggregator)
            .AddEdge(pricing,    aggregator)
            .AddEdge(genre,      aggregator)
            .WithOutputFrom(aggregator)
            .WithName("book-review-dispatcher")
            .Build();
    }
}
