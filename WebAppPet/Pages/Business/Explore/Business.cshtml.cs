using Microsoft.AspNetCore.Mvc;
using WebAppPet.Services;

namespace WebAppPet.Pages.Business.Explore;

public class BusinessModel : ExplorePageModel
{
    public IActionResult OnGet()
    {
        PrepareExplore("business");
        return Page();
    }
}
