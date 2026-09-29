using WebAppPet.Application.Behavior.Shared;
using WebAppPet.Application.Common;
using WebAppPet.Domain;
using WebAppPet.Domain.Markets;

namespace WebAppPet.Application.Behavior.GetBehaviorProviders;

/// <param name="Date">Selected day as yyyy-MM-dd.</param>
/// <param name="When">Legacy "hoy"/"manana", normalized into <paramref name="Date"/>.</param>
/// <param name="Slot">Time of day as shown, e.g. "10:30 AM".</param>
public sealed record GetBehaviorProvidersQuery(int ClientId, int CaseId, int ProviderId, string? Date, string? When, string? Slot);

/// <param name="Redirect">Set when the case cannot pick a specialist.</param>
/// <param name="ProviderId">The requested specialist, or 0 when it is not offered.</param>
/// <param name="Slot">The requested slot, or null when it is not offered, already past or taken.</param>
/// <param name="DistanceLabels">Distance (or city) from the client to each specialist, by specialist id.</param>
public sealed record BehaviorProviderOptions(
    BehaviorStep? Redirect,
    BehaviorCase? Case,
    List<Pet> SelectedDogs,
    ServiceCatalogItem? CatalogItem,
    List<GroomerProfile> Providers,
    List<PaymentMethod> Payments,
    Dictionary<int, string> DistanceLabels,
    int ProviderId,
    string? Date,
    string? When,
    string? Slot,
    HashSet<string> PastSlots,
    HashSet<string> OccupiedSlots)
{
    public static readonly IReadOnlyList<string> TimeSlots =
    [
        "9:00 AM", "10:00 AM", "10:30 AM", "1:00 PM", "2:00 PM", "3:00 PM", "5:00 PM"
    ];

    /// <summary>Price per dog with the chosen specialist, or the market price while none is chosen.</summary>
    public decimal UnitPrice => PriceOf(Providers.FirstOrDefault(p => p.Id == ProviderId));

    /// <summary>Price per dog with <paramref name="provider"/> in the family's market: the specialist's own price when they set one.</summary>
    public decimal PriceOf(GroomerProfile? provider)
    {
        if (CatalogItem is null)
            return 0;

        var marketPrice = MarketPrices.ForCatalog(CatalogItem);
        return provider is null
            ? marketPrice
            : MarketPrices.ForProvider(provider, BusinessMarketResolver.CountryFor(provider, provider.User?.CountryCode), marketPrice);
    }

    public static BehaviorProviderOptions RedirectTo(BehaviorStep step) =>
        new(step, null, [], null, [], [], [], 0, null, null, null, [], []);
}
