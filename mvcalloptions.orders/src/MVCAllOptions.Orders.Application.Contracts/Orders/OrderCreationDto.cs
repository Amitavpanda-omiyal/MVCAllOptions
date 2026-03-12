using System;
using System.ComponentModel.DataAnnotations;

namespace MVCAllOptions.Orders;

public class OrderCreationDto
{
    [Required]
    [MaxLength(120)]
    public string CustomerName { get; set; } = null!;

    [Required]
    public Guid BookId { get; set; }
}
