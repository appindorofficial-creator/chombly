using static WebAppPet.Application.Payments.Shared.CardValidator;

namespace WebAppPet.Tests.Application.Payments;

public class CardValidatorTests
{
    private const string FutureExpiry = "12/35";

    [Fact]
    public void Accepts_a_valid_visa_and_keeps_only_what_is_stored()
    {
        var result = Validate("4111 1111 1111 1111", FutureExpiry, "123", "  Ana   López ");

        Assert.True(result.Ok);
        Assert.Equal(CardBrand.Visa, result.Brand);
        Assert.Equal("Visa", result.BrandName);
        Assert.Equal("1111", result.Last4);
        Assert.Equal(12, result.ExpMonth);
        Assert.Equal(2035, result.ExpYear);
        Assert.Equal("Ana López", result.HolderName);
    }

    [Theory]
    [InlineData("4111111111111111", CardBrand.Visa)]
    [InlineData("5500000000000004", CardBrand.Mastercard)]
    [InlineData("2221000000000009", CardBrand.Mastercard)]
    [InlineData("378282246310005", CardBrand.Amex)]
    [InlineData("6011111111111117", CardBrand.Discover)]
    [InlineData("6445000000000000", CardBrand.Discover)]
    [InlineData("9999999999999999", CardBrand.Unknown)]
    public void Detects_the_brand_from_the_prefix(string digits, CardBrand expected) =>
        Assert.Equal(expected, DetectBrand(digits));

    [Fact]
    public void Amex_needs_a_four_digit_cvv()
    {
        Assert.Equal(FailReason.CvvInvalid, Validate("378282246310005", FutureExpiry, "123", "Ana").Reason);
        Assert.True(Validate("378282246310005", FutureExpiry, "1234", "Ana").Ok);
    }

    [Theory]
    [InlineData("12/35")]
    [InlineData("12/2035")]
    [InlineData("1235")]
    [InlineData("12-35")]
    [InlineData("122035")]
    public void Accepts_common_expiry_formats(string expiry)
    {
        Assert.True(TryParseExpiry(expiry, out var month, out var year, out _));
        Assert.Equal(12, month);
        Assert.Equal(2035, year);
    }

    [Theory]
    [InlineData("", "12/35", "123", "Ana", FailReason.CardNumberRequired)]
    [InlineData("4111 1111", "12/35", "123", "Ana", FailReason.CardNumberInvalid)]
    [InlineData("4111111111111111", "", "123", "Ana", FailReason.ExpiryRequired)]
    [InlineData("4111111111111111", "123", "123", "Ana", FailReason.ExpiryFormat)]
    [InlineData("4111111111111111", "13/35", "123", "Ana", FailReason.ExpiryMonth)]
    [InlineData("4111111111111111", "01/20", "123", "Ana", FailReason.ExpiryExpired)]
    [InlineData("4111111111111111", "12/35", "", "Ana", FailReason.CvvRequired)]
    [InlineData("4111111111111111", "12/35", "12a", "Ana", FailReason.CvvInvalid)]
    [InlineData("4111111111111111", "12/35", "123", " ", FailReason.HolderRequired)]
    [InlineData("4111111111111111", "12/35", "123", "A", FailReason.HolderInvalid)]
    [InlineData("4111111111111111", "12/35", "123", "Ana 123", FailReason.HolderInvalid)]
    public void Reports_the_first_problem_found(string card, string expiry, string cvv, string holder, FailReason expected)
    {
        var result = Validate(card, expiry, cvv, holder);

        Assert.False(result.Ok);
        Assert.Equal(expected, result.Reason);
    }
}
