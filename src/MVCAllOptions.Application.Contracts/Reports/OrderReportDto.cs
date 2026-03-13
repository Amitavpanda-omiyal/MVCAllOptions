using System;
using MVCAllOptions.Orders;

namespace MVCAllOptions.Reports;

public class OrderReportDto
{
    public Guid OrderId { get; set; }
    public string CustomerName { get; set; } = null!;
    public OrderState State { get; set; }
    public DateTime CreationTime { get; set; }
    public Guid BookId { get; set; }
    public string BookName { get; set; } = null!;
}
