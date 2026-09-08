using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Vet.International;

public class CountriesModel : PageModel
{
    private readonly AuthService _auth;
    private readonly ConsultationFlowService _flow;
    private readonly CountryCatalogService _countries;
    private readonly VetAuditService _audit;

    public CountriesModel(
        AuthService auth,
        ConsultationFlowService flow,
        CountryCatalogService countries,
        VetAuditService audit)
    {
        _auth = auth;
        _flow = flow;
        _countries = countries;
        _audit = audit;
    }

    [BindProperty(SupportsGet = true)]
    public int ConsultationId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    [BindProperty]
    public string? SelectedIso { get; set; }

    [BindProperty]
    public string? WaitlistEmail { get; set; }

    [BindProperty]
    public bool WaitlistConsent { get; set; }

    public Consultation? Consultation { get; set; }
    public List<CountryCatalogEntry> Countries { get; set; } = new();
    public string? Message { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Vet/International/Countries?consultationId={ConsultationId}" });

        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation is null) return RedirectToPage("/Vet/International/Home");
        Countries = await _countries.SearchAsync(Q);
        return Page();
    }

    public async Task<IActionResult> OnPostSelectAsync()
    {
        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation is null) return RedirectToPage("/Vet/International/Home");
        Countries = await _countries.SearchAsync(Q);

        var country = Countries.FirstOrDefault(c => c.Iso2 == SelectedIso);
        if (country is null)
        {
            ErrorMessage = CatalogLocalizer.Loc("Selecciona un país.", "Select a country.");
            return Page();
        }

        if (country.Status != CountryMarketStatus.Available)
        {
            ErrorMessage = CatalogLocalizer.Loc(
                "Este país aún no acepta reservas. Únete a la lista de espera.",
                "This country is not bookable yet. Join the waitlist.");
            return Page();
        }

        Consultation.PreferredCountry = country.Iso2;
        Consultation.ContextCountry = country.Iso2;
        Consultation.MatchMode = IntlMatchMode.Country;
        await _flow.TouchAsync(Consultation);
        await _audit.LogAsync("country_selected", _auth.CurrentUserId, "Consultation", ConsultationId, new { country.Iso2 });
        return RedirectToPage("/Vet/International/Matches", new { consultationId = ConsultationId });
    }

    public async Task<IActionResult> OnPostWaitlistAsync()
    {
        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        Countries = await _countries.SearchAsync(Q);
        if (string.IsNullOrWhiteSpace(SelectedIso))
        {
            ErrorMessage = CatalogLocalizer.Loc("Selecciona un país.", "Select a country.");
            return Page();
        }

        if (!WaitlistConsent)
        {
            ErrorMessage = CatalogLocalizer.Loc("Necesitamos tu consentimiento para avisarte.", "We need your consent to notify you.");
            return Page();
        }

        await _countries.JoinWaitlistAsync(
            SelectedIso,
            _auth.CurrentUserId,
            WaitlistEmail,
            CatalogLocalizer.IsEnglish() ? "en" : "es",
            WaitlistConsent);

        await _audit.LogAsync("waitlist_joined", _auth.CurrentUserId, "Country", null, new { SelectedIso });
        Message = CatalogLocalizer.Loc("Te avisaremos cuando ese país esté Disponible.", "We'll notify you when that country becomes Available.");
        return Page();
    }
}
