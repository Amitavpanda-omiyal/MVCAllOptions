using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MVCAllOptions.Books;
using Volo.Abp.Domain.Repositories;

namespace MVCAllOptions.Integration;

public class BookIntegrationService : MVCAllOptionsAppService, IBookIntegrationService
{
    private readonly IRepository<Book, Guid> _repository;

    public BookIntegrationService(IRepository<Book, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<List<BookDto>> GetBooksByIdsAsync(List<Guid> ids)
    {
        var books = await _repository.GetListAsync(b => ids.Contains(b.Id));
        return ObjectMapper.Map<List<Book>, List<BookDto>>(books);
    }

    public async Task<List<BookDto>> GetAllBooksAsync()
    {
        var books = await _repository.GetListAsync();
        return ObjectMapper.Map<List<Book>, List<BookDto>>(books);
    }
}
