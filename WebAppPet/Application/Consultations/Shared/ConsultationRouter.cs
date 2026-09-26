using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Consultations.Shared;

/// <summary>
/// Decides where a screened consultation goes next and saves the service that path uses.
/// The local teleconsult is only offered when the client's home market is the United States.
/// </summary>
public class ConsultationRouter
{
    private readonly AppDbContext _db;
    private readonly VcprService _vcpr;

    public ConsultationRouter(AppDbContext db, VcprService vcpr)
    {
        _db = db;
        _vcpr = vcpr;
    }

    public async Task<ConsultationStep> RouteAfterSafetyAsync(Consultation consultation, string? next, string homeCountry, CancellationToken ct = default)
    {
        var path = ConsultationPath.Normalize(next, consultation.ServiceCatalogCode);
        if (path == ConsultationPath.Local && !MarketCountry.AllowsUsLocalTeleconsult(homeCountry))
            path = ConsultationPath.Intl;

        if (path == ConsultationPath.Local)
        {
            consultation.ServiceCatalogCode = ServiceCatalogCodes.VetLocal30;
            consultation.HasActiveVcpr = consultation.PetId is int petId && await _vcpr.HasActiveAsync(petId, consultation.PetUsState, ct);
            if (!consultation.HasActiveVcpr)
            {
                await _db.TouchAsync(consultation, ct);
                return ConsultationStep.Eligibility;
            }

            consultation.Status = ConsultationStatus.EligibilityVerified;
            await _db.TouchAsync(consultation, ct);
            return ConsultationStep.LocalProviders;
        }

        await UseGuidanceAsync(consultation, ct);
        return ConsultationStep.IntlMatches;
    }

    /// <summary>Switches the consultation to international guidance ranked for the pet's breed, and saves it.</summary>
    public async Task UseGuidanceAsync(Consultation consultation, CancellationToken ct = default)
    {
        consultation.ServiceCatalogCode = ServiceCatalogCodes.VetIntl30;
        consultation.MatchMode = IntlMatchMode.Best;
        if (string.IsNullOrWhiteSpace(consultation.PreferredBreed) && consultation.PetId is int petIdForBreed)
        {
            var breed = await _db.Pets.AsNoTracking()
                .Where(p => p.Id == petIdForBreed)
                .Select(p => p.Breed)
                .FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(breed))
                consultation.PreferredBreed = breed.Trim();
        }

        await _db.TouchAsync(consultation, ct);
    }
}
