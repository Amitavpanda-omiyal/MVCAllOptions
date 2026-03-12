using System.Threading.Tasks;
using MVCAllOptions.Books;
using Microsoft.AspNetCore.Mvc;

namespace MVCAllOptions.Web.Pages.Books
{
    public class CreateModalModel : MVCAllOptionsPageModel
    {
        [BindProperty]
        public CreateUpdateBookDto Book { get; set; }

        private readonly IBookAppService _bookAppService;

        public CreateModalModel(IBookAppService bookAppService)
        {
            _bookAppService = bookAppService;
        }

        public void OnGet()
        {
            Book = new CreateUpdateBookDto();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // The BookAppService.CreateAsync fires the MAF enrichment workflow
            // internally — no Web-layer coordination needed.
            await _bookAppService.CreateAsync(Book);
            return NoContent();
        }
    }
}