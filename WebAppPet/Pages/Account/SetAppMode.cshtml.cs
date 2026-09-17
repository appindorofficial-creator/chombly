using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Services;

namespace WebAppPet.Pages.Account;

public class SetAppModeModel : PageModel
{
    private readonly AuthService _auth;

    public SetAppModeModel(AuthService auth) => _auth = auth;

    public IActionResult OnGet(string mode = AppShellMode.Owner, string? returnUrl = null)
    {
        if (!_auth.IsAuthenticated)
            return RedirectToPage("/Account/Login", new { returnUrl = "/Account/Profile" });

        if (!_auth.IsGroomer)
            return RedirectToPage("/Account/Profile");

        var shell = AppShellMode.Normalize(mode);
        _auth.SetShellMode(shell);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return shell == AppShellMode.Business
            ? RedirectToPage("/Groomer/Dashboard")
            : RedirectToPage("/Index");
    }
}
