using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Services;

namespace WebAppPet.Pages.Business.Explore;

/// <summary>Leaves business preview and restores the family app shell.</summary>
public class ExitModel : PageModel
{
    public IActionResult OnGet(string? returnUrl = null)
    {
        BusinessExploreMode.Clear(Response);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            && !returnUrl.StartsWith("/Business/Explore", StringComparison.OrdinalIgnoreCase))
            return LocalRedirect(returnUrl);

        if (User.Identity?.IsAuthenticated == true)
            return RedirectToPage("/Index");

        // Guests leave the tour into browse home (not Welcome, which would re-arm the tour).
        return RedirectToPage("/Index", new { browse = true });
    }
}
