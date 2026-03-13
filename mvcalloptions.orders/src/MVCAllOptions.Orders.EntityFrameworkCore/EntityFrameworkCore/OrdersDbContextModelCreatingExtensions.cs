using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace MVCAllOptions.Orders.EntityFrameworkCore;

public static class OrdersDbContextModelCreatingExtensions
{
    public static void ConfigureOrders(
        this ModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        builder.Entity<Order>(b =>
        {
            b.ToTable(OrdersDbProperties.DbTablePrefix + "Orders", OrdersDbProperties.DbSchema);
            b.ConfigureByConvention();
            b.Property(q => q.CustomerName).IsRequired().HasMaxLength(120);
        });
    }
}
