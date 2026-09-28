using WebAppPet.Application.Care.Shared;
using WebAppPet.Application.Payments.Shared;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Care.GetCarePlan;

/// <param name="Card">The card the first month would be charged to; only looked up while not subscribed.</param>
public sealed record CarePlanView(
    ServiceCatalogItem? CatalogItem,
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
        var (item, _) = await _catalog.CatalogPriceAsync(ct);
        if (query.UserId is not int userId)
            return new CarePlanView(item, null, 0, null);

        var subscription = await _care.GetActiveAsync(userId, ct);
        if (subscription is not null)
            return new CarePlanView(item, subscription, _care.RemainingQuickConsults(subscription), null);

        return new CarePlanView(item, null, 0, await _payments.FindCardAsync(userId, null, ct));
    }
}
