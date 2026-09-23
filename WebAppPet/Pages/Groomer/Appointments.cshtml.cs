using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Groomer;

public class AppointmentsModel : GroomerPageModel
{
    private readonly IStringLocalizer<SharedResource> _L;

    public AppointmentsModel(AppDbContext db, AuthService auth, IStringLocalizer<SharedResource> L)
        : base(db, auth)
    {
        _L = L;
    }

    [BindProperty(SupportsGet = true)]
    public string Tab { get; set; } = "requests";

    public List<Appointment> Items { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (await LoadGroomerAsync() is IActionResult redirect) return redirect;

        var query = Db.Appointments
            .Include(a => a.Pet)
            .Include(a => a.Service)
            .Include(a => a.Client)
            .Where(a => a.GroomerId == Profile!.Id);

        Items = Tab switch
        {
            "upcoming" => await query
                .Where(a => a.Status == AppointmentStatus.Confirmed)
                .OrderBy(a => a.ScheduledAt)
                .ToListAsync(),
            "history" => await query
                .Where(a => a.Status == AppointmentStatus.Completed || a.Status == AppointmentStatus.Cancelled)
                .OrderByDescending(a => a.ScheduledAt)
                .ToListAsync(),
            _ => await query
                .Where(a => a.Status == AppointmentStatus.Pending)
                .OrderBy(a => a.ScheduledAt)
                .ToListAsync()
        };

        return Page();
    }

    public Task<IActionResult> OnPostAcceptAsync(int id, string tab) =>
        ChangeStatusAsync(id, tab, AppointmentStatus.Pending, AppointmentStatus.Confirmed,
            CatalogLocalizer.Loc("¡Cita confirmada!", "Booking confirmed!"),
            CatalogLocalizer.Loc("confirmó tu cita", "confirmed your booking"));

    public Task<IActionResult> OnPostRejectAsync(int id, string tab) =>
        ChangeStatusAsync(id, tab, AppointmentStatus.Pending, AppointmentStatus.Cancelled,
            CatalogLocalizer.Loc("Cita rechazada", "Booking declined"),
            CatalogLocalizer.Loc("no pudo aceptar tu cita", "could not accept your booking"));

    public Task<IActionResult> OnPostCompleteAsync(int id, string tab, string? clinicalNotes) =>
        ChangeStatusAsync(id, tab, AppointmentStatus.Confirmed, AppointmentStatus.Completed,
            CatalogLocalizer.Loc("Servicio completado", "Service completed"),
            CatalogLocalizer.Loc(
                "completó el servicio. ¡Cuéntanos cómo quedó tu mascota!",
                "completed the service. Tell us how your pet looks!"),
            clinicalNotes);

    public async Task<IActionResult> OnPostSaveNoteAsync(int id, string tab, string? clinicalNotes)
    {
        if (await LoadGroomerAsync() is IActionResult redirect) return redirect;

        var note = clinicalNotes?.Trim();
        if (string.IsNullOrWhiteSpace(note))
            return RedirectToPage(new { tab });

        var appt = await Db.Appointments
            .FirstOrDefaultAsync(a => a.Id == id && a.GroomerId == Profile!.Id);
        if (appt is null) return RedirectToPage(new { tab });

        await ApplyClinicalNoteAsync(appt, note, notifyClient: true);
        await Db.SaveChangesAsync();
        return RedirectToPage(new { tab });
    }

    private async Task<IActionResult> ChangeStatusAsync(
        int id, string tab, AppointmentStatus from, AppointmentStatus to, string title, string messageSuffix,
        string? clinicalNotes = null)
    {
        if (await LoadGroomerAsync() is IActionResult redirect) return redirect;

        var appt = await Db.Appointments
            .Include(a => a.Groomer)
            .FirstOrDefaultAsync(a => a.Id == id && a.GroomerId == Profile!.Id);

        if (appt != null && appt.Status == from)
        {
            appt.Status = to;
            var note = clinicalNotes?.Trim();
            if (!string.IsNullOrWhiteSpace(note))
                await ApplyClinicalNoteAsync(appt, note, notifyClient: true);
            else if (to == AppointmentStatus.Completed)
                await MarkConsultationCompletedAsync(appt);

            Db.Notifications.Add(new AppNotification
            {
                UserId = appt.ClientId,
                Title = title,
                Message = $"{appt.Groomer.BusinessName} {messageSuffix} ({appt.ScheduledAt:g}).",
                Type = "appointment"
            });
            await Db.SaveChangesAsync();
        }

        return RedirectToPage(new { tab });
    }

    private async Task ApplyClinicalNoteAsync(Appointment appt, string note, bool notifyClient)
    {
        appt.Notes = MergeClinicalNotes(appt.Notes, note);

        var consult = await FindLinkedConsultationAsync(appt);
        if (consult != null)
        {
            consult.ClinicalNotes = note;
            consult.UpdatedAt = DateTime.UtcNow;
            consult.Status = ConsultationStatus.FollowUpOpen;
        }

        if (!notifyClient) return;

        Db.Notifications.Add(new AppNotification
        {
            UserId = appt.ClientId,
            Title = CatalogLocalizer.Loc("Nota clínica disponible", "Clinical note available"),
            Message = CatalogLocalizer.Loc(
                $"{Profile!.BusinessName} guardó una nota en el historial de tu mascota.",
                $"{Profile!.BusinessName} saved a note to your pet’s history."),
            Type = "vet-followup",
            CreatedAt = DateTime.UtcNow
        });
    }

    private async Task MarkConsultationCompletedAsync(Appointment appt)
    {
        var consult = await FindLinkedConsultationAsync(appt);
        if (consult is null) return;
        if (consult.Status is ConsultationStatus.Scheduled or ConsultationStatus.InProgress or ConsultationStatus.ProviderSelected)
        {
            consult.Status = ConsultationStatus.Completed;
            consult.UpdatedAt = DateTime.UtcNow;
        }
    }

    private async Task<Consultation?> FindLinkedConsultationAsync(Appointment appt)
    {
        var byAppt = await Db.Consultations
            .FirstOrDefaultAsync(c => c.AppointmentId == appt.Id && c.ProviderId == Profile!.Id);
        if (byAppt != null) return byAppt;

        return await Db.Consultations
            .FirstOrDefaultAsync(c =>
                c.ClientId == appt.ClientId &&
                c.PetId == appt.PetId &&
                c.ProviderId == appt.GroomerId &&
                c.Status >= ConsultationStatus.Scheduled);
    }

    private static string MergeClinicalNotes(string? existing, string clinical)
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

    public string StatusLabel(AppointmentStatus s) => s switch
    {
        AppointmentStatus.Pending => _L["Appt_Status_Pending"].Value,
        AppointmentStatus.Confirmed => _L["Appt_Status_Confirmed"].Value,
        AppointmentStatus.Completed => _L["Appt_Status_Completed"].Value,
        AppointmentStatus.Cancelled => _L["Appt_Status_Cancelled"].Value,
        _ => s.ToString()
    };

    public string StatusBadge(AppointmentStatus s) => s switch
    {
        AppointmentStatus.Pending => "badge-orange",
        AppointmentStatus.Confirmed => "badge-green",
        AppointmentStatus.Completed => "badge-purple",
        _ => "badge-gray"
    };
}
