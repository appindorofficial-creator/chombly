using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Consultations.GetConsultationCheckout;

/// <summary>
/// Price, Care balance and cards for a consultation that already has a provider.
/// Null when it is not the client's, has no provider yet or its service is inactive.
/// </summary>
public class GetConsultationCheckoutHandler
{
    private readonly AppDbContext _db;
    private readonly ClientHomeCountry _homeCountry;
    private readonly ServiceCatalogService _catalog;
    private readonly ChomblyCareService _care;
    private readonly VetAuditService _audit;

    public GetConsultationCheckoutHandler(
        AppDbContext db,
        ClientHomeCountry homeCountry,
        ServiceCatalogService catalog,
        ChomblyCareService care,
        VetAuditService audit)
    {
        _db = db;
        _homeCountry = homeCountry;
        _catalog = catalog;
        _care = care;
        _audit = audit;
    }

    public async Task<CheckoutDetails?> HandleAsync(GetConsultationCheckoutQuery query, CancellationToken ct = default)
    {
        var consultation = await _db.OwnedConsultationAsync(query.ClientId, query.ConsultationId, ct);
        if (consultation is null || consultation.Status < ConsultationStatus.ProviderSelected)
            return null;

        var item = await _catalog.GetAsync(consultation.ServiceCatalogCode ?? "", ct);
        if (item is null)
            return null;

        var provider = consultation.ProviderId is int providerId
            ? await _db.Groomers.AsNoTracking().FirstOrDefaultAsync(g => g.Id == providerId, ct)
            : null;

        var sub = await _care.GetActiveAsync(query.ClientId, ct);
        var payments = await _db.PaymentMethods.AsNoTracking()
            .Where(p => p.UserId == query.ClientId)
            .OrderByDescending(p => p.IsDefault)
            .ToListAsync(ct);

        var details = new CheckoutDetails(
            consultation,
            item,
            provider,
            provider?.StartingPrice > 0 ? provider.StartingPrice : item.Price,
            sub != null ? _care.RemainingQuickConsults(sub) : 0,
            await _homeCountry.ResolveAsync(query.ClientId, ct),
            payments,
            payments.FirstOrDefault(p => p.IsDefault) ?? payments.FirstOrDefault());

        if (query.LogStarted)
            await _audit.LogAsync("checkout_started", query.ClientId, "Consultation", query.ConsultationId, ct: ct);

        return details;
    }
}
