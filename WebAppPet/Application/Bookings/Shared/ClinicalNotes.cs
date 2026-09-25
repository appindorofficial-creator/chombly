using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;

namespace WebAppPet.Application.Bookings.Shared;

/// <summary>Clinical notes written by vets on an appointment, mirrored to the linked consultation.</summary>
public class ClinicalNotes
{
    private readonly AppDbContext _db;

    public ClinicalNotes(AppDbContext db) => _db = db;

    /// <summary>Replaces any previous clinical note in the appointment notes and notifies the client. Caller saves.</summary>
    public async Task ApplyAsync(Appointment appt, string businessName, string note, CancellationToken ct = default)
    {
        appt.Notes = Merge(appt.Notes, note);

        var consult = await FindLinkedConsultationAsync(appt, ct);
        if (consult != null)
        {
            consult.ClinicalNotes = note;
            consult.UpdatedAt = DateTime.UtcNow;
            consult.Status = ConsultationStatus.FollowUpOpen;
        }

        _db.Notifications.Add(new AppNotification
        {
            UserId = appt.ClientId,
            Title = CatalogLocalizer.Loc("Nota clínica disponible", "Clinical note available"),
            Message = CatalogLocalizer.Loc(
                $"{businessName} guardó una nota en el historial de tu mascota.",
                $"{businessName} saved a note to your pet’s history."),
            Type = "vet-followup",
            CreatedAt = DateTime.UtcNow
        });
    }

    /// <summary>Caller saves.</summary>
    public async Task MarkConsultationCompletedAsync(Appointment appt, CancellationToken ct = default)
    {
        var consult = await FindLinkedConsultationAsync(appt, ct);
        if (consult is null) return;
        if (consult.Status is ConsultationStatus.Scheduled or ConsultationStatus.InProgress or ConsultationStatus.ProviderSelected)
        {
            consult.Status = ConsultationStatus.Completed;
            consult.UpdatedAt = DateTime.UtcNow;
        }
    }

    public static string Merge(string? existing, string clinical)
    {
        var kept = (existing ?? "")
            .Split(" · ", StringSplitOptions.None)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0
                && !p.StartsWith("Nota clínica:", StringComparison.OrdinalIgnoreCase)
                && !p.StartsWith("Clinical note:", StringComparison.OrdinalIgnoreCase)
                && !p.StartsWith("Seguimiento:", StringComparison.OrdinalIgnoreCase)
                && !p.StartsWith("Follow-up:", StringComparison.OrdinalIgnoreCase))
            .ToList();

        kept.Add(CatalogLocalizer.Loc($"Nota clínica: {clinical}", $"Clinical note: {clinical}"));
        return string.Join(" · ", kept);
    }

    private async Task<Consultation?> FindLinkedConsultationAsync(Appointment appt, CancellationToken ct)
    {
        var byAppt = await _db.Consultations
            .FirstOrDefaultAsync(c => c.AppointmentId == appt.Id && c.ProviderId == appt.GroomerId, ct);
        if (byAppt != null) return byAppt;

        return await _db.Consultations
            .FirstOrDefaultAsync(c =>
                c.ClientId == appt.ClientId &&
                c.PetId == appt.PetId &&
                c.ProviderId == appt.GroomerId &&
                c.Status >= ConsultationStatus.Scheduled, ct);
    }
}
