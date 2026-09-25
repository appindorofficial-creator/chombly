using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using WebAppPet.Data;
using WebAppPet.Localization;

namespace WebAppPet.Application.Promotions.ApplyPromoCode;

/// <summary>Lightweight promo codes advertised in the app (e.g. CHOMBLY10, TEST10).</summary>
public class ApplyPromoCodeHandler
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

    public ApplyPromoCodeHandler(AppDbContext db, IStringLocalizer<SharedResource> L, IHostEnvironment env)
    {
        _db = db;
        _L = L;
        _isDevelopment = env.IsDevelopment();
    }

    public async Task<ApplyPromoCodeResult> HandleAsync(ApplyPromoCodeCommand command, CancellationToken ct = default)
    {
        var subtotal = Math.Max(0, Math.Round(command.Subtotal, 2));
        var normalized = Normalize(command.Code);
        if (string.IsNullOrEmpty(normalized))
            return ApplyPromoCodeResult.None(subtotal);

        if (!Catalog.TryGetValue(normalized, out var rule))
            return ApplyPromoCodeResult.Invalid(normalized, subtotal, _L["Promo_ErrInvalid"].Value);

        if (command.UserId is not int uid)
            return ApplyPromoCodeResult.Invalid(normalized, subtotal, _L["Promo_ErrLogin"].Value);

        // In Development, let CHOMBLY10 work even after prior bookings so front QA can retest.
        var enforceFirst = rule.FirstBookingOnly && !_isDevelopment;
        if (enforceFirst && await _db.Appointments.AnyAsync(a => a.ClientId == uid, ct))
            return ApplyPromoCodeResult.Invalid(normalized, subtotal, _L["Promo_ErrNotFirst"].Value);

        var discount = Math.Round(subtotal * rule.Percent, 2);
        if (discount > subtotal) discount = subtotal;

        return new ApplyPromoCodeResult
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
