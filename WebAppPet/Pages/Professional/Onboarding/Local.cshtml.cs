using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.ProfessionalOnboarding.GetOnboardingDraft;
using WebAppPet.Application.ProfessionalOnboarding.SaveOnboardingApplication;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Professional.Onboarding;

public class LocalModel : PageModel
{
    private readonly AuthService _auth;
    private readonly GetOnboardingDraftHandler _getDraft;
    private readonly SaveOnboardingApplicationHandler _save;
    private readonly IWebHostEnvironment _env;

    public LocalModel(
        AuthService auth,
        GetOnboardingDraftHandler getDraft,
        SaveOnboardingApplicationHandler save,
        IWebHostEnvironment env)
    {
        _auth = auth;
        _getDraft = getDraft;
        _save = save;
        _env = env;
    }

    [BindProperty(SupportsGet = true)]
    public string? Track { get; set; }

    public bool IsBehavior => string.Equals(Track, "behavior", StringComparison.OrdinalIgnoreCase);

    [BindProperty] public string LegalName { get; set; } = "";
    [BindProperty] public string ClinicOrPracticeName { get; set; } = "";
    [BindProperty] public string LicenseNumber { get; set; } = "";
    [BindProperty] public string LicenseJurisdiction { get; set; } = "";
    [BindProperty] public DateTime? LicenseExpiry { get; set; }
    [BindProperty] public string Languages { get; set; } = "";
    [BindProperty] public string Specialties { get; set; } = "";
    [BindProperty] public bool HasPhysicalClinic { get; set; }
    [BindProperty] public bool VcprCapable { get; set; }
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

        if (view.Draft is { Track: ProfessionalOnboardingTrack.Local or ProfessionalOnboardingTrack.Behavior } draft)
        {
            // Wrong track query (e.g. Local vs behavior) → open the matching form with draft.
            var draftIsBehavior = draft.Track == ProfessionalOnboardingTrack.Behavior;
            if (IsBehavior != draftIsBehavior)
                return draftIsBehavior ? RedirectToPage("./Local", new { Track = "behavior" }) : RedirectToPage("./Local");

            LegalName = draft.LegalName;
            ClinicOrPracticeName = draft.ClinicOrPracticeName;
            LicenseNumber = draft.LicenseNumber;
            LicenseJurisdiction = draft.LicenseJurisdiction;
            LicenseExpiry = draft.LicenseExpiry;
            Languages = draft.Languages;
            Specialties = draft.Specialties;
            HasPhysicalClinic = draft.HasPhysicalClinic;
            VcprCapable = draft.VcprCapable;
            DocumentsNote = draft.DocumentsNote;
        }
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
            IsBehavior ? ProfessionalOnboardingTrack.Behavior : ProfessionalOnboardingTrack.Local,
            new OnboardingDetails(
                LegalName, ClinicOrPracticeName, LicenseNumber, LicenseJurisdiction, LicenseExpiry,
                Languages, Specialties, BreedExpertiseCsv: null, AcceptsInternationalClients: false,
                HasPhysicalClinic, VcprCapable, DocumentsNote),
            CountrySearch: null,
            Document(userId),
            submit));

        switch (result.Outcome)
        {
            case SaveOnboardingApplicationOutcome.NoBusinessProfile:
                return RedirectToPage("/Account/RegisterBusiness");
            case SaveOnboardingApplicationOutcome.MissingRequired:
                Error = CatalogLocalizer.Loc(
                    IsBehavior
                        ? "Guardamos tu borrador. Para enviarlo escribe tu nombre legal y la jurisdicción o estado."
                        : "Guardamos tu borrador. Para enviarlo escribe tu nombre legal y el estado de tu licencia.",
                    IsBehavior
                        ? "Your draft was saved. To submit it, enter your legal name and the jurisdiction or state."
                        : "Your draft was saved. To submit it, enter your legal name and your license state.");
                return Page();
            default:
                return RedirectToPage("/Professional/Onboarding/Status");
        }
    }

    private OnboardingDocument? Document(int userId) =>
        DocumentUpload is { Length: > 0 } file
            ? new OnboardingDocument(ct => ProfessionalDocumentStorage.SaveAsync(file, userId, _env, ct))
            : null;
}
