using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Consultations.SelectIntlVet;

/// <summary>
/// Assigns the chosen international advisor to the consultation, in the advisor's license
/// country, and uses the client's Care quick consult when one is left.
/// </summary>
public class SelectIntlVetHandler
{
    private readonly AppDbContext _db;
    private readonly ChomblyCareService _care;
    private readonly VetAuditService _audit;

    public SelectIntlVetHandler(AppDbContext db, ChomblyCareService care, VetAuditService audit)
    {
        _db = db;
        _care = care;
        _audit = audit;
    }

    public async Task<SelectIntlVetOutcome> HandleAsync(SelectIntlVetCommand command, CancellationToken ct = default)
    {
        var consultation = await _db.OwnedConsultationAsync(command.ClientId, command.ConsultationId, ct);
        if (consultation is null)
            return SelectIntlVetOutcome.NotFound;

        var provider = await _db.Groomers.AsNoTracking().FirstOrDefaultAsync(g =>
            g.Id == command.ProviderId
            && g.VetProviderKind == VetProviderKind.InternationalAdvisor
            && g.IsActive
            && g.PublishStatus == BusinessPublishStatus.Approved, ct);
        if (provider is null)
            return SelectIntlVetOutcome.ProviderUnavailable;

        consultation.ProviderId = provider.Id;
        consultation.ContextCountry = provider.LicenseCountry;
        consultation.ServiceCatalogCode = ServiceCatalogCodes.VetIntl30;
        consultation.Status = ConsultationStatus.ProviderSelected;
        consultation.UsesCareBenefit = await _care.HasQuickConsultAvailableAsync(command.ClientId, ct);

        await _db.TouchAsync(consultation, ct);
        await _audit.LogAsync("profile_viewed", command.ClientId, "Groomer", provider.Id, ct: ct);
        return SelectIntlVetOutcome.Selected;
    }
}
