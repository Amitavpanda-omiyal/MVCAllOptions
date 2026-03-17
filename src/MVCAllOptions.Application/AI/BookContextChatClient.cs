using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using MVCAllOptions.Books;
using Volo.Abp.Caching;
using Volo.Abp.Domain.Repositories;

namespace MVCAllOptions.AI;

/// <summary>
/// Wraps an inner <see cref="IChatClient"/> and prepends book catalogue data as a system message.
/// Used by the OpenAIRAGWorkspace so the built-in AI Management Chat Playground
/// is automatically aware of books from the database and their real-world verification status.
/// </summary>
public class BookContextChatClient : DelegatingChatClient
{
    private readonly IServiceScopeFactory _scopeFactory;

    public BookContextChatClient(IChatClient innerClient, IServiceScopeFactory scopeFactory)
        : base(innerClient)
    {
        _scopeFactory = scopeFactory;
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var enriched = await PrependBookContextAsync(messages);
        return await base.GetResponseAsync(enriched, options, cancellationToken);
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var enriched = await PrependBookContextAsync(messages);
        await foreach (var update in base.GetStreamingResponseAsync(enriched, options, cancellationToken))
        {
            yield return update;
        }
    }

    private async Task<List<ChatMessage>> PrependBookContextAsync(IEnumerable<ChatMessage> messages)
    {
        using var scope = _scopeFactory.CreateScope();
        var bookRepo = scope.ServiceProvider.GetRequiredService<IRepository<Book, Guid>>();
        var books = await bookRepo.GetListAsync();

        var catalogue = string.Join("\n", books.Select(b =>
            $"- {b.Name} | Genre: {b.Type} | Price: ${b.Price:F2} | Published: {b.PublishDate:yyyy-MM-dd}"));

        // Load verification results from distributed cache
        var verificationCache = scope.ServiceProvider
            .GetRequiredService<IDistributedCache<BookVerificationCacheItem>>();

        var verificationSection = new StringBuilder();
        foreach (var book in books)
        {
            var cacheItem = await verificationCache.GetAsync($"book:{book.Name}");
            if (cacheItem is not null)
            {
                verificationSection.AppendLine($"  - {cacheItem.Summary}");
            }
        }

        var verificationText = verificationSection.Length > 0
            ? $"\n\nRECENT BOOK VERIFICATIONS (from background AI check):\n{verificationSection}"
            : string.Empty;

        var systemMessage = new ChatMessage(ChatRole.System, $"""
            You are a knowledgeable bookstore assistant for MVCAllOptions Bookstore.
            You have access to the complete book catalogue below.
            Answer questions about books — titles, genres, prices, publication years.
            Help customers find books matching their interests.
            Be concise, friendly, and helpful.

            Current catalogue:
            {catalogue}{verificationText}
            """);

        var result = new List<ChatMessage> { systemMessage };
        result.AddRange(messages);
        return result;
    }
}
