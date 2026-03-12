using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MVCAllOptions.Integration;
using MVCAllOptions.Orders.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using System.Linq.Dynamic.Core;

namespace MVCAllOptions.Orders;

[Authorize(OrdersPermissions.Orders.Default)]
public class OrderAppService : OrdersAppService, IOrderAppService
{
    private readonly IRepository<Order, Guid> _repository;
    private readonly OrderToOrderDtoMapper _mapper;
    private readonly IBookIntegrationService _bookIntegration;

    public OrderAppService(
        IRepository<Order, Guid> repository,
        OrderToOrderDtoMapper mapper,
        IBookIntegrationService bookIntegration)
    {
        _repository = repository;
        _mapper = mapper;
        _bookIntegration = bookIntegration;
    }

    private static readonly HashSet<string> ValidSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(Order.CustomerName), "CustomerName asc", "CustomerName desc",
        nameof(Order.State), "State asc", "State desc",
        nameof(Order.CreationTime), "CreationTime asc", "CreationTime desc"
    };

    public async Task<PagedResultDto<OrderDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var queryable = await _repository.GetQueryableAsync();

        var sorting = (!input.Sorting.IsNullOrWhiteSpace() && ValidSortFields.Contains(input.Sorting))
            ? input.Sorting
            : nameof(Order.CustomerName);

        var query = queryable
            .OrderBy(sorting)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount);

        var orders = await AsyncExecuter.ToListAsync(query);
        var totalCount = await AsyncExecuter.CountAsync(queryable);

        var dtos = orders.Select(_mapper.Map).ToList();

        var bookIds = orders.Select(o => o.BookId).Distinct().ToList();
        var books = (await _bookIntegration.GetBooksByIdsAsync(bookIds))
            .ToDictionary(b => b.Id, b => b.Name);

        dtos.ForEach(dto => dto.BookName = books.GetValueOrDefault(dto.BookId, string.Empty));

        return new PagedResultDto<OrderDto>(totalCount, dtos);
    }

    [Authorize(OrdersPermissions.Orders.Create)]
    public async Task<OrderDto> CreateAsync(OrderCreationDto input)
    {
        var order = new Order(GuidGenerator.Create(), input.BookId, input.CustomerName);

        await _repository.InsertAsync(order, autoSave: true);
        return _mapper.Map(order);
    }
}

