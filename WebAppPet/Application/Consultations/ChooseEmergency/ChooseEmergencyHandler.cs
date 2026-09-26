using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Data;
using WebAppPet.Services;

namespace WebAppPet.Application.Consultations.ChooseEmergency;

/// <summary>Records that the client chose emergency care after the warning signs.</summary>
public class ChooseEmergencyHandler
{
    private readonly AppDbContext _db;
    private readonly VetAuditService _audit;

    public ChooseEmergencyHandler(AppDbContext db, VetAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    /// <returns>False when the consultation is not the client's.</returns>
    public async Task<bool> HandleAsync(ChooseEmergencyCommand command, CancellationToken ct = default)
    {
        var consultation = await _db.OwnedConsultationAsync(command.ClientId, command.ConsultationId, ct);
        if (consultation is null)
            return false;

        await _audit.LogAsync("safety_chose_emergency", command.ClientId, "Consultation", consultation.Id,
            new { consultationId = command.ConsultationId }, ct);
        return true;
    }
}
