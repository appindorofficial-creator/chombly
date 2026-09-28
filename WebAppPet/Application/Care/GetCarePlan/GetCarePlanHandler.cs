using WebAppPet.Application.Care.Shared;
using WebAppPet.Application.Common;
using WebAppPet.Application.Payments.Shared;
using WebAppPet.Domain;

namespace WebAppPet.Application.Care.GetCarePlan;

/// <param name="MonthlyPrice">What the family's market is charged per month.</param>
/// <param name="Card">The card the first month would be charged to; only looked up while not subscribed.</param>
public sealed record CarePlanView(
    ServiceCatalogItem? CatalogItem,
    decimal MonthlyPrice,
    CareSubscription? Subscription,
    int RemainingConsults,
    PaymentMethod? Card);

public class GetCarePlanHandler
{
    private readonly ServiceCatalogService _catalog;
    private readonly ChomblyCareService _care;
    private readonly PaymentService _payments;

    public GetCarePlanHandler(ServiceCatalogService catalog, ChomblyCareService care, PaymentService payments)
    {
        _catalog = catalog;
        _care = care;
        _payments = payments;
    }

    public async Task<CarePlanView> HandleAsync(GetCarePlanQuery query, CancellationToken ct = default)
    {
        var (item, price) = await _catalog.CatalogPriceAsync(ct);
        if (query.UserId is not int userId)
            return new CarePlanView(item, price, null, 0, null);

        var subscription = await _care.GetActiveAsync(userId, ct);
        if (subscription is not null)
            return new CarePlanView(item, price, subscription, _care.RemainingQuickConsults(subscription), null);

        return new CarePlanView(item, price, null, 0, await _payments.FindCardAsync(userId, null, ct));
    }
}
