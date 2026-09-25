namespace WebAppPet.Application.Promotions.ApplyPromoCode;

public sealed class ApplyPromoCodeResult
{
    public bool IsValid { get; init; }
    public string? NormalizedCode { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal FinalTotal { get; init; }
    /// <summary>Localized error when the user entered a code that cannot be applied.</summary>
    public string? ErrorMessage { get; init; }

    public static ApplyPromoCodeResult None(decimal subtotal) => new()
    {
        IsValid = false,
        FinalTotal = Math.Round(subtotal, 2)
    };

    public static ApplyPromoCodeResult Invalid(string code, decimal subtotal, string error) => new()
    {
        IsValid = false,
        NormalizedCode = code,
        FinalTotal = subtotal,
        ErrorMessage = error
    };
}
