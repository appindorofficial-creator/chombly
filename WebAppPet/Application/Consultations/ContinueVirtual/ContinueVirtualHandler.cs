using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Consultations.ContinueVirtual;

/// <summary>
/// The client saw the warning signs and still wants a virtual consultation.
/// Returns the pet step when the consultation has no pet yet.
/// </summary>
public class ContinueVirtualHandler
{
    private readonly AppDbContext _db;
    private readonly VetAuditService _audit;
    private readonly ConsultationRouter _router;

    public ContinueVirtualHandler(AppDbContext db, VetAuditService audit, ConsultationRouter router)
    {
        _db = db;
        _audit = audit;
        _router = router;
    }

    public async Task<ConsultationStep> HandleAsync(ContinueVirtualCommand command, CancellationToken ct = default)
    {
        var consultation = await _db.OwnedConsultationAsync(command.ClientId, command.ConsultationId, ct);
        if (consultation?.PetId is null)
            return ConsultationStep.Pet;

        var next = ConsultationPath.Normalize(command.Next, consultation.ServiceCatalogCode);
        if (consultation.Status == ConsultationStatus.EscalatedToEmergency)
            consultation.Status = ConsultationStatus.SafetyScreened;
        consultation.Modality = VetModality.Virtual;
        await _db.TouchAsync(consultation, ct);
        await _audit.LogAsync("safety_chose_continue_virtual", command.ClientId, "Consultation", consultation.Id,
            new { hasRedFlags = consultation.HasRedFlags, next }, ct);

        // This path has never looked up the client's country, so the local teleconsult falls back to guidance.
        return await _router.RouteAfterSafetyAsync(consultation, next, MarketCountry.DefaultIso, ct);
    }
}
