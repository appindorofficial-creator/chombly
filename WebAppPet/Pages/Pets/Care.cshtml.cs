using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.Care.GetPetCare;
using WebAppPet.Application.Reminders.CreateCheckupReminder;
using WebAppPet.Domain;
using WebAppPet.Infrastructure.Identity;
using WebAppPet.Localization;

namespace WebAppPet.Pages.Pets;

public class CareModel : PageModel
{
    private readonly AuthService _auth;
    private readonly GetPetCareHandler _getPetCare;
    private readonly CreateCheckupReminderHandler _createCheckupReminder;

    public CareModel(AuthService auth, GetPetCareHandler getPetCare, CreateCheckupReminderHandler createCheckupReminder)
    {
        _auth = auth;
        _getPetCare = getPetCare;
        _createCheckupReminder = createCheckupReminder;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    public Pet? Pet { get; set; }
    public CareSubscription? Subscription { get; set; }
    public int RemainingConsults { get; set; }
    public List<Consultation> RecentConsults { get; set; } = new();
    public List<Appointment> Upcoming { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Pets/Care/{Id}" });

        var view = await _getPetCare.HandleAsync(new GetPetCareQuery(userId, Id), ct);
        if (view is null) return RedirectToPage("/Pets/Index");

        Pet = view.Pet;
        Subscription = view.Subscription;
        RemainingConsults = view.RemainingConsults;
        RecentConsults = view.RecentConsults;
        Upcoming = view.Upcoming;
        return Page();
    }

    public async Task<IActionResult> OnPostRemindAsync(CancellationToken ct)
    {
        if (_auth.CurrentUserId is not int userId) return RedirectToPage("/Account/Login");

        var outcome = await _createCheckupReminder.HandleAsync(new CreateCheckupReminderCommand(userId, Id), ct);
        if (outcome == CreateCheckupReminderOutcome.PetNotFound)
            return RedirectToPage("/Pets/Index");

        TempData["Flash"] = outcome == CreateCheckupReminderOutcome.AlreadyActive
            ? CatalogLocalizer.Loc(
                "Ya tienes un aviso de vacunas/chequeo activo para esta mascota.",
                "You already have an active vaccine/checkup reminder for this pet.")
            : CatalogLocalizer.Loc(
                "Recordatorio de vacunas/chequeo creado.",
                "Vaccine/checkup reminder created.");
        return RedirectToPage("/Pets/Reminders", new { id = Id });
    }
}
