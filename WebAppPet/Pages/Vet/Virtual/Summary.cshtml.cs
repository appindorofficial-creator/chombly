using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.Consultations.GetConsultationSummary;
using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Models;
using WebAppPet.Services;
using WebAppPet.Ui;

namespace WebAppPet.Pages.Vet.Virtual;

public class SummaryModel : PageModel
{
    private readonly AuthService _auth;
    private readonly GetConsultationSummaryHandler _getSummary;

    public SummaryModel(AuthService auth, GetConsultationSummaryHandler getSummary)
    {
        _auth = auth;
        _getSummary = getSummary;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public Consultation? Consultation { get; set; }
    public ServiceCatalogItem? CatalogItem { get; set; }
    public string BackHref { get; private set; } = "/Appointments";
    public string? PetHistoryHref { get; private set; }
    public bool UsingCare { get; private set; }
    public bool IsIntl { get; private set; }
    public bool HasProviderNotes { get; private set; }
    public string HomeCountryCode { get; private set; } = MarketCountry.DefaultIso;

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Vet/Virtual/Summary/{Id}" });

        var summary = await _getSummary.HandleAsync(new GetConsultationSummaryQuery(userId, Id));
        if (summary is null) return RedirectToPage("/Vet/Index");
        if (summary.ResumeStep is ConsultationStep step) return this.RedirectToStep(step, Id);

        Consultation = summary.Consultation;
        CatalogItem = summary.CatalogItem;
        HomeCountryCode = summary.HomeCountryCode;

        BackHref = !string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl)
            ? ReturnUrl!
            : Url.Page("/Appointments/Index") ?? "/Appointments";

        if (Consultation.PetId is int petId)
            PetHistoryHref = Url.Page("/Pets/History", new { id = petId, returnUrl = Url.Page("/Vet/Virtual/Summary", new { id = Id }) });

        UsingCare = Consultation.UsesCareBenefit || Consultation.PriceCharged <= 0m;
        IsIntl = string.Equals(Consultation.ServiceCatalogCode, ServiceCatalogCodes.VetIntl30, StringComparison.OrdinalIgnoreCase);
        HasProviderNotes = !string.IsNullOrWhiteSpace(Consultation.ClinicalNotes);
        return Page();
    }
}
