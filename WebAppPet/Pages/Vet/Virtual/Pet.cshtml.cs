using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.Consultations.ChooseEmergency;
using WebAppPet.Application.Consultations.ContinueVirtual;
using WebAppPet.Application.Consultations.PrepareScreening;
using WebAppPet.Application.Consultations.ScreenConsultation;
using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;
using WebAppPet.Ui;

namespace WebAppPet.Pages.Vet.Virtual;

public class PetModel : PageModel
{
    private readonly AuthService _auth;
    private readonly PrepareScreeningHandler _prepare;
    private readonly ScreenConsultationHandler _screen;
    private readonly ContinueVirtualHandler _continueVirtual;
    private readonly ChooseEmergencyHandler _chooseEmergency;
    private readonly IWebHostEnvironment _env;

    public PetModel(
        AuthService auth,
        PrepareScreeningHandler prepare,
        ScreenConsultationHandler screen,
        ContinueVirtualHandler continueVirtual,
        ChooseEmergencyHandler chooseEmergency,
        IWebHostEnvironment env)
    {
        _auth = auth;
        _prepare = prepare;
        _screen = screen;
        _continueVirtual = continueVirtual;
        _chooseEmergency = chooseEmergency;
        _env = env;
    }

    [BindProperty(SupportsGet = true)]
    public int ConsultationId { get; set; }

    /// <summary>intl = guidance matches; local = state-licensed teleconsult.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Next { get; set; }

    /// <summary>Skip soft-choice and show the form again.</summary>
    [BindProperty(SupportsGet = true)]
    public bool Edit { get; set; }

    [BindProperty] public int PetId { get; set; }
    [BindProperty] public string PetCountry { get; set; } = "CO";
    [BindProperty] public string PetUsState { get; set; } = "NC";
    [BindProperty] public string? Symptoms { get; set; }
    [BindProperty] public IFormFile? Media1 { get; set; }
    [BindProperty] public IFormFile? Media2 { get; set; }

    [BindProperty] public bool BreathingTrouble { get; set; }
    [BindProperty] public bool Seizures { get; set; }
    [BindProperty] public bool Unconscious { get; set; }
    [BindProperty] public bool SevereBleeding { get; set; }
    [BindProperty] public bool ToxinIngestion { get; set; }
    [BindProperty] public bool ExtremePain { get; set; }
    [BindProperty] public bool CannotUrinate { get; set; }
    [BindProperty] public bool NoRedFlags { get; set; }

    public Consultation? Consultation { get; set; }
    public List<Models.Pet> Pets { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public bool StateFromLocation { get; set; }
    public string? UserCity { get; set; }
    public string HomeCountryCode { get; set; } = MarketCountry.DefaultIso;
    public string HomeCountryLabel { get; set; } = "Colombia";
    public BusinessMarket DetectedMarket { get; set; }
    public bool ShowPathChoice { get; set; }
    public bool HasVcpr { get; set; }

    public static readonly (string Code, string NameEs, string NameEn)[] UsStates =
    {
        ("NC", "Carolina del Norte", "North Carolina"),
        ("SC", "Carolina del Sur", "South Carolina"),
        ("VA", "Virginia", "Virginia"),
        ("GA", "Georgia", "Georgia"),
        ("TN", "Tennessee", "Tennessee"),
        ("FL", "Florida", "Florida"),
        ("NY", "Nueva York", "New York"),
        ("CA", "California", "California"),
        ("TX", "Texas", "Texas"),
        ("Other", "Otro / fuera de la lista", "Other / Outside list")
    };

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Vet/Virtual/Pet?consultationId={ConsultationId}&next={Next}" });

        var form = await _prepare.HandleAsync(new PrepareScreeningQuery(userId, ConsultationId, Next, Edit));
        if (form is null) return RedirectToPage("/Vet/Index");

        ApplyContext(form.Context);
        PetId = form.PetId;
        Symptoms = form.Context.Consultation.Symptoms;
        PetUsState = form.PetUsState;
        StateFromLocation = form.StateFromLocation;
        HasVcpr = form.HasVcpr;
        ShowPathChoice = form.ShowPathChoice;
        if (form.SavedAnswers is { } answers)
        {
            BreathingTrouble = answers.BreathingTrouble;
            Seizures = answers.Seizures;
            Unconscious = answers.Unconscious;
            SevereBleeding = answers.SevereBleeding;
            ToxinIngestion = answers.ToxinIngestion;
            ExtremePain = answers.ExtremePain;
            CannotUrinate = answers.CannotUrinate;
            NoRedFlags = !form.Context.Consultation.HasRedFlags;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (_auth.CurrentUserId is not int userId) return RedirectToPage("/Account/Login");

        var answers = new SafetyScreeningService.SafetyAnswers(
            BreathingTrouble, Seizures, Unconscious, SevereBleeding,
            ToxinIngestion, ExtremePain, CannotUrinate, null);

        var result = await _screen.HandleAsync(new ScreenConsultationCommand(
            userId, ConsultationId, Next, PetId, PetUsState, Symptoms,
            Upload(Media1, userId), Upload(Media2, userId), answers, NoRedFlags));

        if (result.Outcome == ScreenConsultationOutcome.NotFound)
            return RedirectToPage("/Vet/Index");

        ApplyContext(result.Context!);
        if (result.PetUsState is not null)
            PetUsState = result.PetUsState;

        switch (result.Outcome)
        {
            case ScreenConsultationOutcome.MediaInvalid:
                ErrorMessage = CatalogLocalizer.Loc("Archivo no válido (foto ≤5MB o video corto ≤25MB).", "Invalid file (photo ≤5MB or short video ≤25MB).");
                return Page();
            case ScreenConsultationOutcome.RedFlags:
                HasVcpr = result.HasVcpr;
                ShowPathChoice = true;
                return Page();
            case ScreenConsultationOutcome.Routed:
                return this.RedirectToStep(result.NextStep!.Value, ConsultationId);
            default:
                return Page(); // UI keeps Continue disabled — no flash alert.
        }
    }

    public async Task<IActionResult> OnPostGoEmergencyAsync()
    {
        if (_auth.CurrentUserId is not int userId) return RedirectToPage("/Account/Login");
        if (!await _chooseEmergency.HandleAsync(new ChooseEmergencyCommand(userId, ConsultationId)))
            return RedirectToPage("/Vet/Index");
        return RedirectToPage("/Vet/Emergency", new { consultationId = ConsultationId });
    }

    public async Task<IActionResult> OnPostContinueVirtualAsync()
    {
        if (_auth.CurrentUserId is not int userId) return RedirectToPage("/Account/Login");
        var step = await _continueVirtual.HandleAsync(new ContinueVirtualCommand(userId, ConsultationId, Next));
        return this.RedirectToStep(step, ConsultationId, Next);
    }

    private ConsultationMedia? Upload(IFormFile? file, int userId) =>
        file is { Length: > 0 }
            ? new ConsultationMedia(VetMediaStorage.Validate(file), ct => VetMediaStorage.SaveAsync(file, userId, _env, ct))
            : null;

    private void ApplyContext(ScreeningContext context)
    {
        Consultation = context.Consultation;
        Next = context.Path;
        Pets = context.Pets;
        UserCity = context.UserCity;
        HomeCountryCode = context.HomeCountryCode;
        DetectedMarket = MarketCountry.ToMarket(HomeCountryCode);
        HomeCountryLabel = HomeCountryCode == "US"
            ? CatalogLocalizer.Loc("Estados Unidos", "United States")
            : CatalogLocalizer.Loc("Colombia", "Colombia");
        PetCountry = HomeCountryCode;
    }
}
