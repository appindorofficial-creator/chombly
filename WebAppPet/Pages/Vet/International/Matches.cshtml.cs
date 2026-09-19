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
    private readonly CountryCatalogService _countries;
    private readonly VetAuditService _audit;

    public MatchesModel(
        AppDbContext db,
        AuthService auth,
        ConsultationFlowService flow,
        CountryCatalogService countries,
        VetAuditService audit)
    {
        _db = db;
        _auth = auth;
        _flow = flow;
        _countries = countries;
        _audit = audit;
    }

    [BindProperty(SupportsGet = true)]
    public int ConsultationId { get; set; }

    public Consultation? Consultation { get; set; }
    public string? PreferredCountryName { get; set; }
    public List<MatchVm> Matches { get; set; } = new();

    public class MatchVm
    {
        public GroomerProfile Provider { get; set; } = null!;
        public string Why { get; set; } = "";
        public string CountryName { get; set; } = "";
        public bool MatchesPreferredCountry { get; set; }
        public int Score { get; set; }
        public List<string> VerifiedBreeds { get; set; } = new();
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Vet/International/Matches?consultationId={ConsultationId}" });

        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation is null) return RedirectToPage("/Vet/International/Home");

        PreferredCountryName = await ResolveCountryNameAsync(Consultation.PreferredCountry);

        var userLang = "es";
        if (_auth.CurrentUserId is int uid)
        {
            userLang = await _db.Users.AsNoTracking()
                .Where(u => u.Id == uid)
                .Select(u => u.PreferredLanguage)
                .FirstOrDefaultAsync() ?? "es";
        }
        if (string.IsNullOrWhiteSpace(userLang))
            userLang = CatalogLocalizer.IsEnglish() ? "en" : "es";

        // Prefer pet breed when ranking if no explicit PreferredBreed yet.
        if (string.IsNullOrWhiteSpace(Consultation.PreferredBreed) && Consultation.PetId is int petId)
        {
            var petBreed = await _db.Pets.AsNoTracking()
                .Where(p => p.Id == petId)
                .Select(p => p.Breed)
                .FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(petBreed))
                Consultation.PreferredBreed = petBreed;
        }

        var preferCountry = !string.IsNullOrWhiteSpace(Consultation.PreferredCountry);
        var preferBreed = !string.IsNullOrWhiteSpace(Consultation.PreferredBreed);

        var providers = await _db.Groomers.AsNoTracking()
            .Include(g => g.Licenses)
            .Where(g => g.IsActive && g.PublishStatus == BusinessPublishStatus.Approved && g.VetProviderKind == VetProviderKind.InternationalAdvisor)
            .ToListAsync();

        if (preferCountry)
        {
            providers = providers
                .Where(g => string.Equals(g.LicenseCountry, Consultation.PreferredCountry, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var breedMap = await _db.ProviderBreedExpertises.AsNoTracking()
            .Where(e => e.IsVerified)
            .GroupBy(e => e.GroomerId)
            .ToDictionaryAsync(g => g.Key, g => g.Select(x => x.Breed).ToList());

        var countryNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in providers)
        {
            var breeds = breedMap.GetValueOrDefault(p.Id) ?? new List<string>();
            string? breedHit = null;
            if (preferBreed)
            {
                breedHit = breeds.FirstOrDefault(b =>
                    b.Contains(Consultation.PreferredBreed!, StringComparison.OrdinalIgnoreCase) ||
                    Consultation.PreferredBreed!.Contains(b, StringComparison.OrdinalIgnoreCase));
                if (breedHit is null)
                    continue; // hard filter by selected / pet breed
            }

            var score = 10;
            var reasons = new List<string>();
            var countryName = await ResolveCountryNameCachedAsync(p.LicenseCountry, countryNames);
            var matchesPreferredCountry = preferCountry;

            if (matchesPreferredCountry && !string.IsNullOrEmpty(countryName))
            {
                score += 40;
                reasons.Add(CatalogLocalizer.Loc(
                    $"País: {countryName}",
                    $"Country: {countryName}"));
            }
            else if (!string.IsNullOrEmpty(countryName))
            {
                score += 5;
            }

            if (breedHit != null)
            {
                score += 50;
                reasons.Add(CatalogLocalizer.Loc($"Experiencia verificada: {breedHit}", $"Verified expertise: {breedHit}"));
            }

            if (!string.IsNullOrEmpty(p.SpokenLanguages))
            {
                var langs = p.SpokenLanguages.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var langHit = langs.Any(l =>
                    l.Equals(userLang, StringComparison.OrdinalIgnoreCase) ||
                    l.StartsWith(userLang, StringComparison.OrdinalIgnoreCase));
                if (langHit)
                {
                    score += 25;
                    reasons.Add(CatalogLocalizer.Loc($"Idioma {userLang}", $"Language {userLang}"));
                }
                else
                {
                    score += 5;
                    reasons.Add(CatalogLocalizer.Loc($"Idiomas {p.SpokenLanguages}", $"Languages {p.SpokenLanguages}"));
                }
            }

            score += (int)(p.Rating * 2);
            reasons.Add($"★ {p.Rating:0.0}");

            Matches.Add(new MatchVm
            {
                Provider = p,
                Score = score,
                CountryName = countryName,
                MatchesPreferredCountry = matchesPreferredCountry,
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
            g.Id == providerId && g.VetProviderKind == VetProviderKind.InternationalAdvisor && g.IsActive && g.PublishStatus == BusinessPublishStatus.Approved);
        if (p is null) return RedirectToPage(new { consultationId = ConsultationId });

        Consultation.ProviderId = p.Id;
        Consultation.ContextCountry = p.LicenseCountry;
        Consultation.ServiceCatalogCode = ServiceCatalogCodes.VetIntl30;
        Consultation.Status = ConsultationStatus.ProviderSelected;
        await _flow.TouchAsync(Consultation);
        await _audit.LogAsync("profile_viewed", _auth.CurrentUserId, "Groomer", p.Id);

        return RedirectToPage("/Vet/International/PurchaseOption", new { consultationId = ConsultationId });
    }

    private async Task<string> ResolveCountryNameAsync(string? iso2)
    {
        if (string.IsNullOrWhiteSpace(iso2)) return "";
        var entry = await _countries.GetByIsoAsync(iso2);
        return entry is null ? iso2.Trim().ToUpperInvariant() : CountryCatalogService.DisplayName(entry);
    }

    private async Task<string> ResolveCountryNameCachedAsync(string? iso2, Dictionary<string, string> cache)
    {
        if (string.IsNullOrWhiteSpace(iso2)) return "";
        var key = iso2.Trim().ToUpperInvariant();
        if (cache.TryGetValue(key, out var cached)) return cached;
        var name = await ResolveCountryNameAsync(key);
        cache[key] = name;
        return name;
    }
}
