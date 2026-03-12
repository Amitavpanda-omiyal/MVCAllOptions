using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using WorkflowRouteBuilder = Microsoft.Agents.AI.Workflows.RouteBuilder;

namespace MVCAllOptions.AI.Workflows.BookReviewWorkflow;

/// <summary>
/// Aggregator executor: collects results from all 3 fan-out branches and produces the final report.
///
/// Key pattern: derive from Executor (base) and override ConfigureRoutes to register
/// multiple typed handlers — one per branch type. Uses IWorkflowContext.ReadStateAsync /
/// WriteStateAsync to accumulate partial results across invocations, then YieldOutputAsync
/// when all three branches have arrived.
/// </summary>
internal sealed class ReviewAggregatorExecutor(IConfiguration config) : Executor("review-aggregator")
{
    private const string ContentKey = "content";
    private const string PricingKey = "pricing";
    private const string GenreKey   = "genre";

    protected override WorkflowRouteBuilder ConfigureRoutes(WorkflowRouteBuilder routeBuilder) =>
        routeBuilder
            .AddHandler<ContentReviewResult>(     HandleContentAsync)
            .AddHandler<PricingAnalysisResult>(   HandlePricingAsync)
            .AddHandler<GenreClassificationResult>(HandleGenreAsync);

    private async ValueTask HandleContentAsync(ContentReviewResult result, IWorkflowContext ctx, CancellationToken ct)
    {
        Console.WriteLine($"  [Aggregator] Received content review for: {result.BookName}");
        await ctx.QueueStateUpdateAsync(ContentKey, result, cancellationToken: ct);
        await TryEmitAsync(ctx, ct);
    }

    private async ValueTask HandlePricingAsync(PricingAnalysisResult result, IWorkflowContext ctx, CancellationToken ct)
    {
        Console.WriteLine($"  [Aggregator] Received pricing analysis for: {result.BookName}");
        await ctx.QueueStateUpdateAsync(PricingKey, result, cancellationToken: ct);
        await TryEmitAsync(ctx, ct);
    }

    private async ValueTask HandleGenreAsync(GenreClassificationResult result, IWorkflowContext ctx, CancellationToken ct)
    {
        Console.WriteLine($"  [Aggregator] Received genre classification for: {result.BookName}");
        await ctx.QueueStateUpdateAsync(GenreKey, result, cancellationToken: ct);
        await TryEmitAsync(ctx, ct);
    }

    private async ValueTask TryEmitAsync(IWorkflowContext ctx, CancellationToken ct)
    {
        var content = await ctx.ReadStateAsync<ContentReviewResult>(ContentKey, cancellationToken: ct);
        var pricing = await ctx.ReadStateAsync<PricingAnalysisResult>(PricingKey, cancellationToken: ct);
        var genre   = await ctx.ReadStateAsync<GenreClassificationResult>(GenreKey, cancellationToken: ct);

        if (content is null || pricing is null || genre is null)
            return; // still waiting for remaining branches

        Console.WriteLine($"[Aggregator] All branches complete — synthesising verdict for: {content.BookName}");

        var verdict = await SynthesiseVerdictAsync(content, pricing, genre, ct);

        var report = new BookReviewReport(
            BookName:            content.BookName,
            ContentReview:       content,
            PricingAnalysis:     pricing,
            GenreClassification: genre,
            CombinedVerdict:     verdict);

        // YieldOutputAsync emits the result as a WorkflowOutputEvent
        await ctx.YieldOutputAsync(report, ct);
    }

    private async Task<string> SynthesiseVerdictAsync(
        ContentReviewResult content,
        PricingAnalysisResult pricing,
        GenreClassificationResult genre,
        CancellationToken ct)
    {
        var client = ChatClientFactory.Create(config);

        var prompt = $"""
            You have received three independent analyses for the book "{content.BookName}":
            
            CONTENT REVIEW:
            {content.LiteraryMerit}
            Readability: {content.ReadabilityScore}
            Target audience: {content.TargetAudience}
            
            PRICING ANALYSIS:
            {pricing.ValueForMoney}
            Category: {pricing.PriceCategory}
            Market: {pricing.MarketComparison}
            
            GENRE CLASSIFICATION:
            Primary genre: {genre.PrimaryGenre}
            Sub-genres: {genre.SubGenres}
            Themes: {genre.Themes}
            
            Write a single cohesive 2-3 sentence verdict that synthesises all three perspectives.
            """;

        var response = await client.CompleteChatAsync([new UserChatMessage(prompt)], cancellationToken: ct);
        return response.Value.Content[0].Text;
    }
}
