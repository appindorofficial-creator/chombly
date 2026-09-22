using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Services;

namespace WebAppPet.Pages;

public class WelcomeModel : PageModel
{
    public void OnGet()
    {
        BusinessExploreMode.Enable(Response);
    }
}
