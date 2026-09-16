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
        if (TryLocalPath(ReturnUrl, out var fromQuery))
        {
            BackHref = fromQuery;
            return;
        }

        var referer = Request.Headers.Referer.ToString();
        if (Uri.TryCreate(referer, UriKind.Absolute, out var uri)
            && string.Equals(uri.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase)
            && !uri.AbsolutePath.Contains("/Legal/Terms", StringComparison.OrdinalIgnoreCase)
            && TryLocalPath(uri.PathAndQuery, out var fromReferer))
        {
            BackHref = fromReferer;
            return;
        }

        BackHref = Url.Page("/Index") ?? "/Index";
    }

    private bool TryLocalPath(string? candidate, out string path)
    {
        path = "/Index";
        if (string.IsNullOrWhiteSpace(candidate)) return false;

        var value = candidate.Trim();
        // Allow root-relative paths only (block //evil.com and javascript:).
        if (!value.StartsWith('/') || value.StartsWith("//", StringComparison.Ordinal))
            return false;
        if (!Url.IsLocalUrl(value))
            return false;

        // Never bounce back into Terms (e.g. hash-only navigations / self-referer).
        if (value.Contains("/Legal/Terms", StringComparison.OrdinalIgnoreCase))
            return false;

        path = value;
        return true;
    }
}
