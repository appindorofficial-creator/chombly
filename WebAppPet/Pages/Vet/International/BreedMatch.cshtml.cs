using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Vet.International;

public class BreedMatchModel : PageModel
{
    private readonly AuthService _auth;
    private readonly ConsultationFlowService _flow;
    private readonly AppDbContext _db;
    private readonly VetAuditService _audit;

    public BreedMatchModel(AuthService auth, ConsultationFlowService flow, AppDbContext db, VetAuditService audit)
    {
        _auth = auth;
        _flow = flow;
        _db = db;
        _audit = audit;
    }

    [BindProperty(SupportsGet = true)]
    public int ConsultationId { get; set; }

    [BindProperty]
    public string? Breed { get; set; }

    public Consultation? Consultation { get; set; }
    public List<string> SuggestedBreeds { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Vet/International/BreedMatch?consultationId={ConsultationId}" });

        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation is null) return RedirectToPage("/Vet/International/Home");

        if (Consultation.PetId is int pid)
        {
            var petBreed = await _db.Pets.Where(p => p.Id == pid).Select(p => p.Breed).FirstOrDefaultAsync();
            Breed ??= petBreed;
        }

        SuggestedBreeds = await _db.ProviderBreedExpertises.AsNoTracking()
            .Where(e => e.IsVerified)
            .Select(e => e.Breed)
            .Distinct()
            .OrderBy(b => b)
            .Take(40)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation is null) return RedirectToPage("/Vet/International/Home");

        SuggestedBreeds = await _db.ProviderBreedExpertises.AsNoTracking()
            .Where(e => e.IsVerified)
            .Select(e => e.Breed)
            .Distinct()
            .OrderBy(b => b)
            .Take(40)
            .ToListAsync();

        if (string.IsNullOrWhiteSpace(Breed))
        {
            ErrorMessage = CatalogLocalizer.Loc("Indica una raza.", "Enter a breed.");
            return Page();
        }

        Consultation.PreferredBreed = Breed.Trim();
        Consultation.MatchMode = IntlMatchMode.Breed;
        await _flow.TouchAsync(Consultation);
        await _audit.LogAsync("breed_selected", _auth.CurrentUserId, "Consultation", ConsultationId, new { Breed });
        return RedirectToPage("/Vet/International/Matches", new { consultationId = ConsultationId });
    }
}
