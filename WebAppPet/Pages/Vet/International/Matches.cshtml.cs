using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Vet.International;

public class MatchesModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly ConsultationFlowService _flow;
    private readonly VetAuditService _audit;

    public MatchesModel(AppDbContext db, AuthService auth, ConsultationFlowService flow, VetAuditService audit)
    {
        _db = db;
        _auth = auth;
        _flow = flow;
        _audit = audit;
    }

    [BindProperty(SupportsGet = true)]
    public int ConsultationId { get; set; }

    public Consultation? Consultation { get; set; }
    public List<MatchVm> Matches { get; set; } = new();

    public class MatchVm
    {
        public GroomerProfile Provider { get; set; } = null!;
        public string Why { get; set; } = "";
        public int Score { get; set; }
        public List<string> VerifiedBreeds { get; set; } = new();
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Vet/International/Matches?consultationId={ConsultationId}" });

        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation is null) return RedirectToPage("/Vet/International/Home");

        var providers = await _db.Groomers.AsNoTracking()
            .Include(g => g.Licenses)
            .Where(g => g.IsPublished && g.VetProviderKind == VetProviderKind.InternationalAdvisor)
            .ToListAsync();

        var breedMap = await _db.ProviderBreedExpertises.AsNoTracking()
            .Where(e => e.IsVerified)
            .GroupBy(e => e.GroomerId)
            .ToDictionaryAsync(g => g.Key, g => g.Select(x => x.Breed).ToList());

        foreach (var p in providers)
        {
            var breeds = breedMap.GetValueOrDefault(p.Id) ?? new List<string>();
            var score = 10;
            var reasons = new List<string>();

            if (!string.IsNullOrEmpty(Consultation.PreferredCountry) &&
                string.Equals(p.LicenseCountry, Consultation.PreferredCountry, StringComparison.OrdinalIgnoreCase))
            {
                score += 40;
                reasons.Add(CatalogLocalizer.Loc($"País {p.LicenseCountry}", $"Country {p.LicenseCountry}"));
            }
            else if (!string.IsNullOrEmpty(p.LicenseCountry))
            {
                score += 5;
                reasons.Add(CatalogLocalizer.Loc($"Licencia {p.LicenseCountry}", $"License {p.LicenseCountry}"));
            }

            if (!string.IsNullOrWhiteSpace(Consultation.PreferredBreed))
            {
                var hit = breeds.FirstOrDefault(b =>
                    b.Contains(Consultation.PreferredBreed, StringComparison.OrdinalIgnoreCase) ||
                    Consultation.PreferredBreed.Contains(b, StringComparison.OrdinalIgnoreCase));
                if (hit != null)
                {
                    score += 50;
                    reasons.Add(CatalogLocalizer.Loc($"Experiencia verificada: {hit}", $"Verified expertise: {hit}"));
                }
            }

            if (!string.IsNullOrEmpty(p.SpokenLanguages))
            {
                score += 10;
                reasons.Add(CatalogLocalizer.Loc($"Idiomas {p.SpokenLanguages}", $"Languages {p.SpokenLanguages}"));
            }

            score += (int)(p.Rating * 2);
            reasons.Add($"★ {p.Rating:0.0}");

            Matches.Add(new MatchVm
            {
                Provider = p,
                Score = score,
                Why = string.Join(" · ", reasons),
                VerifiedBreeds = breeds
            });
        }

        Matches = Matches.OrderByDescending(m => m.Score).ThenByDescending(m => m.Provider.Rating).ToList();
        await _audit.LogAsync("match_viewed", _auth.CurrentUserId, "Consultation", ConsultationId, new { count = Matches.Count });
        return Page();
    }

    public async Task<IActionResult> OnPostSelectAsync(int providerId)
    {
        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation is null) return RedirectToPage("/Vet/International/Home");

        var p = await _db.Groomers.AsNoTracking().FirstOrDefaultAsync(g =>
            g.Id == providerId && g.VetProviderKind == VetProviderKind.InternationalAdvisor && g.IsPublished);
        if (p is null) return RedirectToPage(new { consultationId = ConsultationId });

        Consultation.ProviderId = p.Id;
        Consultation.ContextCountry = p.LicenseCountry;
        Consultation.ServiceCatalogCode = ServiceCatalogCodes.VetIntl30;
        Consultation.Status = ConsultationStatus.ProviderSelected;
        await _flow.TouchAsync(Consultation);
        await _audit.LogAsync("profile_viewed", _auth.CurrentUserId, "Groomer", p.Id);

        return RedirectToPage("/Vet/International/PurchaseOption", new { consultationId = ConsultationId });
    }
}
