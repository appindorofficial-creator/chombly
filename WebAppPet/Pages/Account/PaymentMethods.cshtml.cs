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

    public List<PaymentMethod> Items { get; set; } = new();

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

        await LoadAsync(userId);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login");

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
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login");

        var pm = await _db.PaymentMethods.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        if (pm != null)
        {
            _db.PaymentMethods.Remove(pm);
            await _db.SaveChangesAsync();
        }

        return RedirectToPage();
    }

    private string MessageFor(CardValidator.FailReason reason) => reason switch
    {
        CardValidator.FailReason.CardNumberRequired => _L["Pay_ErrCardRequired"].Value,
        CardValidator.FailReason.CardNumberInvalid => _L["Pay_ErrCardInvalid"].Value,
        CardValidator.FailReason.CardNumberLuhn => _L["Pay_ErrCardLuhn"].Value,
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
