using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Consultations.CheckEligibility;

/// <summary>
/// The VCPR eligibility notice is only for US clients; everyone else is moved to the
/// guidance matches.
/// </summary>
public class CheckEligibilityHandler
{
    private readonly AppDbContext _db;
    private readonly ClientHomeCountry _homeCountry;

    public CheckEligibilityHandler(AppDbContext db, ClientHomeCountry homeCountry)
    {
        _db = db;
        _homeCountry = homeCountry;
    }

    public async Task<EligibilityCheck> HandleAsync(CheckEligibilityQuery query, CancellationToken ct = default)
    {
        var consultation = await _db.OwnedConsultationAsync(query.ClientId, query.ConsultationId, ct);
        if (consultation is null)
            return new EligibilityCheck(ConsultationStep.VetHome, null);

        if (!MarketCountry.AllowsUsLocalTeleconsult(await _homeCountry.ResolveAsync(query.ClientId, ct)))
        {
            consultation.ServiceCatalogCode = ServiceCatalogCodes.VetIntl30;
            consultation.MatchMode = IntlMatchMode.Best;
            consultation.Status = ConsultationStatus.SafetyScreened;
            await _db.TouchAsync(consultation, ct);
            return new EligibilityCheck(ConsultationStep.IntlMatches, null);
        }

        return new EligibilityCheck(null, consultation);
    }
}
