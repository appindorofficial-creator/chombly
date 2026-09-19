using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Vet.International;

public class MatchModeModel : PageModel
{
    private readonly AuthService _auth;
    private readonly ConsultationFlowService _flow;
    private readonly AppDbContext _db;
    private readonly CountryCatalogService _countries;
    private readonly VetAuditService _audit;

    public MatchModeModel(
        AuthService auth,
        ConsultationFlowService flow,
        AppDbContext db,
        CountryCatalogService countries,
        VetAuditService audit)
    {
        _auth = auth;
        _flow = flow;
        _db = db;
        _countries = countries;
        _audit = audit;
    }

    [BindProperty(SupportsGet = true)]
    public int ConsultationId { get; set; }

    [BindProperty]
    public IntlMatchMode? Mode { get; set; }

    public Consultation? Consultation { get; set; }
    public string? PetBreed { get; set; }
    public string? MatchBreed { get; set; }
    public string? PreferredCountry { get; set; }
    public string? PreferredCountryName { get; set; }
    public string PreferredLanguage { get; set; } = "es";
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Vet/International/MatchMode?consultationId={ConsultationId}" });

        if (!await LoadAsync()) return RedirectToPage("/Vet/International/Home");
        Mode = null;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await LoadAsync()) return RedirectToPage("/Vet/International/Home");

        if (Mode is null)
        {
            ErrorMessage = CatalogLocalizer.Loc(
                "Elige cómo quieres buscar a tu veterinario.",
                "Choose how you want to find your vet.");
            return Page();
        }

        var selected = Mode.Value;

        if (selected == IntlMatchMode.Best)
        {
            // Seed breed from pet profile when ranking, so Matches can weight it.
            if (string.IsNullOrWhiteSpace(Consultation!.PreferredBreed) && !string.IsNullOrWhiteSpace(PetBreed))
                Consultation.PreferredBreed = PetBreed.Trim();
        }

        Consultation!.MatchMode = selected;
        await _flow.TouchAsync(Consultation);
        await _audit.LogAsync("mode_selected", _auth.CurrentUserId, "Consultation", ConsultationId, new { Mode = selected });

        return selected switch
        {
            IntlMatchMode.Country => RedirectToPage("/Vet/International/Countries", new { consultationId = ConsultationId }),
            IntlMatchMode.Breed => RedirectToPage("/Vet/International/BreedMatch", new { consultationId = ConsultationId }),
            _ => RedirectToPage("/Vet/International/Matches", new { consultationId = ConsultationId })
        };
    }

    private async Task<bool> LoadAsync()
    {
        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation is null) return false;

        if (Consultation.PetId is int pid)
            PetBreed = await _db.Pets.Where(p => p.Id == pid).Select(p => p.Breed).FirstOrDefaultAsync();

        MatchBreed = !string.IsNullOrWhiteSpace(Consultation.PreferredBreed)
            ? Consultation.PreferredBreed
            : PetBreed;

        PreferredCountry = Consultation.PreferredCountry;
        if (!string.IsNullOrWhiteSpace(PreferredCountry))
        {
            var entry = await _countries.GetByIsoAsync(PreferredCountry);
            PreferredCountryName = entry is null ? PreferredCountry : CountryCatalogService.DisplayName(entry);
        }

        if (_auth.CurrentUserId is int uid)
        {
            PreferredLanguage = await _db.Users.AsNoTracking()
                .Where(u => u.Id == uid)
                .Select(u => u.PreferredLanguage)
                .FirstOrDefaultAsync() ?? "es";
        }

        if (string.IsNullOrWhiteSpace(PreferredLanguage))
            PreferredLanguage = CatalogLocalizer.IsEnglish() ? "en" : "es";

        return true;
    }
}
