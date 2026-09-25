using WebAppPet.Application.Promotions.ApplyPromoCode;
using WebAppPet.Models;

namespace WebAppPet.Application.Bookings.Shared;

public sealed record BookingQuote(decimal Subtotal, decimal Discount, decimal Total, decimal Deposit);

public static class BookingPricing
{
    public const decimal DepositRate = 0.35m;
    public const decimal MinimumDeposit = 15m;
    public const decimal WalkMinimumDeposit = 10m;
    public const decimal HalfDayFactor = 0.7m;
    public const int DefaultServiceMinutes = 60;

    /// <summary>35% of the total, never below the minimum unless the total itself is lower.</summary>
    public static decimal Deposit(decimal total, decimal minimum = MinimumDeposit)
    {
        var deposit = Math.Round(total * DepositRate, 2);
        return deposit < minimum ? Math.Min(minimum, total) : deposit;
    }

    public static BookingQuote Quote(decimal subtotal, ApplyPromoCodeResult? promo, decimal minimumDeposit = MinimumDeposit)
    {
        var valid = promo is { IsValid: true };
        var discount = valid ? promo!.DiscountAmount : 0m;
        var total = valid ? promo!.FinalTotal : subtotal;
        return new BookingQuote(subtotal, discount, total, Deposit(total, minimumDeposit));
    }

    public static decimal ScaleByMinutes(decimal basePrice, int baseMinutes, int minutes)
    {
        if (baseMinutes <= 0) baseMinutes = DefaultServiceMinutes;
        return Math.Round(basePrice * minutes / (decimal)baseMinutes, 0);
    }

    /// <summary>Walk price for the chosen duration; services priced for another duration scale linearly.</summary>
    public static decimal WalkPrice(GroomerService service, PetSize? size, int minutes)
    {
        var basePrice = size is PetSize s ? service.PriceFor(s) : service.PriceSmall;
        if (service.DurationMinutes == minutes) return basePrice;
        var baseMinutes = service.DurationMinutes > 0 ? service.DurationMinutes : DefaultServiceMinutes;
        return ScaleByMinutes(basePrice, baseMinutes, minutes);
    }

    /// <summary>Half day costs 70% of a full-day service unless the service is already a half-day offering.</summary>
    public static decimal DaycarePrice(GroomerService service, PetSize? size, bool halfDay)
    {
        var full = size is PetSize s ? service.PriceFor(s) : service.PriceSmall;
        if (!halfDay) return full;
        var looksHalf = service.Name.Contains("medio", StringComparison.OrdinalIgnoreCase)
                        || service.Name.Contains("half", StringComparison.OrdinalIgnoreCase);
        return looksHalf ? full : Math.Round(full * HalfDayFactor, 0);
    }
}
