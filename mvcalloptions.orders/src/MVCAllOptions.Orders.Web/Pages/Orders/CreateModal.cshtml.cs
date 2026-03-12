using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using MVCAllOptions.Integration;
using MVCAllOptions.Orders.Web.Pages;
using Volo.Abp.AspNetCore.Mvc.UI.Bootstrap.TagHelpers.Form;

namespace MVCAllOptions.Orders.Web.Pages.Orders;

public class CreateModalModel : OrdersPageModel
{
    [BindProperty]
    public CreateInput Order { get; set; } = new();

    private readonly IOrderAppService _orderAppService;
    private readonly IBookIntegrationService _bookIntegration;

    public CreateModalModel(IOrderAppService orderAppService, IBookIntegrationService bookIntegration)
    {
        _orderAppService = orderAppService;
        _bookIntegration = bookIntegration;
    }

    public async Task OnGetAsync()
    {
        var books = await _bookIntegration.GetAllBooksAsync();
        Order.Books = new List<SelectListItem> { new SelectListItem(L["SelectBook"].Value, "") };
        Order.Books.AddRange(books.Select(b => new SelectListItem(b.Name, b.Id.ToString())));
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await _orderAppService.CreateAsync(new OrderCreationDto
        {
            CustomerName = Order.CustomerName,
            BookId = Order.BookId
        });
        return NoContent();
    }

    public class CreateInput
    {
        [Required]
        [MaxLength(120)]
        public string CustomerName { get; set; } = null!;

        [Required]
        [SelectItems(nameof(Books))]
        public Guid BookId { get; set; }

        public List<SelectListItem> Books { get; set; } = new();
    }
}
