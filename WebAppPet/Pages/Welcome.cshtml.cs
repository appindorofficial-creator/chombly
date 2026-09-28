using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Infrastructure.Web;

namespace WebAppPet.Pages;

public class WelcomeModel : PageModel
{
    public void OnGet()
    {
        BusinessExploreMode.Enable(Response);
    }
}
