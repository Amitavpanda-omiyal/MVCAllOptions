using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
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

    public OrderAppService(
        IRepository<Order, Guid> repository,
        OrderToOrderDtoMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<PagedResultDto<OrderDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var queryable = await _repository.GetQueryableAsync();

        var query = queryable
            .OrderBy(input.Sorting.IsNullOrWhiteSpace() ? nameof(Order.CustomerName) : input.Sorting)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount);

        var orders = await AsyncExecuter.ToListAsync(query);
        var totalCount = await AsyncExecuter.CountAsync(queryable);

        return new PagedResultDto<OrderDto>(
            totalCount,
            orders.Select(_mapper.Map).ToList()
        );
    }

    [Authorize(OrdersPermissions.Orders.Create)]
    public async Task<OrderDto> CreateAsync(OrderCreationDto input)
    {
        var order = new Order(GuidGenerator.Create(), input.BookId, input.CustomerName);

        await _repository.InsertAsync(order, autoSave: true);
        return _mapper.Map(order);
    }
}
