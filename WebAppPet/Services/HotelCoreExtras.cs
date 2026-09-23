using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;

namespace WebAppPet.Services;

/// <summary>
/// Core hotel booking extras every hotel offers (bath + medication).
/// Prices are set by the business (including $0).
/// </summary>
public static class HotelCoreExtras
{
    public const string BathName = "Baño adicional";
    public const string BathDescription = "Baño extra durante la estadía";
    public const decimal BathDefaultPrice = 20m;

    public const string MedsName = "Administración de medicamentos";
    public const string MedsDescription = "Si tu mascota necesita medicación durante la estadía";
    public const decimal MedsDefaultPrice = 5m;

    public static bool MatchesBath(string? name) =>
        string.Equals(name?.Trim(), BathName, StringComparison.OrdinalIgnoreCase);

    public static bool MatchesMeds(string? name) =>
        string.Equals(name?.Trim(), MedsName, StringComparison.OrdinalIgnoreCase);

    public static bool IsCore(string? name) => MatchesBath(name) || MatchesMeds(name);

    public static bool IsManagedHotelExtra(string? name) =>
        IsCore(name) || HotelPrivateCameraExtra.Matches(name);

    public static decimal NormalizePrice(decimal? price, decimal fallback) =>
        price is null or < 0 ? fallback : price.Value;

    public static async Task<(decimal BathPrice, decimal MedsPrice)> GetPricesAsync(AppDbContext db, int groomerId)
    {
        var rows = await db.ServiceExtras.AsNoTracking()
            .Where(e => e.GroomerId == groomerId && e.IsActive)
            .ToListAsync();
        var bath = rows.FirstOrDefault(e => MatchesBath(e.Name));
        var meds = rows.FirstOrDefault(e => MatchesMeds(e.Name));
        return (
            bath != null ? NormalizePrice(bath.Price, BathDefaultPrice) : BathDefaultPrice,
            meds != null ? NormalizePrice(meds.Price, MedsDefaultPrice) : MedsDefaultPrice
        );
    }

    /// <summary>Create or update bath + meds extras with the given prices (min $0).</summary>
    public static async Task SyncAsync(AppDbContext db, int groomerId, decimal? bathPrice, decimal? medsPrice)
    {
        var bath = NormalizePrice(bathPrice, BathDefaultPrice);
        var meds = NormalizePrice(medsPrice, MedsDefaultPrice);
        await UpsertAsync(db, groomerId, BathName, BathDescription, bath);
        await UpsertAsync(db, groomerId, MedsName, MedsDescription, meds);
        await db.SaveChangesAsync();
    }

    /// <summary>Ensure every hotel has bath + meds rows; does not overwrite existing prices.</summary>
    public static async Task EnsureMissingAsync(AppDbContext db, IEnumerable<int> hotelIds)
    {
        var ids = hotelIds.Distinct().Where(id => id > 0).ToList();
        if (ids.Count == 0) return;

        var existing = await db.ServiceExtras
            .AsNoTracking()
            .Where(e => ids.Contains(e.GroomerId))
            .Select(e => new { e.GroomerId, e.Name })
            .ToListAsync();

        var toAdd = new List<ServiceExtra>();
        foreach (var hotelId in ids)
        {
            if (!existing.Any(e => e.GroomerId == hotelId && MatchesBath(e.Name)))
            {
                toAdd.Add(new ServiceExtra
                {
                    GroomerId = hotelId,
                    Name = BathName,
                    Description = BathDescription,
                    Price = BathDefaultPrice,
                    IsActive = true
                });
            }

            if (!existing.Any(e => e.GroomerId == hotelId && MatchesMeds(e.Name)))
            {
                toAdd.Add(new ServiceExtra
                {
                    GroomerId = hotelId,
                    Name = MedsName,
                    Description = MedsDescription,
                    Price = MedsDefaultPrice,
                    IsActive = true
                });
            }
        }

        if (toAdd.Count == 0) return;
        db.ServiceExtras.AddRange(toAdd);
        await db.SaveChangesAsync();
    }

    private static async Task UpsertAsync(
        AppDbContext db, int groomerId, string name, string description, decimal price)
    {
        var row = await db.ServiceExtras
            .FirstOrDefaultAsync(e => e.GroomerId == groomerId && e.Name == name);
        if (row == null)
        {
            // Case-insensitive match for legacy rows with different casing.
            var siblings = await db.ServiceExtras.Where(e => e.GroomerId == groomerId).ToListAsync();
            row = siblings.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        if (row == null)
        {
            db.ServiceExtras.Add(new ServiceExtra
            {
                GroomerId = groomerId,
                Name = name,
                Description = description,
                Price = price,
                IsActive = true
            });
            return;
        }

        row.Name = name;
        row.IsActive = true;
        row.Price = price;
        if (string.IsNullOrWhiteSpace(row.Description))
            row.Description = description;
    }
}
