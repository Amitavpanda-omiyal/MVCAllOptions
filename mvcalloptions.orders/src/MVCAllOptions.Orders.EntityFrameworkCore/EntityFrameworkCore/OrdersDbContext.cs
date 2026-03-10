using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace MVCAllOptions.Orders.EntityFrameworkCore;

[ConnectionStringName(OrdersDbProperties.ConnectionStringName)]
public class OrdersDbContext : AbpDbContext<OrdersDbContext>, IOrdersDbContext
{
    public DbSet<Order> Orders { get; set; }

    public OrdersDbContext(DbContextOptions<OrdersDbContext> options)
        : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ConfigureOrders();
    }
}
