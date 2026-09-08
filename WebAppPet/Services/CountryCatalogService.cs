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
            q = q.Trim();
            query = query.Where(c =>
                c.Iso2.Contains(q) ||
                c.NameEs.Contains(q) ||
                c.NameEn.Contains(q));
        }

        var list = await query.ToListAsync(ct);
        var en = CatalogLocalizer.IsEnglish();
        return list
            .OrderByDescending(c => c.Status == CountryMarketStatus.Available)
            .ThenBy(c => en ? c.NameEn : c.NameEs)
            .ToList();
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
