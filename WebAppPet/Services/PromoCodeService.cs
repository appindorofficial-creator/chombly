using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
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

/// <summary>Lightweight promo codes advertised in the app (e.g. CHOMBLY10, TEST10).</summary>
public class PromoCodeService
{
    public const string FirstBookingCode = "CHOMBLY10";
    public const string TestTenCode = "TEST10";
    public const string TestTwentyCode = "TEST20";

    private readonly AppDbContext _db;
    private readonly IStringLocalizer<SharedResource> _L;
    private readonly bool _isDevelopment;

    private sealed record PromoRule(decimal Percent, bool FirstBookingOnly);

    private static readonly IReadOnlyDictionary<string, PromoRule> Catalog =
        new Dictionary<string, PromoRule>(StringComparer.Ordinal)
        {
            // Advertised: 10% off first booking
            [FirstBookingCode] = new(0.10m, FirstBookingOnly: true),
            // Always-on QA codes
            [TestTenCode] = new(0.10m, FirstBookingOnly: false),
            [TestTwentyCode] = new(0.20m, FirstBookingOnly: false),
        };

    public PromoCodeService(AppDbContext db, IStringLocalizer<SharedResource> L, IHostEnvironment env)
    {
        _db = db;
        _L = L;
        _isDevelopment = env.IsDevelopment();
    }

    public async Task<PromoApplyResult> TryApplyAsync(int? userId, string? code, decimal subtotal, CancellationToken ct = default)
    {
        subtotal = Math.Max(0, Math.Round(subtotal, 2));
        var normalized = Normalize(code);
        if (string.IsNullOrEmpty(normalized))
            return PromoApplyResult.None(subtotal);

        if (!Catalog.TryGetValue(normalized, out var rule))
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

        // In Development, let CHOMBLY10 work even after prior bookings so front QA can retest.
        var enforceFirst = rule.FirstBookingOnly && !_isDevelopment;
        if (enforceFirst)
        {
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
        }

        var discount = Math.Round(subtotal * rule.Percent, 2);
        if (discount > subtotal) discount = subtotal;

        return new PromoApplyResult
        {
            IsValid = true,
            NormalizedCode = normalized,
            DiscountAmount = discount,
            FinalTotal = Math.Round(subtotal - discount, 2)
        };
    }

    private static string Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "";
        var chars = code.Trim().ToUpperInvariant()
            .Where(c => !char.IsWhiteSpace(c))
            .ToArray();
        return new string(chars);
    }
}
