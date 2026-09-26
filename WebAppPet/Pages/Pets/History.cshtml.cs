using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.Pets.GetPetHistory;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Pets;

public class HistoryModel : PageModel
{
    private readonly AuthService _auth;
    private readonly GetPetHistoryHandler _getHistory;

    public HistoryModel(AuthService auth, GetPetHistoryHandler getHistory)
    {
        _auth = auth;
        _getHistory = getHistory;
    }

    public Pet? Pet { get; set; }
    public List<Appointment> Items { get; set; } = new();
    public List<Consultation> Consults { get; set; } = new();
    public string BackHref { get; private set; } = "/Pets";

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login");

        var history = await _getHistory.HandleAsync(new GetPetHistoryQuery(userId, id));
        if (history is null)
            return RedirectToPage("./Index");

        BackHref = !string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl)
            ? ReturnUrl!
            : Url.Page("./Index") ?? "/Pets";

        Pet = history.Pet;
        Items = history.Appointments;
        Consults = history.Consultations;
        return Page();
    }
}
