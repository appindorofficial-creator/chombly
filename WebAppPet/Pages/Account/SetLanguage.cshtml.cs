using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Localization;

namespace WebAppPet.Pages.Account;

public class SetLanguageModel : PageModel
{
    public IActionResult OnGet(string culture = "es", string? returnUrl = null)
    {
        CultureCookie.Set(Response, culture);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        var referer = Request.Headers.Referer.ToString();
        if (!string.IsNullOrEmpty(referer) && Uri.TryCreate(referer, UriKind.Absolute, out var uri)
            && string.Equals(uri.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase))
            return Redirect(referer);

        return RedirectToPage("/Index");
    }

    public IActionResult OnPost(string culture = "es", string? returnUrl = null)
        => OnGet(culture, returnUrl);
}
