using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Consultations.GetLocalVets;

/// <summary>
/// Local US vets for a consultation with an active VCPR. Clients outside the US are moved
/// to the guidance matches.
/// </summary>
public class GetLocalVetsHandler
{
    private const int MaxProviders = 30;

    private readonly AppDbContext _db;
    private readonly ClientHomeCountry _homeCountry;
    private readonly ServiceCatalogService _catalog;

    public GetLocalVetsHandler(AppDbContext db, ClientHomeCountry homeCountry, ServiceCatalogService catalog)
    {
        _db = db;
        _homeCountry = homeCountry;
        _catalog = catalog;
    }

    public async Task<LocalVetOptions> HandleAsync(GetLocalVetsQuery query, CancellationToken ct = default)
    {
        var consultation = await _db.OwnedConsultationAsync(query.ClientId, query.ConsultationId, ct);
        if (consultation is null)
            return LocalVetOptions.RedirectTo(ConsultationStep.VetHome);

        if (!MarketCountry.AllowsUsLocalTeleconsult(await _homeCountry.ResolveAsync(query.ClientId, ct)))
        {
            consultation.ServiceCatalogCode = ServiceCatalogCodes.VetIntl30;
            consultation.MatchMode = IntlMatchMode.Best;
            await _db.TouchAsync(consultation, ct);
            return LocalVetOptions.RedirectTo(ConsultationStep.IntlMatches);
        }

        if (!consultation.HasActiveVcpr)
            return LocalVetOptions.RedirectTo(ConsultationStep.Eligibility);

        consultation.ServiceCatalogCode ??= ServiceCatalogCodes.VetLocal30;
        return await LoadAsync(_db, _catalog, consultation, ct);
    }

    internal static async Task<LocalVetOptions> LoadAsync(AppDbContext db, ServiceCatalogService catalog, Consultation consultation, CancellationToken ct)
    {
        var state = consultation.PetUsState;
        var providers = await LocalVets(db)
            .Where(g => g.Licenses.Any(l => l.IsVerified && l.IsUsState && l.Jurisdiction == state))
            .OrderByDescending(g => g.Rating)
            .Take(MaxProviders)
            .ToListAsync(ct);

        if (providers.Count == 0)
        {
            providers = await LocalVets(db)
                .OrderByDescending(g => g.Rating)
                .Take(MaxProviders)
                .ToListAsync(ct);
        }

        return new LocalVetOptions(null, consultation, await catalog.GetAsync(ServiceCatalogCodes.VetLocal30, ct), providers);
    }

    private static IQueryable<GroomerProfile> LocalVets(AppDbContext db) =>
        db.Groomers.AsNoTracking()
            .Include(g => g.Licenses)
            .Where(g => g.IsActive && g.PublishStatus == BusinessPublishStatus.Approved && g.VetProviderKind == VetProviderKind.LocalVet);
}
