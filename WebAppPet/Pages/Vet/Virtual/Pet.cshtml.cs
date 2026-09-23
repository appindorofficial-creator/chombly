using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Vet.Virtual;

public class PetModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly ConsultationFlowService _flow;
    private readonly SafetyScreeningService _safety;
    private readonly VcprService _vcpr;
    private readonly VetAuditService _audit;
    private readonly IWebHostEnvironment _env;

    public PetModel(
        AppDbContext db,
        AuthService auth,
        ConsultationFlowService flow,
        SafetyScreeningService safety,
        VcprService vcpr,
        VetAuditService audit,
        IWebHostEnvironment env)
    {
        _db = db;
        _auth = auth;
        _flow = flow;
        _safety = safety;
        _vcpr = vcpr;
        _audit = audit;
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
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Vet/Virtual/Pet?consultationId={ConsultationId}&next={Next}" });

        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation is null) return RedirectToPage("/Vet/Index");

        Next = NormalizeNext(Next, Consultation.ServiceCatalogCode);

        Pets = await _db.Pets.Where(p => p.OwnerId == _auth.CurrentUserId).OrderBy(p => p.Name).ToListAsync();
        PetId = Consultation.PetId ?? Pets.FirstOrDefault()?.Id ?? 0;
        Symptoms = Consultation.Symptoms;

        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == _auth.CurrentUserId.Value);
        ApplyHomeCountry(user);
        var fromLocation = GeoHelper.ResolveUsState(user?.City, user?.Latitude, user?.Longitude);

        if (ApplyLocationDefaults(Consultation, fromLocation))
            await _flow.TouchAsync(Consultation);

        if (Consultation.PetId is int pid)
            HasVcpr = await _vcpr.HasActiveAsync(pid, Consultation.PetUsState);

        // Resume soft choice after Emergency if flags already saved.
        if (!Edit
            && Consultation.HasRedFlags
            && Consultation.Status is ConsultationStatus.SafetyScreened or ConsultationStatus.EscalatedToEmergency)
        {
            ShowPathChoice = true;
            HydrateSafety(Consultation);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (_auth.CurrentUserId is null) return RedirectToPage("/Account/Login");

        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation is null) return RedirectToPage("/Vet/Index");

        Next = NormalizeNext(Next, Consultation.ServiceCatalogCode);
        Pets = await _db.Pets.Where(p => p.OwnerId == _auth.CurrentUserId).OrderBy(p => p.Name).ToListAsync();

        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == _auth.CurrentUserId.Value);
        ApplyHomeCountry(user);

        if (PetId <= 0 || !Pets.Any(p => p.Id == PetId))
            return Page();

        // Home country is fixed from the profile flag — not chosen in this flow.
        PetCountry = HomeCountryCode;
        if (PetCountry == "US")
        {
            if (string.IsNullOrWhiteSpace(PetUsState))
                return Page();
        }
        else
        {
            PetUsState = "Other";
        }

        var err1 = VetMediaStorage.Validate(Media1);
        var err2 = VetMediaStorage.Validate(Media2);
        if (err1 != null || err2 != null)
        {
            ErrorMessage = CatalogLocalizer.Loc("Archivo no válido (foto ≤5MB o video corto ≤25MB).", "Invalid file (photo ≤5MB or short video ≤25MB).");
            return Page();
        }

        var answers = new SafetyScreeningService.SafetyAnswers(
            BreathingTrouble, Seizures, Unconscious, SevereBleeding,
            ToxinIngestion, ExtremePain, CannotUrinate, null);
        var hasRed = _safety.HasRedFlags(answers);
        if (!hasRed && !NoRedFlags)
            return Page(); // UI keeps Continue disabled — no flash alert.

        Consultation.PetId = PetId;
        Consultation.PetUsState = PetUsState.Trim().ToUpperInvariant();
        Consultation.ContextCountry = PetCountry;
        Consultation.Symptoms = Symptoms?.Trim();
        if (Media1 is { Length: > 0 })
            Consultation.MediaUrl1 = await VetMediaStorage.SaveAsync(Media1, _auth.CurrentUserId.Value, _env);
        if (Media2 is { Length: > 0 })
            Consultation.MediaUrl2 = await VetMediaStorage.SaveAsync(Media2, _auth.CurrentUserId.Value, _env);

        Consultation.SafetyAnswersJson = _safety.Serialize(answers);
        Consultation.HasRedFlags = hasRed;
        Consultation.HasActiveVcpr = await _vcpr.HasActiveAsync(PetId, Consultation.PetUsState);
        HasVcpr = Consultation.HasActiveVcpr;
        Consultation.Status = ConsultationStatus.SafetyScreened;
        await _flow.TouchAsync(Consultation);

        if (hasRed)
        {
            await _audit.LogAsync("safety_flagged", _auth.CurrentUserId, "Consultation", Consultation.Id, answers);
            ShowPathChoice = true;
            return Page();
        }

        return await RouteAfterSafetyAsync(Consultation);
    }

    public async Task<IActionResult> OnPostGoEmergencyAsync()
    {
        if (_auth.CurrentUserId is null) return RedirectToPage("/Account/Login");
        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation is null) return RedirectToPage("/Vet/Index");
        await _audit.LogAsync("safety_chose_emergency", _auth.CurrentUserId, "Consultation", Consultation.Id,
            new { consultationId = ConsultationId });
        return RedirectToPage("/Vet/Emergency", new { consultationId = ConsultationId });
    }

    public async Task<IActionResult> OnPostContinueVirtualAsync()
    {
        if (_auth.CurrentUserId is null) return RedirectToPage("/Account/Login");
        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation?.PetId is null) return RedirectToPage("/Vet/Virtual/Pet", new { consultationId = ConsultationId, next = Next });

        Next = NormalizeNext(Next, Consultation.ServiceCatalogCode);
        if (Consultation.Status == ConsultationStatus.EscalatedToEmergency)
            Consultation.Status = ConsultationStatus.SafetyScreened;
        Consultation.Modality = VetModality.Virtual;
        await _flow.TouchAsync(Consultation);
        await _audit.LogAsync("safety_chose_continue_virtual", _auth.CurrentUserId, "Consultation", Consultation.Id,
            new { hasRedFlags = Consultation.HasRedFlags, next = Next });

        return await RouteAfterSafetyAsync(Consultation);
    }

    private async Task<IActionResult> RouteAfterSafetyAsync(Consultation c)
    {
        var next = NormalizeNext(Next, c.ServiceCatalogCode);

        // Local US teleconsult (VCPR) only when home market is United States.
        if (string.Equals(next, "local", StringComparison.OrdinalIgnoreCase)
            && !MarketCountry.AllowsUsLocalTeleconsult(HomeCountryCode))
            next = "intl";

        if (string.Equals(next, "local", StringComparison.OrdinalIgnoreCase))
        {
            c.ServiceCatalogCode = ServiceCatalogCodes.VetLocal30;
            c.HasActiveVcpr = c.PetId is int pid && await _vcpr.HasActiveAsync(pid, c.PetUsState);
            if (!c.HasActiveVcpr)
            {
                await _flow.TouchAsync(c);
                return RedirectToPage("/Vet/Virtual/Eligibility", new { consultationId = ConsultationId });
            }

            c.Status = ConsultationStatus.EligibilityVerified;
            await _flow.TouchAsync(c);
            return RedirectToPage("/Vet/Virtual/Providers", new { consultationId = ConsultationId });
        }

        // Default: guidance / international matches (shortest virtual path).
        c.ServiceCatalogCode = ServiceCatalogCodes.VetIntl30;
        c.MatchMode = IntlMatchMode.Best;
        if (string.IsNullOrWhiteSpace(c.PreferredBreed) && c.PetId is int petIdForBreed)
        {
            var breed = await _db.Pets.AsNoTracking()
                .Where(p => p.Id == petIdForBreed)
                .Select(p => p.Breed)
                .FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(breed))
                c.PreferredBreed = breed.Trim();
        }

        await _flow.TouchAsync(c);
        return RedirectToPage("/Vet/International/Matches", new { consultationId = ConsultationId });
    }

    private void HydrateSafety(Consultation c)
    {
        var a = _safety.Deserialize(c.SafetyAnswersJson);
        if (a is null) return;
        BreathingTrouble = a.BreathingTrouble;
        Seizures = a.Seizures;
        Unconscious = a.Unconscious;
        SevereBleeding = a.SevereBleeding;
        ToxinIngestion = a.ToxinIngestion;
        ExtremePain = a.ExtremePain;
        CannotUrinate = a.CannotUrinate;
        NoRedFlags = !c.HasRedFlags;
    }

    private void ApplyHomeCountry(AppUser? user)
    {
        UserCity = string.IsNullOrWhiteSpace(user?.City) ? null : user!.City.Trim();
        HomeCountryCode = MarketCountry.ResolveForUser(user?.CountryCode, user?.City, user?.Latitude, user?.Longitude);
        DetectedMarket = MarketCountry.ToMarket(HomeCountryCode);
        HomeCountryLabel = HomeCountryCode == "US"
            ? CatalogLocalizer.Loc("Estados Unidos", "United States")
            : CatalogLocalizer.Loc("Colombia", "Colombia");
        PetCountry = HomeCountryCode;
    }

    private bool ApplyLocationDefaults(Consultation consultation, string? fromLocation)
    {
        PetCountry = HomeCountryCode;
        StateFromLocation = !string.IsNullOrWhiteSpace(fromLocation);

        if (PetCountry == "CO")
        {
            PetUsState = "Other";
            if (!string.Equals(consultation.PetUsState, "Other", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(consultation.ContextCountry, "CO", StringComparison.OrdinalIgnoreCase))
            {
                consultation.PetUsState = "Other";
                consultation.ContextCountry = "CO";
                return true;
            }

            return false;
        }

        var storedState = string.IsNullOrWhiteSpace(consultation.PetUsState)
            ? null
            : consultation.PetUsState.Trim();
        var looksLikeLegacyNcDefault =
            string.Equals(storedState, "NC", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(consultation.ContextCountry, "US", StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(fromLocation) &&
            !string.Equals(fromLocation, "Other", StringComparison.OrdinalIgnoreCase) &&
            (consultation.PetId is null || looksLikeLegacyNcDefault))
        {
            PetUsState = fromLocation;
        }
        else if (!string.IsNullOrWhiteSpace(storedState) &&
                 !string.Equals(storedState, "Other", StringComparison.OrdinalIgnoreCase) &&
                 !looksLikeLegacyNcDefault)
        {
            PetUsState = storedState!;
        }
        else
        {
            PetUsState = fromLocation is not null &&
                         !string.Equals(fromLocation, "Other", StringComparison.OrdinalIgnoreCase)
                ? fromLocation
                : "NC";
        }

        StateFromLocation = !string.IsNullOrWhiteSpace(fromLocation) &&
                            string.Equals(PetUsState, fromLocation, StringComparison.OrdinalIgnoreCase);

        var changed = false;
        if (!string.Equals(consultation.PetUsState, PetUsState, StringComparison.OrdinalIgnoreCase))
        {
            consultation.PetUsState = PetUsState;
            changed = true;
        }

        if (!string.Equals(consultation.ContextCountry, "US", StringComparison.OrdinalIgnoreCase))
        {
            consultation.ContextCountry = "US";
            changed = true;
        }

        return changed;
    }

    private static string NormalizeNext(string? next, string? catalogCode)
    {
        if (string.Equals(next, "local", StringComparison.OrdinalIgnoreCase)) return "local";
        if (string.Equals(next, "intl", StringComparison.OrdinalIgnoreCase)) return "intl";
        if (string.Equals(catalogCode, ServiceCatalogCodes.VetLocal30, StringComparison.OrdinalIgnoreCase))
            return "local";
        return "intl";
    }
}
