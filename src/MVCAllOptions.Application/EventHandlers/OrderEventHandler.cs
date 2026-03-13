using System;
using System.Threading.Tasks;
using MVCAllOptions.Books;
using MVCAllOptions.Orders.Events;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;

namespace MVCAllOptions.EventHandlers;

// Books module listens here: when an order is placed, reduce the book's stock count
public class OrderEventHandler :
    IDistributedEventHandler<OrderPlacedEto>,
    ITransientDependency
{
    private readonly IRepository<Book, Guid> _bookRepository;

    public OrderEventHandler(IRepository<Book, Guid> bookRepository)
    {
        _bookRepository = bookRepository;
    }

    public async Task HandleEventAsync(OrderPlacedEto eventData)
    {
        var book = await _bookRepository.FindAsync(eventData.BookId);
        if (book == null)
        {
            return;
        }

        book.StockCount--;
        await _bookRepository.UpdateAsync(book);
    }
}
