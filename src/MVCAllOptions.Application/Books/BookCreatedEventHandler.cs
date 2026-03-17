using System;
using System.ClientModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using MVCAllOptions.AI;
using MVCAllOptions.AI.Workflows.BookVerificationWorkflow;
using OpenAI;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.AIManagement.Workspaces.Configuration;

namespace MVCAllOptions.Books;

/// <summary>
/// Handles the <see cref="BookCreatedEto"/> distributed event and triggers
/// the MAF Book Verification Workflow in-process.
///
/// Fire-and-forget: book creation is never blocked by the AI pipeline.
/// The verification result is stored in the distributed cache so that
/// <see cref="MVCAllOptions.AI.BookContextChatClient"/> can surface it in the
/// Chat Playground on the OpenAIRAGWorkspace page.
/// </summary>
public class BookCreatedEventHandler :
    IDistributedEventHandler<BookCreatedEto>,
    ITransientDependency
{
    private readonly IWorkspaceConfigurationStore _configStore;
    private readonly IDistributedCache<BookVerificationCacheItem> _verificationCache;
    private readonly ILogger<BookCreatedEventHandler> _logger;

    public BookCreatedEventHandler(
        IWorkspaceConfigurationStore configStore,
        IDistributedCache<BookVerificationCacheItem> verificationCache,
        ILogger<BookCreatedEventHandler> logger)
    {
        _configStore       = configStore;
        _verificationCache = verificationCache;
        _logger            = logger;
    }

    public async Task HandleEventAsync(BookCreatedEto eventData)
    {
        var bookName = eventData.Name;

        // Resolve workspace config on the request thread (scoped services available here)
        // so we can safely pass plain values into the background Task.
        var config = await _configStore.GetOrNullAsync("OpenAIRAGWorkspace");
        if (config is null)
        {
            _logger.LogWarning(
                "[BookVerification] OpenAIRAGWorkspace config not found — skipping verification for \"{BookName}\"",
                bookName);
            return;
        }

        var apiKey  = config.ApiKey    ?? string.Empty;
        var baseUrl = config.ApiBaseUrl ?? "https://api.openai.com/v1";
        var model   = config.ModelName  ?? "gpt-4o-mini";

        // Fire-and-forget: return immediately so book creation is not blocked.
        _ = Task.Run(async () =>
        {
            _logger.LogInformation(
                "[MAF Workflow] ══════════════════════════════════════════════");
            _logger.LogInformation(
                "[MAF Workflow] ► BookVerificationWorkflow STARTED for '{BookName}'", bookName);
            _logger.LogInformation(
                "[MAF Workflow] Pipeline: BookExistenceVerifier → BookSuggestion");
            _logger.LogInformation(
                "[MAF Workflow] Model: {Model} | Workspace: OpenAIRAGWorkspace", model);
            _logger.LogInformation(
                "[MAF Workflow] ══════════════════════════════════════════════");
            try
            {
                var chatClient = new OpenAIClient(
                        new ApiKeyCredential(apiKey),
                        new OpenAIClientOptions { Endpoint = new Uri(baseUrl) })
                    .GetChatClient(model);

                var workflow = BookVerificationWorkflowFactory.Create(chatClient, _logger);

                await using var run = await InProcessExecution.RunAsync<string>(
                    workflow, bookName, Guid.NewGuid().ToString());

                var outputEvent = run.OutgoingEvents
                    .OfType<WorkflowOutputEvent>()
                    .FirstOrDefault(e => e.Is<BookVerificationFinalResult>());

                if (outputEvent is null || !outputEvent.Is<BookVerificationFinalResult>(out var result))
                {
                    _logger.LogWarning(
                        "[BookVerification] No output event captured for \"{BookName}\"", bookName);
                    return;
                }

                _logger.LogInformation(
                    "[BookVerification] Result for \"{BookName}\": {Summary}", bookName, result!.Summary);

                // Cache for 7 days — BookContextChatClient reads this on every chat request
                var cacheItem = new BookVerificationCacheItem
                {
                    BookName              = result.BookName,
                    ExistsInRealWorld    = result.ExistsInRealWorld,
                    PublisherName        = result.PublisherName,
                    PublicationDate      = result.PublicationDate,
                    SuggestedAlternatives = result.SuggestedAlternatives,
                    Summary              = result.Summary,
                    VerifiedAt           = DateTime.UtcNow,
                };

                await _verificationCache.SetAsync(
                    key: $"book:{bookName}",
                    value: cacheItem,
                    options: new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7),
                    });

                _logger.LogInformation(
                    "[MAF Workflow] ══════════════════════════════════════════════");
                _logger.LogInformation(
                    "[MAF Workflow] ✔ BookVerificationWorkflow COMPLETED for '{BookName}'", bookName);
                _logger.LogInformation(
                    "[MAF Workflow] Result cached for 7 days under key 'book:{BookName}'", bookName);
                _logger.LogInformation(
                    "[MAF Workflow] ══════════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[BookVerification] Workflow failed for \"{BookName}\"", bookName);
            }
        });
    }
}

