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

    public Task<IActionResult> OnPostCompleteAsync(int id, string tab) =>
        ChangeStatusAsync(id, tab, AppointmentStatus.Confirmed, AppointmentStatus.Completed,
            CatalogLocalizer.Loc("Servicio completado", "Service completed"),
            CatalogLocalizer.Loc(
                "completó el servicio. ¡Cuéntanos cómo quedó tu mascota!",
                "completed the service. Tell us how your pet looks!"));

    private async Task<IActionResult> ChangeStatusAsync(
        int id, string tab, AppointmentStatus from, AppointmentStatus to, string title, string messageSuffix)
    {
        if (await LoadGroomerAsync() is IActionResult redirect) return redirect;

        var appt = await Db.Appointments
            .Include(a => a.Groomer)
            .FirstOrDefaultAsync(a => a.Id == id && a.GroomerId == Profile!.Id);

        if (appt != null && appt.Status == from)
        {
            appt.Status = to;
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
