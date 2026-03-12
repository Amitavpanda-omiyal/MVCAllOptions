using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MVCAllOptions.AI.Workflows.BookEnrichmentWorkflow;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using MEChatMessage = Microsoft.Extensions.AI.ChatMessage;
using MEChatRole    = Microsoft.Extensions.AI.ChatRole;

namespace MVCAllOptions.Books;

/// <summary>
/// Handles the <see cref="BookCreatedEto"/> distributed event and triggers
/// the MAF Book Enrichment Workflow in-process.
///
/// Fire-and-forget: book creation is never blocked by the AI pipeline.
/// Results are written to the application logger (visible in console / logs.txt / ABP Studio).
/// </summary>
public class BookCreatedEventHandler :
    IDistributedEventHandler<BookCreatedEto>,
    ITransientDependency
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<BookCreatedEventHandler> _logger;

    public BookCreatedEventHandler(
        IConfiguration configuration,
        ILogger<BookCreatedEventHandler> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task HandleEventAsync(BookCreatedEto eventData)
    {
        // Serialize the event data as JSON — the BookDataExtractor agent
        // accepts both JSON and natural language; JSON is the fast path.
        var input = JsonSerializer.Serialize(new BookCreatedInput(
            Name:        eventData.Name,
            Type:        eventData.Type,
            Price:       eventData.Price,
            PublishDate: eventData.PublishDate));

        var bookName = eventData.Name;

        // Fire-and-forget: return immediately so book creation is not blocked.
        _ = Task.Run(async () =>
        {
            _logger.LogInformation("[MAF] ═══ BookEnrichmentWorkflow starting for \"{BookName}\" ═══", bookName);
            try
            {
                var workflow = BookEnrichmentWorkflowFactory.Create(_configuration);

                // RunAsync<TInput> — TInput is the input type (string JSON for this workflow).
                // The workflow routes the string through 3 sequential AIAgents.
                await using var run = await InProcessExecution.RunAsync<string>(
                    workflow, input, Guid.NewGuid().ToString());

                // Collect all outgoing events and log them for visibility.
                var events = run.OutgoingEvents.ToList();
                _logger.LogInformation("[MAF] ─── Workflow finished with {Count} outgoing events ───", events.Count);

                // Log every event so we can diagnose failures or capture output
                foreach (var evt in events)
                {
                    var evtType = evt.GetType().Name;
                    var evtStr  = evt.ToString() ?? "";

                    if (evtType.Contains("Error") || evtType.Contains("Failed"))
                        _logger.LogError("[MAF] ✗ {EventType}: {Detail}", evtType, evtStr);
                    else if (evtType == "WorkflowOutputEvent")
                        _logger.LogInformation("[MAF] ✓ OUTPUT: {Detail}", evtStr);
                    else
                        _logger.LogDebug("[MAF] {EventType}: {Detail}", evtType, evtStr);
                }

                var replied = false;
                foreach (var evt in events)
                {
                    // WorkflowOutputEvent carries the final output — log it directly.
                    if (evt is WorkflowOutputEvent outputEvent)
                    {
                        if (outputEvent.Is<string>(out var text) && !string.IsNullOrWhiteSpace(text))
                        {
                            _logger.LogInformation("[MAF] Workflow output:\n{Text}", text);
                            replied = true;
                        }
                        else if (outputEvent.Is<List<MEChatMessage>>(out var msgs) && msgs is { Count: > 0 })
                        {
                            var assistants = msgs.Where(m => m.Role == MEChatRole.Assistant).ToList();
                            for (int i = 0; i < assistants.Count; i++)
                            {
                                var msgText = string.Join("", assistants[i].Contents.Select(c => c.ToString())).Trim();
                                _logger.LogInformation("[MAF] Agent reply #{Index}:\n{Text}", i + 1, msgText);
                            }
                            replied = true;
                        }
                    }
                }

                if (!replied)
                {
                    _logger.LogWarning(
                        "[MAF] ⚠ No output captured for \"{BookName}\" from {Count} events. " +
                        "Event types: {Types}",
                        bookName,
                        events.Count,
                        string.Join(", ", events.Select(e => e.GetType().Name).Distinct()));
                }

                _logger.LogInformation("[MAF] ═══ BookEnrichmentWorkflow complete for \"{BookName}\" ═══", bookName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MAF] BookEnrichmentWorkflow failed for \"{BookName}\"", bookName);
            }
        });

        return Task.CompletedTask;
    }
}

