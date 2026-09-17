using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;

namespace WebAppPet.Services;

public class CountryCatalogService
{
    private readonly AppDbContext _db;

    public CountryCatalogService(AppDbContext db) => _db = db;

    public async Task<List<CountryCatalogEntry>> SearchAsync(string? q, CancellationToken ct = default)
    {
        var query = _db.CountryCatalog.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var ql = q.Trim().ToLowerInvariant();
            query = query.Where(c =>
                c.Iso2.ToLower().Contains(ql) ||
                c.NameEs.ToLower().Contains(ql) ||
                c.NameEn.ToLower().Contains(ql));
        }

        var list = await query.ToListAsync(ct);
        var en = CatalogLocalizer.IsEnglish();
        return list
            .OrderByDescending(c => c.Status == CountryMarketStatus.Available)
            .ThenBy(c => en ? c.NameEn : c.NameEs)
            .ToList();
    }

    public async Task<CountryCatalogEntry?> GetByIsoAsync(string? iso2, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(iso2)) return null;
        var code = iso2.Trim().ToUpperInvariant();
        return await _db.CountryCatalog.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Iso2 == code, ct);
    }

    /// <summary>Resolve free text or ISO to a catalog ISO2 when possible.</summary>
    public async Task<string?> ResolveIsoAsync(string? raw, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var t = raw.Trim();
        if (t.Length == 2)
        {
            var byIso = await GetByIsoAsync(t, ct);
            return byIso?.Iso2 ?? t.ToUpperInvariant();
        }

        // "Colombia (CO)" → CO
        var paren = t.LastIndexOf('(');
        if (paren >= 0 && t.EndsWith(')') && t.Length - paren >= 4)
        {
            var maybe = t[(paren + 1)..^1].Trim();
            if (maybe.Length == 2)
                return await ResolveIsoAsync(maybe, ct);
        }

        var hits = await SearchAsync(t, ct);
        var exact = hits.FirstOrDefault(c =>
            c.NameEs.Equals(t, StringComparison.OrdinalIgnoreCase) ||
            c.NameEn.Equals(t, StringComparison.OrdinalIgnoreCase) ||
            c.Iso2.Equals(t, StringComparison.OrdinalIgnoreCase));
        return exact?.Iso2 ?? hits.FirstOrDefault()?.Iso2;
    }

    public static string DisplayName(CountryCatalogEntry c)
        => CatalogLocalizer.IsEnglish() ? c.NameEn : c.NameEs;

    public static string StatusLabel(CountryMarketStatus s) => s switch
    {
        CountryMarketStatus.Available => CatalogLocalizer.Loc("Disponible", "Available"),
        CountryMarketStatus.Paused => CatalogLocalizer.Loc("Pausado", "Paused"),
        _ => CatalogLocalizer.Loc("Próximamente", "Coming soon")
    };

    public async Task JoinWaitlistAsync(string iso2, int? userId, string? email, string lang, bool consent, CancellationToken ct = default)
    {
        iso2 = iso2.Trim().ToUpperInvariant();
        _db.CountryWaitlist.Add(new CountryWaitlistEntry
        {
            Iso2 = iso2,
            UserId = userId,
            Email = email,
            PreferredLanguage = lang,
            ConsentMarketing = consent,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }
}
