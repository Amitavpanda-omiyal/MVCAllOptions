using System;
using System.Linq;
using System.Threading.Tasks;
using MVCAllOptions.Books;
using MVCAllOptions.Orders;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MVCAllOptions.Reports;

public class OrderReportAppService : MVCAllOptionsAppService, IOrderReportAppService
{
    private readonly IRepository<Order, Guid> _orderRepo;
    private readonly IRepository<Book, Guid> _bookRepo;

    public OrderReportAppService(
        IRepository<Order, Guid> orderRepo,
        IRepository<Book, Guid> bookRepo)
    {
        _orderRepo = orderRepo;
        _bookRepo = bookRepo;
    }

    public async Task<ListResultDto<OrderReportDto>> GetLatestOrdersAsync()
    {
        var orders = await _orderRepo.GetQueryableAsync();
        var books  = await _bookRepo.GetQueryableAsync();

        var items = (from o in orders
                     join b in books on o.BookId equals b.Id
                     orderby o.CreationTime descending
                     select new OrderReportDto
                     {
                         OrderId      = o.Id,
                         CustomerName = o.CustomerName,
                         State        = o.State,
                         CreationTime = o.CreationTime,
                         BookId       = b.Id,
                         BookName     = b.Name
                     })
                    .Take(50)
                    .ToList();

        return new ListResultDto<OrderReportDto>(items);
    }
}
