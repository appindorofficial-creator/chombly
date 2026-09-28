namespace WebAppPet.Application.Payments.Shared;

/// <summary>Rate used to settle a business for charges taken in another currency than its own.</summary>
public class ExchangeRateOptions
{
    public const string SectionName = "ExchangeRates";

    public decimal CopPerUsd { get; set; } = 4000m;

    public decimal Convert(decimal amount, string fromCurrency, string toCurrency)
    {
        if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase) || CopPerUsd <= 0)
            return amount;
        if (string.Equals(fromCurrency, "USD", StringComparison.OrdinalIgnoreCase))
            return Math.Round(amount * CopPerUsd, 2);
        return Math.Round(amount / CopPerUsd, 2);
    }
}
