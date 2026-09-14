using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WebAppPet.Pages.Legal;

public class TermsModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string BackHref { get; private set; } = "/Index";

    public void OnGet()
    {
        if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
        {
            BackHref = ReturnUrl;
            return;
        }

        var referer = Request.Headers.Referer.ToString();
        if (Uri.TryCreate(referer, UriKind.Absolute, out var uri)
            && string.Equals(uri.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase)
            && !uri.AbsolutePath.Contains("/Legal/Terms", StringComparison.OrdinalIgnoreCase)
            && Url.IsLocalUrl(uri.PathAndQuery))
        {
            BackHref = uri.PathAndQuery;
            return;
        }

        BackHref = Url.Page("/Index") ?? "/Index";
    }
}
