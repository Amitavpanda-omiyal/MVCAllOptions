using System;
using System.Threading.Tasks;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Hosting.OpenAI;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MVCAllOptions.AI.Agents;
using MVCAllOptions.AI.Workflows.BookEnrichmentWorkflow;
using MVCAllOptions.AI.Workflows.BookRecommendationWorkflow;
using MVCAllOptions.AI.Workflows.BookReviewWorkflow;
using Serilog;
using Serilog.Events;

namespace MVCAllOptions.Web;

public class Program
{
    public async static Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Async(c => c.File("Logs/logs.txt"))
            .WriteTo.Async(c => c.Console())
            .CreateBootstrapLogger();

        try
        {
            Log.Information("Starting web host.");
            var builder = WebApplication.CreateBuilder(args);
            builder.Host
                .AddAppSettingsSecretsJson()
                .UseAutofac()
                .UseSerilog((context, services, loggerConfiguration) =>
                {
                    loggerConfiguration
                        .ReadFrom.Configuration(context.Configuration)
                        .ReadFrom.Services(services)
                        .WriteTo.Async(c => c.AbpStudio(services));
                });

            // ── MAF: AI agents + workflows (DevUI omitted — incompatible with Autofac) ──
            var config = builder.Configuration;

            var catalogAgent     = BookCatalogAgentFactory.Create(config);
            var recommenderAgent = BookRecommenderAgentFactory.Create(config, catalogAgent);

            builder.AddAIAgent("BookCatalogAgent",     (_, _) => catalogAgent);
            builder.AddAIAgent("BookRecommenderAgent", (_, _) => recommenderAgent);
            builder.AddWorkflow("book-review-dispatcher", (_, _) => BookReviewWorkflowFactory.Create(config));
            builder.AddWorkflow("preference-analyzer",    (_, _) => BookRecommendationWorkflowFactory.Create(config));
            builder.AddWorkflow("book-enrichment-entry",  (_, _) => BookEnrichmentWorkflowFactory.Create(config));
            builder.Services.AddOpenAIResponses();
            builder.Services.AddOpenAIConversations();
            // ──────────────────────────────────────────────────────────────────

            await builder.AddApplicationAsync<MVCAllOptionsWebModule>();
            var app = builder.Build();
            await app.InitializeApplicationAsync();

            // ── MAF: OpenAI-compatible API endpoints ──────────────────────────
            //  POST /v1/responses       — streaming agent/workflow chat
            //  GET+POST /v1/conversations — conversation history
            app.MapOpenAIResponses();
            app.MapOpenAIConversations();
            // ──────────────────────────────────────────────────────────────────

            await app.RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly!");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
