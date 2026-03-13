using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace MVCAllOptions.Orders;

public class Order : CreationAuditedAggregateRoot<Guid>
{
    public Guid BookId { get; set; }
    public string CustomerName { get; set; } = null!;
    public OrderState State { get; set; }
}
