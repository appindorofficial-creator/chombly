using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using WebAppPet.Application.Accounts.Login;
using WebAppPet.Localization;
using WebAppPet.Services;

namespace WebAppPet.Pages.Account;

public class LoginModel : PageModel
{
    private readonly LoginHandler _login;
    private readonly AuthService _auth;
    private readonly IStringLocalizer<SharedResource> _L;

    public LoginModel(LoginHandler login, AuthService auth, IStringLocalizer<SharedResource> L)
    {
        _login = login;
        _auth = auth;
        _L = L;
    }

    [BindProperty, EmailAddress, Required]
    public string Email { get; set; } = string.Empty;

    [BindProperty, Required]
    public string Password { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty]
    public string Culture { get; set; } = "es";

    public string? ErrorMessage { get; set; }
    public string BackPage { get; set; } = "/Index";

    public IActionResult OnGet()
    {
        if (_auth.IsAuthenticated)
        {
            var dest = SafeLocalUrl(ReturnUrl);
            if (dest != null)
                return LocalRedirect(dest);
            return RedirectToPage("/Index");
        }

        BackPage = SafeLocalUrl(ReturnUrl) ?? "/Index";
        var feature = HttpContext.Features.Get<IRequestCultureFeature>();
        Culture = CultureCookie.Normalize(feature?.RequestCulture.UICulture.TwoLetterISOLanguageName);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        BackPage = SafeLocalUrl(ReturnUrl) ?? "/Index";
        Culture = CultureCookie.Normalize(Culture);
        CultureCookie.Set(Response, Culture);

        var result = await _login.HandleAsync(new LoginCommand(Email, Password, Culture));
        if (result.User is not { } user)
        {
            ErrorMessage = _L["Login_Error"].Value;
            return Page();
        }

        await _auth.SignInAsync(user);

        var dest = SafeLocalUrl(ReturnUrl);
        if (dest != null)
            return LocalRedirect(dest);

        if (user.Role == Models.UserRole.Admin)
            return RedirectToPage("/Admin/Approvals");

        if (user.Role == Models.UserRole.Groomer && _auth.IsBusinessShell)
            return RedirectToPage("/Groomer/Dashboard");

        return RedirectToPage("/Index");
    }

    private string? SafeLocalUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (Url.IsLocalUrl(url)) return url;
        return null;
    }
}
