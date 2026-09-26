using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Consultations.GetConsultationSummary;

/// <summary>
/// A consultation as the client sees it, with the step to resume when it is not booked yet.
/// Null when it is not theirs.
/// </summary>
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
            await _homeCountry.ResolveAsync(query.ClientId, ct),
            ResumeStep(consultation));
    }

    private static ConsultationStep? ResumeStep(Consultation consultation)
    {
        var booked = consultation.AppointmentId is not null
            || consultation.Status is ConsultationStatus.Scheduled or ConsultationStatus.InProgress
                or ConsultationStatus.Completed or ConsultationStatus.FollowUpOpen or ConsultationStatus.Closed;
        if (booked)
            return null;

        return consultation is { ProviderId: not null, PetId: not null, ScheduledAt: not null }
            ? ConsultationStep.Checkout
            : ConsultationStep.Pet;
    }
}
