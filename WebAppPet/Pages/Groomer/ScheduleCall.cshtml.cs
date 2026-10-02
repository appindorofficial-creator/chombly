using Microsoft.AspNetCore.Mvc;
using WebAppPet.Application.Businesses.ScheduleSupportCall;
using WebAppPet.Domain.Markets;
using WebAppPet.Infrastructure.Identity;
using WebAppPet.Infrastructure.Persistence;
using WebAppPet.Localization;

namespace WebAppPet.Pages.Groomer;

public class ScheduleCallModel : GroomerPageModel
{
    private readonly ScheduleSupportCallHandler _scheduleCall;

    public ScheduleCallModel(AppDbContext db, AuthService auth, ScheduleSupportCallHandler scheduleCall)
        : base(db, auth)
    {
        _scheduleCall = scheduleCall;
    }

    public List<DateTime> Days { get; set; } = new();
    public List<string> Slots { get; } = new() { "10:00 AM", "11:00 AM", "1:00 PM", "2:00 PM", "3:00 PM", "4:00 PM" };

    [BindProperty]
    public string? Day { get; set; }

    [BindProperty]
    public string Slot { get; set; } = "10:00 AM";

    public string? Message { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (await LoadGroomerAsync() is IActionResult r) return r;
        BuildDays();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (await LoadGroomerAsync() is IActionResult r) return r;
        BuildDays();

        var result = await _scheduleCall.HandleAsync(new ScheduleSupportCallCommand(Profile!.Id, Day, Slot));
        switch (result.Error)
        {
            case ScheduleSupportCallError.InvalidDay:
                ErrorMessage = CatalogLocalizer.Loc("Elige un día.", "Pick a day.");
                return Page();
            case ScheduleSupportCallError.InvalidSlot:
                ErrorMessage = CatalogLocalizer.Loc("Horario inválido.", "Invalid time slot.");
                return Page();
            case ScheduleSupportCallError.NotFound:
                return RedirectToPage("/Account/RegisterBusiness");
        }

        var when = result.When!.Value;
        Message = CatalogLocalizer.Loc(
            $"Llamada confirmada: {when:ddd d MMM · h:mm tt}",
            $"Call confirmed: {when:ddd d MMM · h:mm tt}");
        return Page();
    }

    private void BuildDays()
    {
        Days = Enumerable.Range(1, 7).Select(i => AppTimeZones.TodayLocalDate().AddDays(i)).ToList();
        if (string.IsNullOrEmpty(Day)) Day = Days[0].ToString("yyyy-MM-dd");
    }
}
