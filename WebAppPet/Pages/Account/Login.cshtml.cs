using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Services;

namespace WebAppPet.Pages.Account;

public class LoginModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly IStringLocalizer<SharedResource> _L;

    public LoginModel(AppDbContext db, AuthService auth, IStringLocalizer<SharedResource> L)
    {
        _db = db;
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

    public void OnGet()
    {
        BackPage = SafeLocalUrl(ReturnUrl) ?? "/Index";
        var feature = HttpContext.Features.Get<IRequestCultureFeature>();
        Culture = CultureCookie.Normalize(feature?.RequestCulture.UICulture.TwoLetterISOLanguageName);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        BackPage = SafeLocalUrl(ReturnUrl) ?? "/Index";
        Culture = CultureCookie.Normalize(Culture);
        CultureCookie.Set(Response, Culture);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == Email);
        if (user == null || !PasswordHasher.Verify(Password, user.PasswordHash))
        {
            ErrorMessage = _L["Login_Error"].Value;
            return Page();
        }

        user.PreferredLanguage = Culture;
        await _db.SaveChangesAsync();

        await _auth.SignInAsync(user);

        var dest = SafeLocalUrl(ReturnUrl);
        if (dest != null)
            return LocalRedirect(dest);

        return user.Role switch
        {
            Models.UserRole.Groomer => RedirectToPage("/Groomer/Dashboard"),
            Models.UserRole.Admin => RedirectToPage("/Admin/Approvals"),
            _ => RedirectToPage("/Index")
        };
    }

    private string? SafeLocalUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (Url.IsLocalUrl(url)) return url;
        return null;
    }
}
