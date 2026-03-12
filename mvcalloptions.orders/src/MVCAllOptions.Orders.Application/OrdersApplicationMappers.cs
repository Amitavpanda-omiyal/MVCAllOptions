using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace MVCAllOptions.Orders;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class OrderToOrderDtoMapper : MapperBase<Order, OrderDto>
{
    [MapperIgnoreTarget(nameof(OrderDto.BookName))]
    public override partial OrderDto Map(Order source);

    [MapperIgnoreTarget(nameof(OrderDto.BookName))]
    public override partial void Map(Order source, OrderDto destination);
}