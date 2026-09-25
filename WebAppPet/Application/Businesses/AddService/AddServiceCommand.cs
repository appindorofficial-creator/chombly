namespace WebAppPet.Application.Businesses.AddService;

/// <param name="Price">Price for small pets. Larger sizes add the market's size step.</param>
/// <param name="CountryCode">Market of the business, used to pick the size step.</param>
public sealed record AddServiceCommand(int BusinessId, string? Name, string BillingUnit, decimal Price, string? CountryCode);
