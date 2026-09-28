using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.ProfessionalOnboarding.GetOnboardingDraft;
using WebAppPet.Application.ProfessionalOnboarding.SaveOnboardingApplication;
using WebAppPet.Infrastructure.Identity;
using WebAppPet.Infrastructure.Storage;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Professional.Onboarding;

public class InternationalModel : PageModel
{
    private readonly AuthService _auth;
    private readonly GetOnboardingDraftHandler _getDraft;
    private readonly SaveOnboardingApplicationHandler _save;
    private readonly CountryCatalogService _countries;
    private readonly IWebHostEnvironment _env;

    public InternationalModel(
        AuthService auth,
        GetOnboardingDraftHandler getDraft,
        SaveOnboardingApplicationHandler save,
        CountryCatalogService countries,
        IWebHostEnvironment env)
    {
        _auth = auth;
        _getDraft = getDraft;
        _save = save;
        _countries = countries;
        _env = env;
    }

    [BindProperty] public string LegalName { get; set; } = "";
    [BindProperty] public string ClinicOrPracticeName { get; set; } = "";
    [BindProperty] public string LicenseNumber { get; set; } = "";
    [BindProperty] public string LicenseJurisdiction { get; set; } = "";
    [BindProperty] public string CountrySearch { get; set; } = "";
    [BindProperty] public DateTime? LicenseExpiry { get; set; }
    [BindProperty] public string Languages { get; set; } = "es,en";
    [BindProperty] public string Specialties { get; set; } = "";
    [BindProperty] public string BreedExpertiseCsv { get; set; } = "";
    [BindProperty] public bool AcceptsInternationalClients { get; set; } = true;
    [BindProperty] public string? DocumentsNote { get; set; }
    [BindProperty] public IFormFile? DocumentUpload { get; set; }

    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is not int userId || (!_auth.IsGroomer && !_auth.IsAdmin))
            return RedirectToPage("/Account/RegisterBusiness");

        var view = await _getDraft.HandleAsync(new GetOnboardingDraftQuery(userId));
        if (!view.HasBusinessProfile)
            return RedirectToPage("/Account/RegisterBusiness");

        var defaultCountry = BusinessMarketResolver.DefaultInternationalIso(view.Market);
        if (string.IsNullOrWhiteSpace(LicenseJurisdiction))
            LicenseJurisdiction = defaultCountry;

        if (view.Draft is { Track: ProfessionalOnboardingTrack.International } draft)
        {
            LegalName = draft.LegalName;
            ClinicOrPracticeName = draft.ClinicOrPracticeName;
            LicenseNumber = draft.LicenseNumber;
            LicenseJurisdiction = string.IsNullOrWhiteSpace(draft.LicenseJurisdiction) ? defaultCountry : draft.LicenseJurisdiction;
            LicenseExpiry = draft.LicenseExpiry;
            Languages = draft.Languages;
            Specialties = draft.Specialties;
            BreedExpertiseCsv = draft.BreedExpertiseCsv;
            AcceptsInternationalClients = draft.AcceptsInternationalClients;
            DocumentsNote = draft.DocumentsNote;
        }

        await SyncCountrySearchAsync();
        return Page();
    }

    public Task<IActionResult> OnPostDraftAsync() => SaveAsync(submit: false);

    public Task<IActionResult> OnPostSubmitAsync() => SaveAsync(submit: true);

    private async Task<IActionResult> SaveAsync(bool submit)
    {
        if (_auth.CurrentUserId is not int userId || (!_auth.IsGroomer && !_auth.IsAdmin))
            return RedirectToPage("/Account/RegisterBusiness");

        var result = await _save.HandleAsync(new SaveOnboardingApplicationCommand(
            userId,
            ProfessionalOnboardingTrack.International,
            new OnboardingDetails(
                LegalName, ClinicOrPracticeName, LicenseNumber, LicenseJurisdiction, LicenseExpiry,
                Languages, Specialties, BreedExpertiseCsv, AcceptsInternationalClients,
                HasPhysicalClinic: false, VcprCapable: false, DocumentsNote),
            CountrySearch,
            Document(userId),
            submit));

        switch (result.Outcome)
        {
            case SaveOnboardingApplicationOutcome.NoBusinessProfile:
                return RedirectToPage("/Account/RegisterBusiness");
            case SaveOnboardingApplicationOutcome.CountryNotFound:
                Error = CatalogLocalizer.Loc("Elige un país de la lista.", "Pick a country from the list.");
                break;
            case SaveOnboardingApplicationOutcome.MissingRequired:
                Error = CatalogLocalizer.Loc(
                    "Guardamos tu borrador. Para enviarlo escribe tu nombre legal.",
                    "Your draft was saved. To submit it, enter your legal name.");
                break;
            default:
                return RedirectToPage("/Professional/Onboarding/Status");
        }

        LicenseJurisdiction = result.LicenseJurisdiction ?? "";
        await SyncCountrySearchAsync();
        return Page();
    }

    private async Task SyncCountrySearchAsync()
    {
        if (string.IsNullOrWhiteSpace(LicenseJurisdiction))
        {
            CountrySearch = "";
            return;
        }

        var entry = await _countries.GetByIsoAsync(LicenseJurisdiction);
        CountrySearch = entry is null
            ? LicenseJurisdiction
            : CountryCatalogService.DisplayName(entry);
    }

    private OnboardingDocument? Document(int userId) =>
        DocumentUpload is { Length: > 0 } file
            ? new OnboardingDocument(ct => ProfessionalDocumentStorage.SaveAsync(file, userId, _env, ct))
            : null;
}
