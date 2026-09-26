using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.Consultations.GetLocalVets;
using WebAppPet.Application.Consultations.SelectLocalVet;
using WebAppPet.Models;
using WebAppPet.Services;
using WebAppPet.Ui;

namespace WebAppPet.Pages.Vet.Virtual;

public class ProvidersModel : PageModel
{
    private readonly AuthService _auth;
    private readonly GetLocalVetsHandler _getLocalVets;
    private readonly SelectLocalVetHandler _selectLocalVet;

    public ProvidersModel(AuthService auth, GetLocalVetsHandler getLocalVets, SelectLocalVetHandler selectLocalVet)
    {
        _auth = auth;
        _getLocalVets = getLocalVets;
        _selectLocalVet = selectLocalVet;
    }

    [BindProperty(SupportsGet = true)]
    public int ConsultationId { get; set; }

    [BindProperty]
    public int ProviderId { get; set; }

    [BindProperty]
    public string Slot { get; set; } = "";

    [BindProperty]
    public string When { get; set; } = "";

    public Consultation? Consultation { get; set; }
    public ServiceCatalogItem? CatalogItem { get; set; }
    public List<GroomerProfile> Providers { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public List<string> TimeSlots { get; } = new()
    {
        "9:00 AM", "10:00 AM", "10:30 AM", "11:30 AM", "1:00 PM", "2:00 PM", "3:00 PM", "4:00 PM", "5:00 PM"
    };

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Vet/Virtual/Providers?consultationId={ConsultationId}" });

        var options = await _getLocalVets.HandleAsync(new GetLocalVetsQuery(userId, ConsultationId));
        if (options.Redirect is { } step)
            return this.RedirectToStep(step, ConsultationId);

        Show(options);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (_auth.CurrentUserId is not int userId) return RedirectToPage("/Vet/Index");

        var result = await _selectLocalVet.HandleAsync(new SelectLocalVetCommand(userId, ConsultationId, ProviderId, Slot, When));
        if (result.NextStep is { } step)
            return this.RedirectToStep(step, ConsultationId);

        Show(result.Options!);
        return Page();
    }

    private void Show(LocalVetOptions options)
    {
        Consultation = options.Consultation;
        CatalogItem = options.CatalogItem;
        Providers = options.Providers;
    }
}
