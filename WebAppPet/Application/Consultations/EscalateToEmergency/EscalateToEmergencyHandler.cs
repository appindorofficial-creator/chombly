using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Consultations.EscalateToEmergency;

/// <summary>
/// Lists emergency vet clinics in the home market and marks the signed-in client's consultation
/// (if any) as escalated to emergency.
/// </summary>
public class EscalateToEmergencyHandler
{
    private readonly AppDbContext _db;
    private readonly ChomblyCareService _care;
    private readonly VetAuditService _audit;

    public EscalateToEmergencyHandler(AppDbContext db, ChomblyCareService care, VetAuditService audit)
    {
        _db = db;
        _care = care;
        _audit = audit;
    }

    public async Task<EmergencyOptions> HandleAsync(EscalateToEmergencyCommand command, CancellationToken ct = default)
    {
        var hasCare = command.UserId is int userId && await _care.GetActiveAsync(userId, ct) != null;

        var clinics = await ClinicsAsync(g => g.OffersEmergency24x7 && (g.Category == null || g.Category.Slug == "vet"), command.HomeCountry, 20, ct);
        if (clinics.Count == 0)
            clinics = await ClinicsAsync(g => g.Category != null && g.Category.Slug == "vet", command.HomeCountry, 10, ct);

        Consultation? consultation = null;
        if (command.ConsultationId is int consultationId && command.UserId is int clientId)
            consultation = await _db.Consultations.FirstOrDefaultAsync(c => c.Id == consultationId && c.ClientId == clientId, ct);

        if (consultation != null)
        {
            consultation.Status = ConsultationStatus.EscalatedToEmergency;
            consultation.Modality = VetModality.Emergency;
            consultation.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        await _audit.LogAsync("escalated_emergency", command.UserId, "Consultation", consultation?.Id,
            new { clinics = clinics.Count, care = hasCare }, ct);

        return new EmergencyOptions(clinics, hasCare);
    }

    private async Task<List<GroomerProfile>> ClinicsAsync(
        System.Linq.Expressions.Expression<Func<GroomerProfile, bool>> filter, string homeCountry, int take, CancellationToken ct)
    {
        var clinics = await _db.Groomers.AsNoTracking()
            .Include(g => g.Category)
            .Where(g => g.IsActive && g.PublishStatus == BusinessPublishStatus.Approved)
            .Where(filter)
            .OrderByDescending(g => g.Rating)
            .ToListAsync(ct);
        return BusinessMarketResolver.FilterHomeMarket(clinics, homeCountry).Take(take).ToList();
    }
}
