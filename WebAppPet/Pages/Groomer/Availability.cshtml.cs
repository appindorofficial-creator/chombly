using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Groomer;

public class AvailabilityModel : GroomerPageModel
{
    private readonly AvailabilityService _availability;
    private readonly IStringLocalizer<SharedResource> _L;

    public AvailabilityModel(
        AppDbContext db,
        AuthService auth,
        AvailabilityService availability,
        IStringLocalizer<SharedResource> L)
        : base(db, auth)
    {
        _availability = availability;
        _L = L;
    }

    public List<BusinessDayAvailability> Days { get; set; } = new();
    public List<WeekDayInput> Week { get; set; } = WeekDayInput.DefaultWeek();
    public string? Message { get; set; }

    [BindProperty]
    public List<WeekDayInput> WeekEdit { get; set; } = WeekDayInput.DefaultWeek();

    public async Task<IActionResult> OnGetAsync()
    {
        if (await LoadGroomerAsync() is IActionResult r) return r;
        await _availability.EnsureUpcomingDayAgendaForAsync(Profile!.Id);
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostSaveWeekAsync()
    {
        if (await LoadGroomerAsync() is IActionResult r) return r;
        EnsureWeekLabels(WeekEdit);
        if (!WeekEdit.Any(d => d.IsOpen))
        {
            Message = CatalogLocalizer.Loc(
                "Debes tener al menos un día abierto.",
                "Open at least one day in your schedule.");
            await LoadAsync();
            return Page();
        }

        await _availability.SaveWeeklyAndGenerateAsync(Profile!.Id, WeekEdit, days: 60);
        Message = CatalogLocalizer.Loc(
            "Horario semanal guardado y agenda de 60 días actualizada.",
            "Weekly hours saved and 60-day agenda updated.");
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        if (await LoadGroomerAsync() is IActionResult r) return r;
        var row = await Db.DayAvailabilities.FirstOrDefaultAsync(a => a.Id == id && a.GroomerId == Profile!.Id);
        if (row != null)
        {
            row.IsAvailable = !row.IsAvailable;
            row.Note = row.IsAvailable
                ? CatalogLocalizer.Loc("Abierto (manual)", "Open (manual)")
                : CatalogLocalizer.Loc("Cerrado (manual)", "Closed (manual)");
            await Db.SaveChangesAsync();
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRegenerateAsync()
    {
        if (await LoadGroomerAsync() is IActionResult r) return r;
        var n = await _availability.GenerateFromWeeklyAsync(Profile!.Id, 60, replaceExisting: true);
        if (n == 0)
        {
            Message = CatalogLocalizer.Loc(
                "Primero guarda un horario semanal.",
                "Save a weekly schedule first.");
        }
        else
        {
            Message = CatalogLocalizer.Loc(
                $"Agenda regenerada ({n} días) según tu horario semanal.",
                $"Agenda regenerated ({n} days) from your weekly hours.");
        }
        await LoadAsync();
        return Page();
    }

    private async Task LoadAsync()
    {
        var hours = await Db.WeeklyHours
            .Where(h => h.GroomerId == Profile!.Id)
            .OrderBy(h => h.DayOfWeek)
            .ToListAsync();

        Week = WeekDayInput.DefaultWeek();
        if (hours.Count > 0)
        {
            foreach (var h in hours)
            {
                var row = Week.FirstOrDefault(w => w.DayOfWeek == h.DayOfWeek);
                if (row == null) continue;
                row.IsOpen = h.IsOpen;
                row.OpenTime = h.OpenLabel;
                row.CloseTime = h.CloseLabel;
            }
        }
        WeekEdit = Week.Select(w => new WeekDayInput
        {
            DayOfWeek = w.DayOfWeek,
            Label = w.Label,
            IsOpen = w.IsOpen,
            OpenTime = w.OpenTime,
            CloseTime = w.CloseTime
        }).ToList();
        EnsureWeekLabels(WeekEdit);

        var start = AppTimeZones.TodayLocalDate();
        Days = await Db.DayAvailabilities
            .Where(a => a.GroomerId == Profile!.Id && a.Day >= start && a.Day < start.AddDays(30))
            .OrderBy(a => a.Day)
            .ToListAsync();
    }

    private void EnsureWeekLabels(List<WeekDayInput> week)
    {
        var names = new[]
        {
            _L["Day_Sunday"].Value,
            _L["Day_Monday"].Value,
            _L["Day_Tuesday"].Value,
            _L["Day_Wednesday"].Value,
            _L["Day_Thursday"].Value,
            _L["Day_Friday"].Value,
            _L["Day_Saturday"].Value
        };
        for (var i = 0; i < week.Count && i < 7; i++)
        {
            week[i].DayOfWeek = i;
            week[i].Label = names[i];
        }
    }
}
