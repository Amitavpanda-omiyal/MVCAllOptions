using System;
using Volo.Abp.Application.Dtos;

namespace MVCAllOptions.Orders;

public class OrderDto : CreationAuditedEntityDto<Guid>
{
    public Guid BookId { get; set; }
    public string CustomerName { get; set; } = null!;
    public OrderState State { get; set; }
    public string BookName { get; set; } = string.Empty;
}
