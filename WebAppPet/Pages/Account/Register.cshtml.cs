using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using WebAppPet.Application.Accounts.Register;
using WebAppPet.Infrastructure.Security;
using WebAppPet.Localization;
using WebAppPet.Services;

namespace WebAppPet.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly RegisterHandler _register;
    private readonly AuthService _auth;
    private readonly IStringLocalizer<SharedResource> _L;

    public RegisterModel(RegisterHandler register, AuthService auth, IStringLocalizer<SharedResource> L)
    {
        _register = register;
        _auth = auth;
        _L = L;
    }

    [BindProperty, Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [BindProperty, Required, EmailAddress, MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [BindProperty, Required, MaxLength(10)]
    public string Phone { get; set; } = string.Empty;

    [BindProperty, Required, MaxLength(120)]
    public string City { get; set; } = string.Empty;

    [BindProperty]
    public string? Latitude { get; set; }

    [BindProperty]
    public string? Longitude { get; set; }

    [BindProperty, Required, MinLength(PasswordPolicy.MinLength)]
    public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string BackHref { get; set; } = "/Welcome";

    public IActionResult OnGet()
    {
        if (_auth.IsAuthenticated)
            return RedirectToPage("/Index");

        BackHref = SafeLocalUrl(ReturnUrl) ?? "/Welcome";
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        BackHref = SafeLocalUrl(ReturnUrl) ?? "/Welcome";

        var result = await _register.HandleAsync(new RegisterCommand(
            FullName, Email, Phone, City, Latitude, Longitude, Password,
            DefaultCity: _L["Register_LocationOk"].Value));

        if (result.User is not { } user)
        {
            ErrorMessage = result.Error switch
            {
                RegisterError.NameRequired => _L["Profile_Edit_NameRequired"].Value,
                RegisterError.EmailInvalid => _L["Profile_Edit_EmailInvalid"].Value,
                RegisterError.PhoneRequired => _L["Phone_Required"].Value,
                RegisterError.PhoneInvalid => _L["Phone_Invalid"].Value,
                RegisterError.PasswordWeak => _L["Profile_Edit_PasswordShort"].Value,
                RegisterError.LocationRequired => _L["Register_LocationRequired"].Value,
                _ => _L["Profile_Edit_EmailTaken"].Value
            };
            return Page();
        }

        await _auth.SignInAsync(user);
        TempData["CelebrateRegister"] = "1";
        return RedirectToPage("/Index");
    }

    private string? SafeLocalUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (Url.IsLocalUrl(url)) return url;
        return null;
    }
}
