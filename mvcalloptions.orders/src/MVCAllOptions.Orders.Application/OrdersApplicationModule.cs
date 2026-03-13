using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;
using Volo.Abp.Application;

namespace MVCAllOptions.Orders;

[DependsOn(
    typeof(OrdersDomainModule),
    typeof(OrdersApplicationContractsModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpMapperlyModule)
    )]
public class OrdersApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMapperlyObjectMapper<OrdersApplicationModule>();
        context.Services.AddSingleton<OrderToOrderDtoMapper>();
    }
}
