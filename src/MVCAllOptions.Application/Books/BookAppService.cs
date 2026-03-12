using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MVCAllOptions.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using System.Linq.Dynamic.Core;

namespace MVCAllOptions.Books;

[Authorize(MVCAllOptionsPermissions.Books.Default)]
public class BookAppService : ApplicationService, IBookAppService
{
    private readonly IRepository<Book, Guid> _repository;
    private readonly IDistributedEventBus _distributedEventBus;

    public BookAppService(
        IRepository<Book, Guid> repository,
        IDistributedEventBus distributedEventBus)
    {
        _repository          = repository;
        _distributedEventBus = distributedEventBus;
    }

    public async Task<BookDto> GetAsync(Guid id)
    {
        var book = await _repository.GetAsync(id);
        return ObjectMapper.Map<Book, BookDto>(book);
    }

    public async Task<PagedResultDto<BookDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var queryable = await _repository.GetQueryableAsync();
        var query = queryable
            .OrderBy(input.Sorting.IsNullOrWhiteSpace() ? "Name" : input.Sorting)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount);

        var books = await AsyncExecuter.ToListAsync(query);
        var totalCount = await AsyncExecuter.CountAsync(queryable);

        return new PagedResultDto<BookDto>(
            totalCount,
            ObjectMapper.Map<List<Book>, List<BookDto>>(books)
        );
    }

    [Authorize(MVCAllOptionsPermissions.Books.Create)]
    public async Task<BookDto> CreateAsync(CreateUpdateBookDto input)
    {
        var book = ObjectMapper.Map<CreateUpdateBookDto, Book>(input);
        await _repository.InsertAsync(book);

        // Publish a distributed event — BookCreatedEventHandler will pick it up
        // and trigger the MAF Book Enrichment Workflow in the AgentWorkflows service.
        // Using the default LocalDistributedEventBus (in-process) for the monolith;
        // swap to RabbitMQ / Azure Service Bus in a microservice deployment with
        // zero code changes here.
        await _distributedEventBus.PublishAsync(new BookCreatedEto
        {
            BookId      = book.Id,
            Name        = book.Name,
            Type        = book.Type.ToString(),
            Price       = book.Price,
            PublishDate = book.PublishDate.ToString("yyyy-MM-dd")
        });

        return ObjectMapper.Map<Book, BookDto>(book);
    }

    [Authorize(MVCAllOptionsPermissions.Books.Edit)]
    public async Task<BookDto> UpdateAsync(Guid id, CreateUpdateBookDto input)
    {
        var book = await _repository.GetAsync(id);
        ObjectMapper.Map(input, book);
        await _repository.UpdateAsync(book);
        return ObjectMapper.Map<Book, BookDto>(book);
    }

    [Authorize(MVCAllOptionsPermissions.Books.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
