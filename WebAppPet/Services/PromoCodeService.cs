using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using WebAppPet.Data;
using WebAppPet.Localization;

namespace WebAppPet.Services;

public sealed class PromoApplyResult
{
    public bool IsValid { get; init; }
    public string? NormalizedCode { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal FinalTotal { get; init; }
    /// <summary>Localized error when the user entered a code that cannot be applied.</summary>
    public string? ErrorMessage { get; init; }

    public static PromoApplyResult None(decimal subtotal) => new()
    {
        IsValid = false,
        FinalTotal = Math.Round(subtotal, 2)
    };
}

/// <summary>Lightweight promo codes advertised in the app (e.g. CHOMBLY10).</summary>
public class PromoCodeService
{
    public const string FirstBookingCode = "CHOMBLY10";
    public const decimal FirstBookingPercent = 0.10m;

    private readonly AppDbContext _db;
    private readonly IStringLocalizer<SharedResource> _L;

    public PromoCodeService(AppDbContext db, IStringLocalizer<SharedResource> L)
    {
        _db = db;
        _L = L;
    }

    public async Task<PromoApplyResult> TryApplyAsync(int? userId, string? code, decimal subtotal, CancellationToken ct = default)
    {
        subtotal = Math.Max(0, Math.Round(subtotal, 2));
        var trimmed = code?.Trim() ?? "";
        if (string.IsNullOrEmpty(trimmed))
            return PromoApplyResult.None(subtotal);

        var normalized = trimmed.ToUpperInvariant();

        if (!string.Equals(normalized, FirstBookingCode, StringComparison.Ordinal))
        {
            return new PromoApplyResult
            {
                IsValid = false,
                NormalizedCode = normalized,
                FinalTotal = subtotal,
                ErrorMessage = _L["Promo_ErrInvalid"].Value
            };
        }

        if (userId is not int uid)
        {
            return new PromoApplyResult
            {
                IsValid = false,
                NormalizedCode = normalized,
                FinalTotal = subtotal,
                ErrorMessage = _L["Promo_ErrLogin"].Value
            };
        }

        var hasPrior = await _db.Appointments.AnyAsync(a => a.ClientId == uid, ct);
        if (hasPrior)
        {
            return new PromoApplyResult
            {
                IsValid = false,
                NormalizedCode = normalized,
                FinalTotal = subtotal,
                ErrorMessage = _L["Promo_ErrNotFirst"].Value
            };
        }

        var discount = Math.Round(subtotal * FirstBookingPercent, 2);
        if (discount > subtotal) discount = subtotal;

        return new PromoApplyResult
        {
            IsValid = true,
            NormalizedCode = normalized,
            DiscountAmount = discount,
            FinalTotal = Math.Round(subtotal - discount, 2)
        };
    }
}
