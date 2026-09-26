using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.Consultations.ChooseConsultationService;
using WebAppPet.Application.Consultations.GetConsultationServices;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;
using WebAppPet.Ui;

namespace WebAppPet.Pages.Vet.Virtual;

public class ServiceModel : PageModel
{
    private readonly AuthService _auth;
    private readonly GetConsultationServicesHandler _getServices;
    private readonly ChooseConsultationServiceHandler _choose;

    public ServiceModel(AuthService auth, GetConsultationServicesHandler getServices, ChooseConsultationServiceHandler choose)
    {
        _auth = auth;
        _getServices = getServices;
        _choose = choose;
    }

    [BindProperty(SupportsGet = true)]
    public int ConsultationId { get; set; }

    [BindProperty]
    public string? ServiceCode { get; set; }

    public Consultation? Consultation { get; set; }
    public List<ServiceCatalogItem> Items { get; set; } = new();
    public bool HasCare { get; set; }
    public int CareRemaining { get; set; }
    public string? ErrorMessage { get; set; }
    public string HomeCountryCode { get; set; } = MarketCountry.DefaultIso;
    public bool ShowUsLocal => MarketCountry.AllowsUsLocalTeleconsult(HomeCountryCode);

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Vet/Virtual/Service?consultationId={ConsultationId}" });

        return await LoadAsync(userId) ? Page() : RedirectToPage("/Vet/Index");
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (_auth.CurrentUserId is not int userId) return RedirectToPage("/Vet/Index");

        var result = await _choose.HandleAsync(new ChooseConsultationServiceCommand(userId, ConsultationId, ServiceCode));
        switch (result.Outcome)
        {
            case ChooseConsultationServiceOutcome.NotFound:
                return RedirectToPage("/Vet/Index");
            case ChooseConsultationServiceOutcome.Moved:
                return this.RedirectToStep(result.NextStep!.Value, ConsultationId);
            default:
                if (!await LoadAsync(userId)) return RedirectToPage("/Vet/Index");
                ErrorMessage = CatalogLocalizer.Loc("Elige un servicio reservable.", "Choose a bookable service.");
                return Page();
        }
    }

    public async Task<IActionResult> OnPostUseCareAsync()
    {
        if (_auth.CurrentUserId is not int userId) return RedirectToPage("/Account/Login");

        var result = await _choose.HandleAsync(new ChooseConsultationServiceCommand(userId, ConsultationId, null, UseCare: true));
        return result.Outcome == ChooseConsultationServiceOutcome.Moved
            ? this.RedirectToStep(result.NextStep!.Value, ConsultationId)
            : RedirectToPage("/Vet/Index");
    }

    private async Task<bool> LoadAsync(int userId)
    {
        var services = await _getServices.HandleAsync(new GetConsultationServicesQuery(userId, ConsultationId));
        if (services is null) return false;

        Consultation = services.Consultation;
        Items = services.Items;
        HomeCountryCode = services.HomeCountryCode;
        HasCare = services.HasCare;
        CareRemaining = services.CareRemaining;
        return true;
    }
}
