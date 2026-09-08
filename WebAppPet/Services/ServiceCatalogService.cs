using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;

namespace WebAppPet.Services;

public class ServiceCatalogService
{
    private readonly AppDbContext _db;

    public ServiceCatalogService(AppDbContext db) => _db = db;

    public async Task<ServiceCatalogItem?> GetAsync(string code, CancellationToken ct = default)
        => await _db.ServiceCatalog.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Code == code && s.IsActive, ct);

    public async Task<List<ServiceCatalogItem>> ListActiveAsync(CancellationToken ct = default)
        => await _db.ServiceCatalog.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Price)
            .ToListAsync(ct);

    public static string DisplayName(ServiceCatalogItem item)
        => CatalogLocalizer.IsEnglish() ? item.NameEn : item.NameEs;

    public static string DisplayScope(ServiceCatalogItem item)
        => CatalogLocalizer.IsEnglish() ? item.ScopeEn : item.ScopeEs;
}
