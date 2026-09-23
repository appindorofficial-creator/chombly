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
    private readonly ServiceCatalogService _catalog;
    private readonly ChomblyCareService _care;
    private readonly VetAuditService _audit;

    public MatchesModel(
        AppDbContext db,
        AuthService auth,
        ConsultationFlowService flow,
        CountryCatalogService countries,
        ServiceCatalogService catalog,
        ChomblyCareService care,
        VetAuditService audit)
    {
        _db = db;
        _auth = auth;
        _flow = flow;
        _countries = countries;
        _catalog = catalog;
        _care = care;
        _audit = audit;
    }

    [BindProperty(SupportsGet = true)]
    public int ConsultationId { get; set; }

    /// <summary>Language filter code (es, en, …). Empty = all.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Lang { get; set; }

    public Consultation? Consultation { get; set; }
    public List<MatchVm> Matches { get; set; } = new();
    public List<LangChip> LanguageChips { get; set; } = new();
    public decimal ConsultPrice { get; set; } = 30m;
    public bool HasCareBenefit { get; set; }
    public int CareRemaining { get; set; }
    public string UserLang { get; set; } = "es";

    public class LangChip
    {
        public string Code { get; set; } = "";
        public string Label { get; set; } = "";
        public int Count { get; set; }
        public bool Active { get; set; }
    }

    public class MatchVm
    {
        public GroomerProfile Provider { get; set; } = null!;
        public string Why { get; set; } = "";
        public string CountryName { get; set; } = "";
        public string LanguagesDisplay { get; set; } = "";
        public bool SpeaksUserLang { get; set; }
        public int Score { get; set; }
        public decimal Price { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Vet/International/Matches?consultationId={ConsultationId}" });

        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation is null) return RedirectToPage("/Vet/Index");
        if (Consultation.PetId is null)
            return RedirectToPage("/Vet/Virtual/Pet", new { consultationId = ConsultationId, next = "intl" });

        if (!string.Equals(Consultation.ServiceCatalogCode, ServiceCatalogCodes.VetIntl30, StringComparison.OrdinalIgnoreCase))
        {
            Consultation.ServiceCatalogCode = ServiceCatalogCodes.VetIntl30;
            Consultation.MatchMode = IntlMatchMode.Best;
            await _flow.TouchAsync(Consultation);
        }

        var intl = await _catalog.GetAsync(ServiceCatalogCodes.VetIntl30);
        ConsultPrice = intl?.Price ?? 30m;

        if (_auth.CurrentUserId is int uid)
        {
            var sub = await _care.GetActiveAsync(uid);
            HasCareBenefit = sub != null && _care.RemainingQuickConsults(sub) > 0;
            CareRemaining = sub != null ? _care.RemainingQuickConsults(sub) : 0;
            UserLang = await _db.Users.AsNoTracking()
                .Where(u => u.Id == uid)
                .Select(u => u.PreferredLanguage)
                .FirstOrDefaultAsync() ?? "es";
        }

        if (string.IsNullOrWhiteSpace(UserLang))
            UserLang = CatalogLocalizer.IsEnglish() ? "en" : "es";

        var activeLang = string.IsNullOrWhiteSpace(Lang) ? UserLang : Lang.Trim().ToLowerInvariant();
        Lang = activeLang;

        var providers = await _db.Groomers.AsNoTracking()
            .Include(g => g.Licenses)
            .Where(g => g.IsActive && g.PublishStatus == BusinessPublishStatus.Approved && g.VetProviderKind == VetProviderKind.InternationalAdvisor)
            .ToListAsync();

        var langCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var countryNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in providers)
        {
            var langs = ParseLanguages(p.SpokenLanguages);
            foreach (var l in langs)
                langCounts[l] = langCounts.GetValueOrDefault(l) + 1;

            var speaks = langs.Any(l => LangMatch(l, activeLang));
            // Soft filter: if chip selected, only show speakers of that language; if none match, fall back to all.
            if (!string.IsNullOrWhiteSpace(activeLang) && langs.Count > 0 && !speaks)
                continue;

            var score = 10;
            var reasons = new List<string>();
            var countryName = await ResolveCountryNameCachedAsync(p.LicenseCountry, countryNames);

            if (speaks)
            {
                score += 40;
                reasons.Add(CatalogLocalizer.Loc($"Habla {activeLang.ToUpperInvariant()}", $"Speaks {activeLang.ToUpperInvariant()}"));
            }

            if (!string.IsNullOrEmpty(countryName))
            {
                score += 8;
                reasons.Add(countryName);
            }

            score += (int)(p.Rating * 2);
            reasons.Add($"★ {p.Rating:0.0}");

            Matches.Add(new MatchVm
            {
                Provider = p,
                Score = score,
                CountryName = countryName,
                SpeaksUserLang = speaks,
                LanguagesDisplay = langs.Count > 0 ? string.Join(", ", langs.Select(l => l.ToUpperInvariant())) : "—",
                Why = string.Join(" · ", reasons),
                Price = ResolveProviderPrice(p, ConsultPrice)
            });
        }

        // If filter emptied the list, show everyone ranked by language affinity.
        if (Matches.Count == 0 && providers.Count > 0)
        {
            foreach (var p in providers)
            {
                var langs = ParseLanguages(p.SpokenLanguages);
                var speaks = langs.Any(l => LangMatch(l, activeLang));
                var countryName = await ResolveCountryNameCachedAsync(p.LicenseCountry, countryNames);
                Matches.Add(new MatchVm
                {
                    Provider = p,
                    Score = speaks ? 50 : 10 + (int)(p.Rating * 2),
                    CountryName = countryName,
                    SpeaksUserLang = speaks,
                    LanguagesDisplay = langs.Count > 0 ? string.Join(", ", langs.Select(l => l.ToUpperInvariant())) : "—",
                    Why = speaks
                        ? CatalogLocalizer.Loc($"Habla {activeLang.ToUpperInvariant()}", $"Speaks {activeLang.ToUpperInvariant()}")
                        : $"★ {p.Rating:0.0}",
                    Price = ResolveProviderPrice(p, ConsultPrice)
                });
            }
        }

        Matches = Matches.OrderByDescending(m => m.SpeaksUserLang).ThenByDescending(m => m.Score).ThenByDescending(m => m.Provider.Rating).ToList();

        LanguageChips = langCounts
            .OrderByDescending(kv => kv.Key.Equals(UserLang, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(kv => kv.Value)
            .Select(kv => new LangChip
            {
                Code = kv.Key.ToLowerInvariant(),
                Label = kv.Key.ToUpperInvariant(),
                Count = kv.Value,
                Active = string.Equals(kv.Key, activeLang, StringComparison.OrdinalIgnoreCase)
            })
            .ToList();

        // Ensure user lang chip exists even if count 0.
        if (!LanguageChips.Any(c => c.Code.Equals(UserLang, StringComparison.OrdinalIgnoreCase)))
        {
            LanguageChips.Insert(0, new LangChip
            {
                Code = UserLang.ToLowerInvariant(),
                Label = UserLang.ToUpperInvariant(),
                Count = 0,
                Active = string.Equals(activeLang, UserLang, StringComparison.OrdinalIgnoreCase)
            });
        }

        await _audit.LogAsync("match_viewed", _auth.CurrentUserId, "Consultation", ConsultationId,
            new { count = Matches.Count, lang = activeLang, care = HasCareBenefit });
        return Page();
    }

    public async Task<IActionResult> OnPostSelectAsync(int providerId)
    {
        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation is null) return RedirectToPage("/Vet/Index");

        var p = await _db.Groomers.AsNoTracking().FirstOrDefaultAsync(g =>
            g.Id == providerId && g.VetProviderKind == VetProviderKind.InternationalAdvisor && g.IsActive && g.PublishStatus == BusinessPublishStatus.Approved);
        if (p is null) return RedirectToPage(new { consultationId = ConsultationId, lang = Lang });

        Consultation.ProviderId = p.Id;
        Consultation.ContextCountry = p.LicenseCountry;
        Consultation.ServiceCatalogCode = ServiceCatalogCodes.VetIntl30;
        Consultation.Status = ConsultationStatus.ProviderSelected;

        if (_auth.CurrentUserId is int uid && await _care.HasQuickConsultAvailableAsync(uid))
            Consultation.UsesCareBenefit = true;
        else
            Consultation.UsesCareBenefit = false;

        await _flow.TouchAsync(Consultation);
        await _audit.LogAsync("profile_viewed", _auth.CurrentUserId, "Groomer", p.Id);

        // Skip purchase choice — Care/price already shown on list; go to schedule.
        return RedirectToPage("/Vet/International/Consent", new { consultationId = ConsultationId });
    }

    private static decimal ResolveProviderPrice(GroomerProfile p, decimal catalogFallback)
        => p.StartingPrice > 0 ? p.StartingPrice : catalogFallback;

    private static List<string> ParseLanguages(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new List<string>();
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(l => l.ToLowerInvariant())
            .Where(l => l.Length > 0)
            .Distinct()
            .ToList();
    }

    private static bool LangMatch(string providerLang, string filter)
        => providerLang.Equals(filter, StringComparison.OrdinalIgnoreCase)
           || providerLang.StartsWith(filter, StringComparison.OrdinalIgnoreCase)
           || filter.StartsWith(providerLang, StringComparison.OrdinalIgnoreCase);

    private async Task<string> ResolveCountryNameCachedAsync(string? iso2, Dictionary<string, string> cache)
    {
        if (string.IsNullOrWhiteSpace(iso2)) return "";
        var key = iso2.Trim().ToUpperInvariant();
        if (cache.TryGetValue(key, out var cached)) return cached;
        var entry = await _countries.GetByIsoAsync(key);
        var name = entry is null ? key : CountryCatalogService.DisplayName(entry);
        cache[key] = name;
        return name;
    }
}
