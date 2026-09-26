using WebAppPet.Application.Consultations.GetConsultationServices;
using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Consultations.ChooseConsultationService;

/// <summary>
/// Saves the service picked for the consultation. Chombly Care goes to the plan page; the local
/// teleconsult needs a US client with an active VCPR.
/// </summary>
public class ChooseConsultationServiceHandler
{
    private readonly AppDbContext _db;
    private readonly ClientHomeCountry _homeCountry;
    private readonly ServiceCatalogService _catalog;
    private readonly ChomblyCareService _care;

    public ChooseConsultationServiceHandler(AppDbContext db, ClientHomeCountry homeCountry, ServiceCatalogService catalog, ChomblyCareService care)
    {
        _db = db;
        _homeCountry = homeCountry;
        _catalog = catalog;
        _care = care;
    }

    public async Task<ChooseConsultationServiceResult> HandleAsync(ChooseConsultationServiceCommand command, CancellationToken ct = default)
    {
        var consultation = await _db.OwnedConsultationAsync(command.ClientId, command.ConsultationId, ct);
        if (consultation is null)
            return new ChooseConsultationServiceResult(ChooseConsultationServiceOutcome.NotFound);

        if (command.UseCare)
            return await UseCareAsync(command.ClientId, consultation, ct);

        if (command.ServiceCode == ServiceCatalogCodes.ChomblyCare)
            return Moved(ConsultationStep.ChomblyCare);

        var home = await _homeCountry.ResolveAsync(command.ClientId, ct);
        var items = await GetConsultationServicesHandler.ListItemsAsync(_catalog, home, ct);
        var item = items.FirstOrDefault(i => i.Code == command.ServiceCode);
        if (item is null || !item.IsBookable)
            return new ChooseConsultationServiceResult(ChooseConsultationServiceOutcome.Invalid);

        if (item.Code == ServiceCatalogCodes.VetLocal30)
        {
            if (!MarketCountry.AllowsUsLocalTeleconsult(home))
                return Moved(ConsultationStep.IntlMatches);
            if (!consultation.HasActiveVcpr)
                return Moved(ConsultationStep.Eligibility);
        }

        consultation.ServiceCatalogCode = item.Code;
        consultation.UsesCareBenefit = false;
        consultation.Status = ConsultationStatus.EligibilityVerified;
        await _db.TouchAsync(consultation, ct);

        return Moved(item.Code == ServiceCatalogCodes.VetIntl30 ? ConsultationStep.IntlHome : ConsultationStep.LocalProviders);
    }

    private async Task<ChooseConsultationServiceResult> UseCareAsync(int clientId, Consultation consultation, CancellationToken ct)
    {
        if (!await _care.HasQuickConsultAvailableAsync(clientId, ct))
            return Moved(ConsultationStep.ChomblyCare);

        consultation.ServiceCatalogCode = ServiceCatalogCodes.VetIntl30;
        consultation.UsesCareBenefit = true;
        consultation.Status = ConsultationStatus.EligibilityVerified;
        await _db.TouchAsync(consultation, ct);
        return Moved(ConsultationStep.IntlHome);
    }

    private static ChooseConsultationServiceResult Moved(ConsultationStep step) =>
        new(ChooseConsultationServiceOutcome.Moved, step);
}
