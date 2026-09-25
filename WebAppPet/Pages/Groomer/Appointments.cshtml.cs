using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using WebAppPet.Application.Bookings.SaveClinicalNote;
using WebAppPet.Application.Bookings.UpdateBookingStatus;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Groomer;

public class AppointmentsModel : GroomerPageModel
{
    private readonly IStringLocalizer<SharedResource> _L;
    private readonly UpdateBookingStatusHandler _updateStatus;
    private readonly SaveClinicalNoteHandler _saveNote;

    public AppointmentsModel(
        AppDbContext db,
        AuthService auth,
        IStringLocalizer<SharedResource> L,
        UpdateBookingStatusHandler updateStatus,
        SaveClinicalNoteHandler saveNote)
        : base(db, auth)
    {
        _L = L;
        _updateStatus = updateStatus;
        _saveNote = saveNote;
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
        ChangeStatusAsync(id, tab, BookingStatusAction.Accept);

    public Task<IActionResult> OnPostRejectAsync(int id, string tab) =>
        ChangeStatusAsync(id, tab, BookingStatusAction.Reject);

    public Task<IActionResult> OnPostCompleteAsync(int id, string tab, string? clinicalNotes) =>
        ChangeStatusAsync(id, tab, BookingStatusAction.Complete, clinicalNotes);

    public async Task<IActionResult> OnPostSaveNoteAsync(int id, string tab, string? clinicalNotes)
    {
        if (await LoadGroomerAsync() is IActionResult redirect) return redirect;

        await _saveNote.HandleAsync(new SaveClinicalNoteCommand(Profile!.Id, id, clinicalNotes));
        return RedirectToPage(new { tab });
    }

    private async Task<IActionResult> ChangeStatusAsync(
        int id, string tab, BookingStatusAction action, string? clinicalNotes = null)
    {
        if (await LoadGroomerAsync() is IActionResult redirect) return redirect;

        await _updateStatus.HandleAsync(new UpdateBookingStatusCommand(Profile!.Id, id, action, clinicalNotes));
        return RedirectToPage(new { tab });
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
