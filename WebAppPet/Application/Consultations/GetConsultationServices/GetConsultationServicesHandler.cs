using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Consultations.GetConsultationServices;

/// <summary>Services the client can pick for a consultation, plus their Care balance. Null when it is not theirs.</summary>
public class GetConsultationServicesHandler
{
    private readonly AppDbContext _db;
    private readonly ClientHomeCountry _homeCountry;
    private readonly ServiceCatalogService _catalog;
    private readonly ChomblyCareService _care;

    public GetConsultationServicesHandler(AppDbContext db, ClientHomeCountry homeCountry, ServiceCatalogService catalog, ChomblyCareService care)
    {
        _db = db;
        _homeCountry = homeCountry;
        _catalog = catalog;
        _care = care;
    }

    public async Task<ConsultationServices?> HandleAsync(GetConsultationServicesQuery query, CancellationToken ct = default)
    {
        var consultation = await _db.OwnedConsultationAsync(query.ClientId, query.ConsultationId, ct);
        if (consultation is null)
            return null;

        var home = await _homeCountry.ResolveAsync(query.ClientId, ct);
        var sub = await _care.GetActiveAsync(query.ClientId, ct);
        return new ConsultationServices(
            consultation,
            await ListItemsAsync(_catalog, home, ct),
            home,
            sub != null,
            sub != null ? _care.RemainingQuickConsults(sub) : 0);
    }

    internal static async Task<List<ServiceCatalogItem>> ListItemsAsync(ServiceCatalogService catalog, string homeCountry, CancellationToken ct)
    {
        var all = await catalog.ListActiveAsync(ct);
        return MarketCountry.AllowsUsLocalTeleconsult(homeCountry)
            ? all
            : all.Where(i => i.Code != ServiceCatalogCodes.VetLocal30).ToList();
    }
}
