using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Pets;

public class CareModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly ChomblyCareService _care;
    private readonly ReminderEngineService _reminders;

    public CareModel(AppDbContext db, AuthService auth, ChomblyCareService care, ReminderEngineService reminders)
    {
        _db = db;
        _auth = auth;
        _care = care;
        _reminders = reminders;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    public Pet? Pet { get; set; }
    public CareSubscription? Subscription { get; set; }
    public int RemainingConsults { get; set; }
    public List<Consultation> RecentConsults { get; set; } = new();
    public List<Appointment> Upcoming { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Pets/Care/{Id}" });

        Pet = await _db.Pets.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == Id && p.OwnerId == _auth.CurrentUserId);
        if (Pet is null) return RedirectToPage("/Pets/Index");

        Subscription = await _care.GetActiveAsync(_auth.CurrentUserId.Value);
        if (Subscription != null)
            RemainingConsults = _care.RemainingQuickConsults(Subscription);

        RecentConsults = await _db.Consultations.AsNoTracking()
            .Include(c => c.Provider)
            .Where(c => c.ClientId == _auth.CurrentUserId && c.PetId == Id)
            .OrderByDescending(c => c.UpdatedAt)
            .Take(8)
            .ToListAsync();

        var now = DateTime.UtcNow;
        Upcoming = await _db.Appointments.AsNoTracking()
            .Include(a => a.Groomer)
            .Include(a => a.Service)
            .Where(a => a.ClientId == _auth.CurrentUserId && a.PetId == Id &&
                        a.ScheduledAt >= now && a.Status != AppointmentStatus.Cancelled)
            .OrderBy(a => a.ScheduledAt)
            .Take(5)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostRemindAsync()
    {
        if (_auth.CurrentUserId is null) return RedirectToPage("/Account/Login");

        Pet = await _db.Pets.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == Id && p.OwnerId == _auth.CurrentUserId);
        if (Pet is null) return RedirectToPage("/Pets/Index");

        var nextLocal = AppTimeZones.TodayLocalDate().AddDays(7);
        var nextUtc = AppTimeZones.LocalDateAndTimeToUtc(nextLocal, TimeSpan.FromHours(9));

        await _reminders.CreateScheduleAsync(new ReminderSchedule
        {
            UserId = _auth.CurrentUserId.Value,
            PetId = Id,
            Type = ReminderType.Vaccine,
            Title = CatalogLocalizer.Loc(
                $"Vacunas / chequeo · {Pet.Name}",
                $"Vaccines / checkup · {Pet.Name}"),
            Notes = CatalogLocalizer.Loc(
                "Aviso creado desde Control. Ajusta fecha o frecuencia si lo necesitas.",
                "Created from Care. Adjust the date or frequency if needed."),
            FrequencyDays = 365,
            NextDueUtc = nextUtc,
            QuietHoursStartLocal = TimeSpan.FromHours(21),
            QuietHoursEndLocal = TimeSpan.FromHours(8),
            TimeZoneId = "America/New_York",
            Channel = ReminderChannel.InApp
        });

        TempData["Flash"] = CatalogLocalizer.Loc(
            "Recordatorio de vacunas/chequeo creado.",
            "Vaccine/checkup reminder created.");
        return RedirectToPage("/Pets/Reminders", new { id = Id });
    }
}
