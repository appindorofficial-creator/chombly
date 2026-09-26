using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Data;
using WebAppPet.Services;

namespace WebAppPet.Application.Consultations.GetConsultationSummary;

/// <summary>A booked consultation as the client sees it. Null when it is not theirs.</summary>
public class GetConsultationSummaryHandler
{
    private readonly AppDbContext _db;
    private readonly ClientHomeCountry _homeCountry;
    private readonly ServiceCatalogService _catalog;

    public GetConsultationSummaryHandler(AppDbContext db, ClientHomeCountry homeCountry, ServiceCatalogService catalog)
    {
        _db = db;
        _homeCountry = homeCountry;
        _catalog = catalog;
    }

    public async Task<ConsultationSummary?> HandleAsync(GetConsultationSummaryQuery query, CancellationToken ct = default)
    {
        var consultation = await _db.Consultations
            .AsNoTracking()
            .Include(c => c.Pet)
            .Include(c => c.Provider)
            .FirstOrDefaultAsync(c => c.Id == query.ConsultationId && c.ClientId == query.ClientId, ct);
        if (consultation is null)
            return null;

        return new ConsultationSummary(
            consultation,
            await _catalog.GetAsync(consultation.ServiceCatalogCode ?? "", ct),
            await _homeCountry.ResolveAsync(query.ClientId, ct));
    }
}
