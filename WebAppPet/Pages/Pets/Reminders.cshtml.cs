using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.Reminders.CreateReminder;
using WebAppPet.Application.Reminders.DeactivateReminder;
using WebAppPet.Application.Reminders.GetPetReminders;
using WebAppPet.Domain;
using WebAppPet.Infrastructure.Identity;
using WebAppPet.Localization;

namespace WebAppPet.Pages.Pets;

public class RemindersModel : PageModel
{
    private readonly AuthService _auth;
    private readonly GetPetRemindersHandler _getReminders;
    private readonly CreateReminderHandler _createReminder;
    private readonly DeactivateReminderHandler _deactivateReminder;

    public RemindersModel(
        AuthService auth,
        GetPetRemindersHandler getReminders,
        CreateReminderHandler createReminder,
        DeactivateReminderHandler deactivateReminder)
    {
        _auth = auth;
        _getReminders = getReminders;
        _createReminder = createReminder;
        _deactivateReminder = deactivateReminder;
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

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Pets/Reminders/{Id}" });
        if (!await LoadAsync(userId, ct)) return RedirectToPage("/Pets/Index");
        if (TempData["Flash"] is string flash)
            Message = flash;
        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken ct)
    {
        if (_auth.CurrentUserId is not int userId) return RedirectToPage("/Pets/Index");

        var outcome = await _createReminder.HandleAsync(new CreateReminderCommand(
            userId, Id, Type, Title, Notes, FrequencyDays, NextDueLocal, QuietStart, QuietEnd), ct);

        Error = outcome switch
        {
            CreateReminderOutcome.NoType => CatalogLocalizer.Loc("Elige un tipo de recordatorio.", "Choose a reminder type."),
            CreateReminderOutcome.NoDate => CatalogLocalizer.Loc("Elige la próxima fecha.", "Choose the next due date."),
            CreateReminderOutcome.PastDate => CatalogLocalizer.Loc(
                "La próxima fecha no puede ser en el pasado.",
                "The next due date can't be in the past."),
            _ => null
        };

        if (outcome == CreateReminderOutcome.PetNotFound)
            return RedirectToPage("/Pets/Index");
        if (Error != null)
            return await LoadAsync(userId, ct) ? Page() : RedirectToPage("/Pets/Index");

        TempData["Flash"] = CatalogLocalizer.Loc("Recordatorio creado.", "Reminder created.");
        return RedirectToPage(new { Id });
    }

    public async Task<IActionResult> OnPostDeactivateAsync(int scheduleId, CancellationToken ct)
    {
        if (_auth.CurrentUserId is not int userId || !await LoadAsync(userId, ct))
            return RedirectToPage("/Pets/Index");

        await _deactivateReminder.HandleAsync(new DeactivateReminderCommand(userId, scheduleId), ct);
        TempData["Flash"] = CatalogLocalizer.Loc("Recordatorio desactivado.", "Reminder deactivated.");
        return RedirectToPage(new { Id });
    }

    private async Task<bool> LoadAsync(int userId, CancellationToken ct)
    {
        var view = await _getReminders.HandleAsync(new GetPetRemindersQuery(userId, Id), ct);
        if (view is null) return false;
        Pet = view.Pet;
        Schedules = view.Schedules;
        return true;
    }
}
