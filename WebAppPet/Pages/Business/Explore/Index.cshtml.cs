using Microsoft.AspNetCore.Mvc;

namespace WebAppPet.Pages.Business.Explore;

public class IndexModel : ExplorePageModel
{
    public IActionResult OnGet()
    {
        PrepareExplore("dashboard");
        return Page();
    }
}
