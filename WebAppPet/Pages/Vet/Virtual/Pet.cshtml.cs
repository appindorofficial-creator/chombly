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
    private readonly IWebHostEnvironment _env;

    public PetModel(AppDbContext db, AuthService auth, ConsultationFlowService flow, IWebHostEnvironment env)
    {
        _db = db;
        _auth = auth;
        _flow = flow;
        _env = env;
    }

    [BindProperty(SupportsGet = true)]
    public int ConsultationId { get; set; }

    /// <summary>When "intl", continue to international match after pet/location.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Next { get; set; }

    [BindProperty]
    public int PetId { get; set; }

    /// <summary>CO or US — primary launch markets.</summary>
    [BindProperty]
    public string PetCountry { get; set; } = "CO";

    [BindProperty]
    public string PetUsState { get; set; } = "NC";

    [BindProperty]
    public string? Symptoms { get; set; }

    [BindProperty]
    public IFormFile? Media1 { get; set; }

    [BindProperty]
    public IFormFile? Media2 { get; set; }

    public Consultation? Consultation { get; set; }
    public List<Models.Pet> Pets { get; set; } = new();
    public string? ErrorMessage { get; set; }

    /// <summary>True when the location was prefilled from the user's saved profile.</summary>
    public bool StateFromLocation { get; set; }

    public string? UserCity { get; set; }

    public BusinessMarket DetectedMarket { get; set; }

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
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Vet/Virtual/Pet?consultationId={ConsultationId}" });

        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation is null) return RedirectToPage("/Vet/Index");

        Pets = await _db.Pets.Where(p => p.OwnerId == _auth.CurrentUserId).OrderBy(p => p.Name).ToListAsync();
        PetId = Consultation.PetId ?? Pets.FirstOrDefault()?.Id ?? 0;
        Symptoms = Consultation.Symptoms;

        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == _auth.CurrentUserId.Value);
        UserCity = string.IsNullOrWhiteSpace(user?.City) ? null : user!.City.Trim();
        DetectedMarket = BusinessMarketResolver.ResolveUser(user?.City, user?.Latitude, user?.Longitude);
        var fromLocation = GeoHelper.ResolveUsState(user?.City, user?.Latitude, user?.Longitude);

        if (ApplyLocationDefaults(Consultation, fromLocation))
            await _flow.TouchAsync(Consultation);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login");

        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation is null) return RedirectToPage("/Vet/Index");

        Pets = await _db.Pets.Where(p => p.OwnerId == _auth.CurrentUserId).OrderBy(p => p.Name).ToListAsync();

        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == _auth.CurrentUserId.Value);
        UserCity = string.IsNullOrWhiteSpace(user?.City) ? null : user!.City.Trim();
        DetectedMarket = BusinessMarketResolver.ResolveUser(user?.City, user?.Latitude, user?.Longitude);

        if (PetId <= 0 || !Pets.Any(p => p.Id == PetId))
        {
            ErrorMessage = CatalogLocalizer.Loc("Selecciona una mascota.", "Select a pet.");
            return Page();
        }

        PetCountry = NormalizeCountry(PetCountry) is { Length: > 0 } c ? c : "CO";
        if (PetCountry == "US")
        {
            if (string.IsNullOrWhiteSpace(PetUsState))
            {
                ErrorMessage = CatalogLocalizer.Loc("Confirma el estado actual de la mascota.", "Confirm the pet's current state.");
                return Page();
            }
        }
        else
        {
            PetCountry = "CO";
            PetUsState = "Other";
        }

        var err1 = VetMediaStorage.Validate(Media1);
        var err2 = VetMediaStorage.Validate(Media2);
        if (err1 != null || err2 != null)
        {
            ErrorMessage = CatalogLocalizer.Loc("Archivo no válido (foto ≤5MB o video corto ≤25MB).", "Invalid file (photo ≤5MB or short video ≤25MB).");
            return Page();
        }

        Consultation.PetId = PetId;
        Consultation.PetUsState = PetUsState.Trim().ToUpperInvariant();
        Consultation.ContextCountry = PetCountry;
        Consultation.Symptoms = Symptoms?.Trim();
        if (Media1 is { Length: > 0 })
            Consultation.MediaUrl1 = await VetMediaStorage.SaveAsync(Media1, _auth.CurrentUserId.Value, _env);
        if (Media2 is { Length: > 0 })
            Consultation.MediaUrl2 = await VetMediaStorage.SaveAsync(Media2, _auth.CurrentUserId.Value, _env);

        await _flow.TouchAsync(Consultation);

        if (string.Equals(Next, "intl", StringComparison.OrdinalIgnoreCase))
        {
            Consultation.MatchMode = IntlMatchMode.Best;
            Consultation.ServiceCatalogCode = ServiceCatalogCodes.VetIntl30;
            if (string.IsNullOrWhiteSpace(Consultation.PreferredBreed) && Consultation.PetId is int petIdForBreed)
            {
                var breed = await _db.Pets.AsNoTracking()
                    .Where(p => p.Id == petIdForBreed)
                    .Select(p => p.Breed)
                    .FirstOrDefaultAsync();
                if (!string.IsNullOrWhiteSpace(breed))
                    Consultation.PreferredBreed = breed.Trim();
            }
            await _flow.TouchAsync(Consultation);
            return RedirectToPage("/Vet/International/Matches", new { consultationId = ConsultationId });
        }

        return RedirectToPage("/Vet/Virtual/Safety", new { consultationId = ConsultationId });
    }

    /// <summary>Returns true when the consultation location fields were corrected.</summary>
    private bool ApplyLocationDefaults(Consultation consultation, string? fromLocation)
    {
        var storedCountry = NormalizeCountry(consultation.ContextCountry);
        var storedState = string.IsNullOrWhiteSpace(consultation.PetUsState)
            ? null
            : consultation.PetUsState.Trim();
        var looksLikeLegacyNcDefault =
            string.Equals(storedState, "NC", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(consultation.ContextCountry);

        PetCountry = ResolvePetCountry(storedCountry, fromLocation, looksLikeLegacyNcDefault);
        StateFromLocation = DetectedMarket != BusinessMarket.Unknown ||
                            !string.IsNullOrWhiteSpace(fromLocation);

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

        // United States
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

    private string ResolvePetCountry(string storedCountry, string? fromLocation, bool looksLikeLegacyNcDefault)
    {
        if (storedCountry is "CO" or "US")
            return storedCountry;

        if (DetectedMarket == BusinessMarket.Colombia)
            return "CO";

        if (DetectedMarket == BusinessMarket.UnitedStates)
            return "US";

        if (fromLocation is not null && !string.Equals(fromLocation, "Other", StringComparison.OrdinalIgnoreCase))
            return "US";

        if (string.Equals(fromLocation, "Other", StringComparison.OrdinalIgnoreCase))
            return "CO";

        // Ambiguous legacy NC default with no GPS/city signal → keep US state UX.
        if (looksLikeLegacyNcDefault)
            return "US";

        // Dual-market default when location is truly unknown.
        return "CO";
    }

    private static string NormalizeCountry(string? country)
    {
        if (string.IsNullOrWhiteSpace(country)) return "";
        var t = country.Trim().ToUpperInvariant();
        if (t is "CO" or "COL" or "COLOMBIA") return "CO";
        if (t is "US" or "USA" or "UM" or "EEUU" or "EE.UU") return "US";
        return t is "CO" or "US" ? t : "";
    }
}
