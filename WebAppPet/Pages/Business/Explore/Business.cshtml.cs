using Microsoft.AspNetCore.Mvc;

namespace WebAppPet.Pages.Business.Explore;

public class BusinessModel : ExplorePageModel
{
    public IActionResult OnGet()
    {
        PrepareExplore("business");
        return Page();
    }
}
