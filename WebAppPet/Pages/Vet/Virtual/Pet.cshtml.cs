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

    /// <summary>True when the select was prefilled from the user's saved location.</summary>
    public bool StateFromLocation { get; set; }

    public string? UserCity { get; set; }

    public static readonly (string Code, string Name)[] UsStates =
    {
        ("NC", "North Carolina"), ("SC", "South Carolina"), ("VA", "Virginia"),
        ("GA", "Georgia"), ("TN", "Tennessee"), ("FL", "Florida"), ("NY", "New York"),
        ("CA", "California"), ("TX", "Texas"), ("Other", "Other / Outside list")
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
        var fromLocation = GeoHelper.ResolveUsState(user?.City, user?.Latitude, user?.Longitude);

        // Antes de completar este paso, prioriza la ubicación guardada del usuario
        // (las consultas nuevas aún pueden venir con NC por defecto).
        if (Consultation.PetId is null && !string.IsNullOrWhiteSpace(fromLocation))
        {
            PetUsState = fromLocation;
            StateFromLocation = true;
            if (!string.Equals(Consultation.PetUsState, fromLocation, StringComparison.OrdinalIgnoreCase))
            {
                Consultation.PetUsState = fromLocation;
                await _flow.TouchAsync(Consultation);
            }
        }
        else
        {
            PetUsState = string.IsNullOrWhiteSpace(Consultation.PetUsState) ? (fromLocation ?? "NC") : Consultation.PetUsState;
            StateFromLocation = !string.IsNullOrWhiteSpace(fromLocation) &&
                                string.Equals(PetUsState, fromLocation, StringComparison.OrdinalIgnoreCase);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login");

        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation is null) return RedirectToPage("/Vet/Index");

        Pets = await _db.Pets.Where(p => p.OwnerId == _auth.CurrentUserId).OrderBy(p => p.Name).ToListAsync();

        if (PetId <= 0 || !Pets.Any(p => p.Id == PetId))
        {
            ErrorMessage = CatalogLocalizer.Loc("Selecciona una mascota.", "Select a pet.");
            return Page();
        }

        if (string.IsNullOrWhiteSpace(PetUsState))
        {
            ErrorMessage = CatalogLocalizer.Loc("Confirma el estado actual de la mascota.", "Confirm the pet's current state.");
            return Page();
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
        Consultation.Symptoms = Symptoms?.Trim();
        if (Media1 is { Length: > 0 })
            Consultation.MediaUrl1 = await VetMediaStorage.SaveAsync(Media1, _auth.CurrentUserId.Value, _env);
        if (Media2 is { Length: > 0 })
            Consultation.MediaUrl2 = await VetMediaStorage.SaveAsync(Media2, _auth.CurrentUserId.Value, _env);

        await _flow.TouchAsync(Consultation);

        if (string.Equals(Next, "intl", StringComparison.OrdinalIgnoreCase))
            return RedirectToPage("/Vet/International/MatchMode", new { consultationId = ConsultationId });

        return RedirectToPage("/Vet/Virtual/Safety", new { consultationId = ConsultationId });
    }
}
