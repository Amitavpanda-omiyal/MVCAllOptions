using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace MVCAllOptions.Orders.EntityFrameworkCore;

[ConnectionStringName(OrdersDbProperties.ConnectionStringName)]
public interface IOrdersDbContext : IEfCoreDbContext
{
    DbSet<Order> Orders { get; set; }
}
