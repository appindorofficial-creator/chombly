using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using WebAppPet.Application.Businesses.GetAvailability;
using WebAppPet.Application.Businesses.SaveWeeklySchedule;
using WebAppPet.Application.Businesses.Shared;
using WebAppPet.Application.Businesses.ToggleAvailabilityDay;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Groomer;

public class AvailabilityModel : GroomerPageModel
{
    private readonly AvailabilityService _availability;
    private readonly GetAvailabilityHandler _getAvailability;
    private readonly SaveWeeklyScheduleHandler _saveWeek;
    private readonly ToggleAvailabilityDayHandler _toggleDay;
    private readonly IStringLocalizer<SharedResource> _L;

    public AvailabilityModel(
        AppDbContext db,
        AuthService auth,
        AvailabilityService availability,
        GetAvailabilityHandler getAvailability,
        SaveWeeklyScheduleHandler saveWeek,
        ToggleAvailabilityDayHandler toggleDay,
        IStringLocalizer<SharedResource> L)
        : base(db, auth)
    {
        _availability = availability;
        _getAvailability = getAvailability;
        _saveWeek = saveWeek;
        _toggleDay = toggleDay;
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

        var result = await _saveWeek.HandleAsync(new SaveWeeklyScheduleCommand(Profile!.Id, WeekEdit));
        Message = result.Success
            ? CatalogLocalizer.Loc(
                "Horario semanal guardado y agenda de 60 días actualizada.",
                "Weekly hours saved and 60-day agenda updated.")
            : result.Error;
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        if (await LoadGroomerAsync() is IActionResult r) return r;
        await _toggleDay.HandleAsync(new ToggleAvailabilityDayCommand(Profile!.Id, id));
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRegenerateAsync()
    {
        if (await LoadGroomerAsync() is IActionResult r) return r;
        var n = await _availability.GenerateFromWeeklyAsync(Profile!.Id, SaveWeeklyScheduleHandler.AgendaDays, replaceExisting: true);
        Message = n == 0
            ? CatalogLocalizer.Loc(
                "Primero guarda un horario semanal.",
                "Save a weekly schedule first.")
            : CatalogLocalizer.Loc(
                $"Agenda regenerada ({n} días) según tu horario semanal.",
                $"Agenda regenerated ({n} days) from your weekly hours.");
        await LoadAsync();
        return Page();
    }

    private async Task LoadAsync()
    {
        var view = await _getAvailability.HandleAsync(new GetAvailabilityQuery(Profile!.Id));
        Week = view.Week;
        Days = view.Days;
        WeekEdit = Week.Select(w => new WeekDayInput
        {
            DayOfWeek = w.DayOfWeek,
            Label = w.Label,
            IsOpen = w.IsOpen,
            OpenTime = w.OpenTime,
            CloseTime = w.CloseTime
        }).ToList();
        EnsureWeekLabels(WeekEdit);
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
