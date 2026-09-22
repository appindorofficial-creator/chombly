using Microsoft.AspNetCore.Mvc;

namespace WebAppPet.Pages.Business.Explore;

public class ProfileModel : ExplorePageModel
{
    public IActionResult OnGet()
    {
        PrepareExplore("profile");
        return Page();
    }
}
