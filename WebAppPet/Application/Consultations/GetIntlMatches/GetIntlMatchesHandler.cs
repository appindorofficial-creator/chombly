using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Consultations.GetIntlMatches;

/// <summary>
/// International advisors for the consultation, filtered and ranked by the language the client
/// speaks. Falls back to everyone when nobody speaks the selected language.
/// </summary>
public class GetIntlMatchesHandler
{
    private readonly AppDbContext _db;
    private readonly CountryCatalogService _countries;
    private readonly ServiceCatalogService _catalog;
    private readonly ChomblyCareService _care;
    private readonly VetAuditService _audit;

    public GetIntlMatchesHandler(
        AppDbContext db,
        CountryCatalogService countries,
        ServiceCatalogService catalog,
        ChomblyCareService care,
        VetAuditService audit)
    {
        _db = db;
        _countries = countries;
        _catalog = catalog;
        _care = care;
        _audit = audit;
    }

    public async Task<IntlMatches> HandleAsync(GetIntlMatchesQuery query, CancellationToken ct = default)
    {
        var consultation = await _db.OwnedConsultationAsync(query.ClientId, query.ConsultationId, ct);
        if (consultation is null)
            return IntlMatches.RedirectTo(ConsultationStep.VetHome);
        if (consultation.PetId is null)
            return IntlMatches.RedirectTo(ConsultationStep.Pet);

        if (!string.Equals(consultation.ServiceCatalogCode, ServiceCatalogCodes.VetIntl30, StringComparison.OrdinalIgnoreCase))
        {
            consultation.ServiceCatalogCode = ServiceCatalogCodes.VetIntl30;
            consultation.MatchMode = IntlMatchMode.Best;
            await _db.TouchAsync(consultation, ct);
        }

        var consultPrice = (await _catalog.GetAsync(ServiceCatalogCodes.VetIntl30, ct))?.Price ?? 30m;

        var subscription = await _care.GetActiveAsync(query.ClientId, ct);
        var careRemaining = subscription != null ? _care.RemainingQuickConsults(subscription) : 0;
        var hasCare = careRemaining > 0;

        var userLang = await _db.Users.AsNoTracking()
            .Where(u => u.Id == query.ClientId)
            .Select(u => u.PreferredLanguage)
            .FirstOrDefaultAsync(ct) ?? "es";
        if (string.IsNullOrWhiteSpace(userLang))
            userLang = CatalogLocalizer.IsEnglish() ? "en" : "es";

        var activeLang = string.IsNullOrWhiteSpace(query.Lang) ? userLang : query.Lang.Trim().ToLowerInvariant();

        var providers = await _db.Groomers.AsNoTracking()
            .Include(g => g.Licenses)
            .Where(g => g.IsActive && g.PublishStatus == BusinessPublishStatus.Approved && g.VetProviderKind == VetProviderKind.InternationalAdvisor)
            .ToListAsync(ct);

        var langCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var countryNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var matches = new List<IntlMatch>();

        foreach (var p in providers)
        {
            var langs = ParseLanguages(p.SpokenLanguages);
            foreach (var l in langs)
                langCounts[l] = langCounts.GetValueOrDefault(l) + 1;

            var speaks = langs.Any(l => LangMatch(l, activeLang));
            if (!string.IsNullOrWhiteSpace(activeLang) && langs.Count > 0 && !speaks)
                continue;

            var score = 10;
            var reasons = new List<string>();
            var countryName = await CountryNameAsync(p.LicenseCountry, countryNames, ct);

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

            matches.Add(new IntlMatch(p, string.Join(" · ", reasons), countryName, LanguagesDisplay(langs), speaks, score,
                ProviderPrice(p, consultPrice)));
        }

        if (matches.Count == 0 && providers.Count > 0)
        {
            foreach (var p in providers)
            {
                var langs = ParseLanguages(p.SpokenLanguages);
                var speaks = langs.Any(l => LangMatch(l, activeLang));
                var countryName = await CountryNameAsync(p.LicenseCountry, countryNames, ct);
                var why = speaks
                    ? CatalogLocalizer.Loc($"Habla {activeLang.ToUpperInvariant()}", $"Speaks {activeLang.ToUpperInvariant()}")
                    : $"★ {p.Rating:0.0}";
                matches.Add(new IntlMatch(p, why, countryName, LanguagesDisplay(langs), speaks,
                    speaks ? 50 : 10 + (int)(p.Rating * 2), ProviderPrice(p, consultPrice)));
            }
        }

        matches = matches
            .OrderByDescending(m => m.SpeaksUserLang)
            .ThenByDescending(m => m.Score)
            .ThenByDescending(m => m.Provider.Rating)
            .ToList();

        var chips = langCounts
            .OrderByDescending(kv => kv.Key.Equals(userLang, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(kv => kv.Value)
            .Select(kv => new LanguageChip(kv.Key.ToLowerInvariant(), kv.Key.ToUpperInvariant(), kv.Value,
                string.Equals(kv.Key, activeLang, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (!chips.Any(c => c.Code.Equals(userLang, StringComparison.OrdinalIgnoreCase)))
        {
            chips.Insert(0, new LanguageChip(userLang.ToLowerInvariant(), userLang.ToUpperInvariant(), 0,
                string.Equals(activeLang, userLang, StringComparison.OrdinalIgnoreCase)));
        }

        await _audit.LogAsync("match_viewed", query.ClientId, "Consultation", query.ConsultationId,
            new { count = matches.Count, lang = activeLang, care = hasCare }, ct);

        return new IntlMatches(null, matches, chips, consultPrice, hasCare, careRemaining, activeLang);
    }

    private static decimal ProviderPrice(GroomerProfile p, decimal catalogFallback)
        => p.StartingPrice > 0 ? p.StartingPrice : catalogFallback;

    private static string LanguagesDisplay(List<string> langs)
        => langs.Count > 0 ? string.Join(", ", langs.Select(l => l.ToUpperInvariant())) : "—";

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

    private async Task<string> CountryNameAsync(string? iso2, Dictionary<string, string> cache, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(iso2)) return "";
        var key = iso2.Trim().ToUpperInvariant();
        if (cache.TryGetValue(key, out var cached)) return cached;
        var entry = await _countries.GetByIsoAsync(key, ct);
        var name = entry is null ? key : CountryCatalogService.DisplayName(entry);
        cache[key] = name;
        return name;
    }
}
