using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Pets;

public class RemindersModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly ReminderEngineService _reminders;

    public RemindersModel(AppDbContext db, AuthService auth, ReminderEngineService reminders)
    {
        _db = db;
        _auth = auth;
        _reminders = reminders;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    public Pet? Pet { get; set; }
    public List<ReminderSchedule> Schedules { get; set; } = new();
    public string? Message { get; set; }
    public string? Error { get; set; }

    [BindProperty] public ReminderType? Type { get; set; }
    [BindProperty] public string Title { get; set; } = "";
    [BindProperty] public string? Notes { get; set; }
    [BindProperty] public int? FrequencyDays { get; set; }
    [BindProperty] public DateTime? NextDueLocal { get; set; }
    [BindProperty] public string QuietStart { get; set; } = "";
    [BindProperty] public string QuietEnd { get; set; } = "";

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Pets/Reminders/{Id}" });
        if (!await LoadPetAsync()) return RedirectToPage("/Pets/Index");
        Schedules = await _reminders.ListForPetAsync(_auth.CurrentUserId.Value, Id);
        if (TempData["Flash"] is string flash)
            Message = flash;
        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!await LoadPetAsync()) return RedirectToPage("/Pets/Index");

        if (Type is null)
        {
            Error = CatalogLocalizer.Loc("Elige un tipo de recordatorio.", "Choose a reminder type.");
            Schedules = await _reminders.ListForPetAsync(_auth.CurrentUserId!.Value, Id);
            return Page();
        }

        if (NextDueLocal is null)
        {
            Error = CatalogLocalizer.Loc("Elige la próxima fecha.", "Choose the next due date.");
            Schedules = await _reminders.ListForPetAsync(_auth.CurrentUserId!.Value, Id);
            return Page();
        }

        var type = Type.Value;
        if (string.IsNullOrWhiteSpace(Title))
        {
            Title = type switch
            {
                ReminderType.Vaccine => "Recordatorio de vacuna",
                ReminderType.Medication => "Recordatorio de medicamento",
                ReminderType.Appointment => "Recordatorio de cita",
                _ => "Recordatorio de cuidado"
            };
        }

        TimeSpan? qStart = ParseTime(QuietStart);
        TimeSpan? qEnd = ParseTime(QuietEnd);
        var nextUtc = AppTimeZones.LocalDateAndTimeToUtc(NextDueLocal.Value.Date, TimeSpan.FromHours(9));

        await _reminders.CreateScheduleAsync(new ReminderSchedule
        {
            UserId = _auth.CurrentUserId!.Value,
            PetId = Id,
            Type = type,
            Title = Title.Trim(),
            Notes = Notes,
            FrequencyDays = FrequencyDays is > 0 ? FrequencyDays : null,
            NextDueUtc = nextUtc,
            QuietHoursStartLocal = qStart,
            QuietHoursEndLocal = qEnd,
            TimeZoneId = "America/New_York",
            Channel = ReminderChannel.InApp
        });

        TempData["Flash"] = CatalogLocalizer.Loc("Recordatorio creado.", "Reminder created.");
        return RedirectToPage(new { Id });
    }

    public async Task<IActionResult> OnPostDeactivateAsync(int scheduleId)
    {
        if (!await LoadPetAsync()) return RedirectToPage("/Pets/Index");
        await _reminders.DeactivateAsync(scheduleId, _auth.CurrentUserId!.Value);
        TempData["Flash"] = CatalogLocalizer.Loc("Recordatorio desactivado.", "Reminder deactivated.");
        return RedirectToPage(new { Id });
    }

    private async Task<bool> LoadPetAsync()
    {
        if (_auth.CurrentUserId is null) return false;
        Pet = await _db.Pets.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == Id && p.OwnerId == _auth.CurrentUserId);
        return Pet != null;
    }

    private static TimeSpan? ParseTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var ts)) return ts;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt.TimeOfDay;
        return null;
    }
}
