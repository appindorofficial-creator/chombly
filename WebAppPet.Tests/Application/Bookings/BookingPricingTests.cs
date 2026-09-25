using WebAppPet.Application.Bookings.Shared;
using WebAppPet.Application.Promotions.ApplyPromoCode;
using WebAppPet.Models;

namespace WebAppPet.Tests.Application.Bookings;

public class BookingPricingTests
{
    private static GroomerService Service(string name = "Paseo", int minutes = 60) => new()
    {
        Name = name,
        PriceSmall = 20_000m,
        PriceMedium = 25_000m,
        PriceLarge = 30_000m,
        PriceGiant = 35_000m,
        DurationMinutes = minutes
    };

    [Theory]
    [InlineData(64_000, 22_400)]
    [InlineData(28_800, 10_080)]
    [InlineData(100, 35)]
    [InlineData(333.33, 116.67)]
    public void Deposit_is_35_percent_rounded_to_cents(decimal total, decimal expected)
    {
        Assert.Equal(expected, BookingPricing.Deposit(total));
    }

    [Theory]
    [InlineData(40, 15)]
    [InlineData(12, 12)]
    [InlineData(0, 0)]
    public void Deposit_uses_minimum_of_15_capped_at_total(decimal total, decimal expected)
    {
        Assert.Equal(expected, BookingPricing.Deposit(total));
    }

    [Theory]
    [InlineData(20, 10)]
    [InlineData(8, 8)]
    [InlineData(40, 14)]
    public void Walk_deposit_uses_minimum_of_10(decimal total, decimal expected)
    {
        Assert.Equal(expected, BookingPricing.Deposit(total, BookingPricing.WalkMinimumDeposit));
    }

    [Fact]
    public void Quote_without_valid_promo_keeps_subtotal()
    {
        var quote = BookingPricing.Quote(64_000m, ApplyPromoCodeResult.None(64_000m));

        Assert.Equal(new BookingQuote(64_000m, 0m, 64_000m, 22_400m), quote);
    }

    [Fact]
    public void Quote_with_invalid_promo_ignores_it()
    {
        var quote = BookingPricing.Quote(64_000m, ApplyPromoCodeResult.Invalid("NOVALE", 64_000m, "err"));

        Assert.Equal(new BookingQuote(64_000m, 0m, 64_000m, 22_400m), quote);
    }

    [Fact]
    public void Quote_with_valid_promo_uses_discounted_total()
    {
        var promo = new ApplyPromoCodeResult { IsValid = true, NormalizedCode = "TEST20", DiscountAmount = 12_800m, FinalTotal = 51_200m };

        var quote = BookingPricing.Quote(64_000m, promo);

        Assert.Equal(new BookingQuote(64_000m, 12_800m, 51_200m, 17_920m), quote);
    }

    [Fact]
    public void Quote_passes_walk_minimum_through()
    {
        var quote = BookingPricing.Quote(20m, null, BookingPricing.WalkMinimumDeposit);

        Assert.Equal(10m, quote.Deposit);
    }

    [Theory]
    [InlineData(60, PetSize.Medium, 25_000)]
    [InlineData(30, PetSize.Medium, 12_500)]
    [InlineData(90, PetSize.Large, 45_000)]
    [InlineData(120, PetSize.Small, 40_000)]
    public void Walk_price_scales_with_duration(int minutes, PetSize size, decimal expected)
    {
        Assert.Equal(expected, BookingPricing.WalkPrice(Service(minutes: 60), size, minutes));
    }

    [Fact]
    public void Walk_price_without_pet_uses_small_price()
    {
        Assert.Equal(20_000m, BookingPricing.WalkPrice(Service(minutes: 60), null, 60));
    }

    [Fact]
    public void Walk_price_matching_duration_is_not_scaled()
    {
        Assert.Equal(25_000m, BookingPricing.WalkPrice(Service(minutes: 30), PetSize.Medium, 30));
    }

    [Fact]
    public void Walk_price_treats_zero_duration_service_as_60_minutes()
    {
        Assert.Equal(12_500m, BookingPricing.WalkPrice(Service(minutes: 0), PetSize.Medium, 30));
    }

    [Theory]
    [InlineData(10, 60, 25, 4)]
    [InlineData(10, 0, 30, 5)]
    public void Scale_by_minutes_rounds_to_whole_units(decimal price, int baseMinutes, int minutes, decimal expected)
    {
        Assert.Equal(expected, BookingPricing.ScaleByMinutes(price, baseMinutes, minutes));
    }

    [Fact]
    public void Daycare_full_day_uses_size_price()
    {
        Assert.Equal(30_000m, BookingPricing.DaycarePrice(Service("Día completo"), PetSize.Large, halfDay: false));
    }

    [Fact]
    public void Daycare_half_day_is_70_percent_of_full_day_service()
    {
        Assert.Equal(17_500m, BookingPricing.DaycarePrice(Service("Día completo"), PetSize.Medium, halfDay: true));
    }

    [Theory]
    [InlineData("Medio día")]
    [InlineData("Half day")]
    public void Daycare_half_day_service_is_not_discounted(string name)
    {
        Assert.Equal(25_000m, BookingPricing.DaycarePrice(Service(name), PetSize.Medium, halfDay: true));
    }
}
