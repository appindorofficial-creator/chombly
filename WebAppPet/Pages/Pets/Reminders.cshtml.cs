using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
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

    [BindProperty] public ReminderType Type { get; set; } = ReminderType.Vaccine;
    [BindProperty] public string Title { get; set; } = "";
    [BindProperty] public string? Notes { get; set; }
    [BindProperty] public int? FrequencyDays { get; set; } = 365;
    [BindProperty] public DateTime NextDueLocal { get; set; } = DateTime.Today.AddDays(7);
    [BindProperty] public string QuietStart { get; set; } = "21:00";
    [BindProperty] public string QuietEnd { get; set; } = "08:00";

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Pets/Reminders/{Id}" });
        if (!await LoadPetAsync()) return RedirectToPage("/Pets/Index");
        Schedules = await _reminders.ListForPetAsync(_auth.CurrentUserId.Value, Id);
        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!await LoadPetAsync()) return RedirectToPage("/Pets/Index");

        if (string.IsNullOrWhiteSpace(Title))
        {
            Title = Type switch
            {
                ReminderType.Vaccine => "Vaccine reminder",
                ReminderType.Medication => "Medication reminder",
                _ => "Care reminder"
            };
        }

        TimeSpan? qStart = ParseTime(QuietStart);
        TimeSpan? qEnd = ParseTime(QuietEnd);
        var nextUtc = AppTimeZones.LocalDateAndTimeToUtc(NextDueLocal.Date, TimeSpan.FromHours(9));

        await _reminders.CreateScheduleAsync(new ReminderSchedule
        {
            UserId = _auth.CurrentUserId!.Value,
            PetId = Id,
            Type = Type,
            Title = Title.Trim(),
            Notes = Notes,
            FrequencyDays = FrequencyDays is > 0 ? FrequencyDays : null,
            NextDueUtc = nextUtc,
            QuietHoursStartLocal = qStart,
            QuietHoursEndLocal = qEnd,
            TimeZoneId = "America/New_York",
            Channel = ReminderChannel.InApp
        });

        Message = "Reminder created.";
        Schedules = await _reminders.ListForPetAsync(_auth.CurrentUserId.Value, Id);
        return Page();
    }

    public async Task<IActionResult> OnPostDeactivateAsync(int scheduleId)
    {
        if (!await LoadPetAsync()) return RedirectToPage("/Pets/Index");
        await _reminders.DeactivateAsync(scheduleId, _auth.CurrentUserId!.Value);
        Message = "Reminder deactivated.";
        Schedules = await _reminders.ListForPetAsync(_auth.CurrentUserId.Value, Id);
        return Page();
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
