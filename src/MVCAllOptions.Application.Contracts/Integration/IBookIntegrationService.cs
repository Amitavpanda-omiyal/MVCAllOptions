using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MVCAllOptions.Books;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace MVCAllOptions.Integration;

[IntegrationService]
public interface IBookIntegrationService : IApplicationService
{
    Task<List<BookDto>> GetBooksByIdsAsync(List<Guid> ids);
    Task<List<BookDto>> GetAllBooksAsync();
}
