using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Account;

public class PaymentMethodsModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly IStringLocalizer<SharedResource> _L;

    public PaymentMethodsModel(AppDbContext db, AuthService auth, IStringLocalizer<SharedResource> L)
    {
        _db = db;
        _auth = auth;
        _L = L;
    }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string BackHref { get; private set; } = "/Account/Profile";

    public List<PaymentMethod> Items { get; set; } = new();

    private const string PayReturnCookie = "chombly.payReturn";

    [BindProperty]
    public string CardNumber { get; set; } = string.Empty;

    [BindProperty]
    public string Expiry { get; set; } = string.Empty;

    [BindProperty]
    public string Cvv { get; set; } = string.Empty;

    [BindProperty]
    public string HolderName { get; set; } = string.Empty;

    [BindProperty]
    public bool MakeDefault { get; set; }

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login");

        BackHref = ResolveBackHref();
        await LoadAsync(userId);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login");

        BackHref = ResolveBackHref();

        var result = CardValidator.Validate(CardNumber, Expiry, Cvv, HolderName);
        if (!result.Ok)
        {
            ErrorMessage = MessageFor(result.Reason);
            await LoadAsync(userId);
            return Page();
        }

        if (MakeDefault)
        {
            var current = await _db.PaymentMethods.Where(p => p.UserId == userId && p.IsDefault).ToListAsync();
            foreach (var c in current) c.IsDefault = false;
        }

        _db.PaymentMethods.Add(new PaymentMethod
        {
            UserId = userId,
            Brand = result.Brand == CardValidator.CardBrand.Unknown
                ? _L["Pay_BrandUnknown"].Value
                : result.BrandName,
            Last4 = result.Last4,
            HolderName = result.HolderName,
            ExpMonth = result.ExpMonth,
            ExpYear = result.ExpYear,
            IsDefault = MakeDefault || !await _db.PaymentMethods.AnyAsync(p => p.UserId == userId)
        });

        await _db.SaveChangesAsync();
        return RedirectAfterMutation();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login");

        BackHref = ResolveBackHref();

        var pm = await _db.PaymentMethods.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        if (pm != null)
        {
            _db.PaymentMethods.Remove(pm);
            await _db.SaveChangesAsync();
        }

        return RedirectAfterMutation();
    }

    private IActionResult RedirectAfterMutation()
    {
        if (TryLocalPath(ReturnUrl, out var local))
            return LocalRedirect(local);
        if (TryLocalPath(Request.Cookies[PayReturnCookie], out var fromCookie))
            return LocalRedirect(fromCookie);
        return RedirectToPage(new { returnUrl = ReturnUrl });
    }

    private string ResolveBackHref()
    {
        // Prefer explicit query (also re-read raw query in case model binding truncated).
        var rawQuery = Request.Query["returnUrl"].ToString();
        if (TryLocalPath(ReturnUrl, out var fromModel))
        {
            RememberReturn(fromModel);
            return fromModel;
        }
        if (TryLocalPath(rawQuery, out var fromRaw))
        {
            RememberReturn(fromRaw);
            return fromRaw;
        }
        if (TryLocalPath(Request.Cookies[PayReturnCookie], out var fromCookie))
            return fromCookie;

        var referer = Request.Headers.Referer.ToString();
        if (Uri.TryCreate(referer, UriKind.Absolute, out var uri)
            && string.Equals(uri.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase)
            && !uri.AbsolutePath.Contains("/Account/PaymentMethods", StringComparison.OrdinalIgnoreCase)
            && TryLocalPath(uri.PathAndQuery, out var fromReferer))
        {
            RememberReturn(fromReferer);
            return fromReferer;
        }

        return Url.Page("./Profile") ?? "/Account/Profile";
    }

    private void RememberReturn(string path)
    {
        Response.Cookies.Append(PayReturnCookie, path, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromHours(2),
            Secure = Request.IsHttps
        });
    }

    private bool TryLocalPath(string? candidate, out string path)
    {
        path = "/Account/Profile";
        if (string.IsNullOrWhiteSpace(candidate)) return false;

        var value = Uri.UnescapeDataString(candidate.Trim());
        // Absolute same-host URLs → path+query
        if (Uri.TryCreate(value, UriKind.Absolute, out var abs)
            && string.Equals(abs.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase))
        {
            value = abs.PathAndQuery;
        }

        if (!value.StartsWith('/') || value.StartsWith("//", StringComparison.Ordinal))
            return false;
        if (value.Contains("://", StringComparison.Ordinal))
            return false;
        if (value.Contains("/Account/PaymentMethods", StringComparison.OrdinalIgnoreCase))
            return false;

        // Prefer Url.IsLocalUrl when it accepts; otherwise allow plain root-relative paths.
        if (Url.IsLocalUrl(value) || (value[0] == '/' && value.Length > 1 && value[1] != '/' && value[1] != '\\'))
        {
            path = value;
            return true;
        }

        return false;
    }

    private string MessageFor(CardValidator.FailReason reason) => reason switch
    {
        CardValidator.FailReason.CardNumberRequired => _L["Pay_ErrCardRequired"].Value,
        CardValidator.FailReason.CardNumberInvalid => _L["Pay_ErrCardInvalid"].Value,
        CardValidator.FailReason.ExpiryRequired => _L["Pay_ErrExpiryRequired"].Value,
        CardValidator.FailReason.ExpiryFormat => _L["Pay_ErrExpiryFormat"].Value,
        CardValidator.FailReason.ExpiryMonth => _L["Pay_ErrExpiryMonth"].Value,
        CardValidator.FailReason.ExpiryExpired => _L["Pay_ErrExpiryExpired"].Value,
        CardValidator.FailReason.CvvRequired => _L["Pay_ErrCvvRequired"].Value,
        CardValidator.FailReason.CvvInvalid => _L["Pay_ErrCvvInvalid"].Value,
        CardValidator.FailReason.HolderRequired => _L["Pay_ErrHolderRequired"].Value,
        CardValidator.FailReason.HolderInvalid => _L["Pay_ErrHolderInvalid"].Value,
        _ => _L["Pay_ErrGeneric"].Value
    };

    private async Task LoadAsync(int userId) =>
        Items = await _db.PaymentMethods
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.IsDefault)
            .ToListAsync();
}
