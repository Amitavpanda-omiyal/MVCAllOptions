using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Configuration;

namespace MVCAllOptions.AI.Workflows.BookRecommendationWorkflow;

/// <summary>
/// Builds the sequential Book Recommendation Workflow.
///
/// Pipeline:
///   [string] → PreferenceAnalyzer → BookFinder → RecommendationFormatter → [RecommendationCard]
///
/// Each executor's output becomes the next executor's input (linear chain).
/// </summary>
public static class BookRecommendationWorkflowFactory
{
    public static Workflow Create(IConfiguration config)
    {
        var preferenceAnalyzer      = new PreferenceAnalyzerExecutor(config);
        var bookFinder              = new BookFinderExecutor(config);
        var recommendationFormatter = new RecommendationFormatterExecutor(config);

        return new WorkflowBuilder(preferenceAnalyzer)
            .AddEdge(preferenceAnalyzer,      bookFinder)
            .AddEdge(bookFinder,              recommendationFormatter)
            .WithOutputFrom(recommendationFormatter)
            .WithName("preference-analyzer")
            .Build();
    }
}
