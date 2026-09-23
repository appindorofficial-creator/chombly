using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;

namespace WebAppPet.Services;

/// <summary>
/// Core hotel booking extras every hotel offers (bath + medication).
/// Prices are set by the business (including $0); defaults follow home market (USD / COP).
/// </summary>
public static class HotelCoreExtras
{
    public const string BathName = "Baño adicional";
    public const string BathDescription = "Baño extra durante la estadía";
    public const string MedsName = "Administración de medicamentos";
    public const string MedsDescription = "Si tu mascota necesita medicación durante la estadía";

    /// <summary>US default bath add-on (USD).</summary>
    public const decimal BathDefaultPriceUsd = 20m;
    /// <summary>US default meds add-on (USD).</summary>
    public const decimal MedsDefaultPriceUsd = 5m;
    /// <summary>Colombia default bath add-on (COP).</summary>
    public const decimal BathDefaultPriceCop = 20_000m;
    /// <summary>Colombia default meds add-on (COP).</summary>
    public const decimal MedsDefaultPriceCop = 5_000m;

    /// <summary>Legacy alias — US bath default. Prefer <see cref="BathDefaultFor"/>.</summary>
    public const decimal BathDefaultPrice = BathDefaultPriceUsd;
    /// <summary>Legacy alias — US meds default. Prefer <see cref="MedsDefaultFor"/>.</summary>
    public const decimal MedsDefaultPrice = MedsDefaultPriceUsd;

    public static decimal BathDefaultFor(string? countryIso = null) =>
        MarketCountry.IsUnitedStates(countryIso ?? AppTimeZones.CurrentCountryCode)
            ? BathDefaultPriceUsd
            : BathDefaultPriceCop;

    public static decimal MedsDefaultFor(string? countryIso = null) =>
        MarketCountry.IsUnitedStates(countryIso ?? AppTimeZones.CurrentCountryCode)
            ? MedsDefaultPriceUsd
            : MedsDefaultPriceCop;

    /// <summary>Typical size-tier step when creating a service (USD $10 / COP $5.000).</summary>
    public static decimal SizeStepFor(string? countryIso = null) =>
        MarketCountry.IsUnitedStates(countryIso ?? AppTimeZones.CurrentCountryCode)
            ? 10m
            : 5_000m;

    public static bool MatchesBath(string? name) =>
        string.Equals(name?.Trim(), BathName, StringComparison.OrdinalIgnoreCase);

    public static bool MatchesMeds(string? name) =>
        string.Equals(name?.Trim(), MedsName, StringComparison.OrdinalIgnoreCase);

    public static bool IsCore(string? name) => MatchesBath(name) || MatchesMeds(name);

    public static bool IsManagedHotelExtra(string? name) =>
        IsCore(name) || HotelPrivateCameraExtra.Matches(name);

    public static decimal NormalizePrice(decimal? price, decimal fallback) =>
        price is null or < 0 ? fallback : price.Value;

    public static async Task<(decimal BathPrice, decimal MedsPrice)> GetPricesAsync(
        AppDbContext db, int groomerId, string? countryIso = null)
    {
        var bathDefault = BathDefaultFor(countryIso);
        var medsDefault = MedsDefaultFor(countryIso);
        var rows = await db.ServiceExtras.AsNoTracking()
            .Where(e => e.GroomerId == groomerId && e.IsActive)
            .ToListAsync();
        var bath = rows.FirstOrDefault(e => MatchesBath(e.Name));
        var meds = rows.FirstOrDefault(e => MatchesMeds(e.Name));
        return (
            bath != null ? NormalizePrice(bath.Price, bathDefault) : bathDefault,
            meds != null ? NormalizePrice(meds.Price, medsDefault) : medsDefault
        );
    }

    /// <summary>Create or update bath + meds extras with the given prices (min $0).</summary>
    public static async Task SyncAsync(
        AppDbContext db, int groomerId, decimal? bathPrice, decimal? medsPrice, string? countryIso = null)
    {
        var bath = NormalizePrice(bathPrice, BathDefaultFor(countryIso));
        var meds = NormalizePrice(medsPrice, MedsDefaultFor(countryIso));
        await UpsertAsync(db, groomerId, BathName, BathDescription, bath);
        await UpsertAsync(db, groomerId, MedsName, MedsDescription, meds);
        await db.SaveChangesAsync();
    }

    /// <summary>Ensure every hotel has bath + meds rows; does not overwrite existing prices.</summary>
    public static async Task EnsureMissingAsync(AppDbContext db, IEnumerable<int> hotelIds)
    {
        var ids = hotelIds.Distinct().Where(id => id > 0).ToList();
        if (ids.Count == 0) return;

        var hotels = await db.Groomers.AsNoTracking()
            .Include(g => g.User)
            .Where(g => ids.Contains(g.Id))
            .Select(g => new { g.Id, Country = g.User != null ? g.User.CountryCode : g.LicenseCountry })
            .ToListAsync();

        var existing = await db.ServiceExtras
            .AsNoTracking()
            .Where(e => ids.Contains(e.GroomerId))
            .Select(e => new { e.GroomerId, e.Name })
            .ToListAsync();

        var toAdd = new List<ServiceExtra>();
        foreach (var hotel in hotels)
        {
            var bathDefault = BathDefaultFor(hotel.Country);
            var medsDefault = MedsDefaultFor(hotel.Country);
            if (!existing.Any(e => e.GroomerId == hotel.Id && MatchesBath(e.Name)))
            {
                toAdd.Add(new ServiceExtra
                {
                    GroomerId = hotel.Id,
                    Name = BathName,
                    Description = BathDescription,
                    Price = bathDefault,
                    IsActive = true
                });
            }

            if (!existing.Any(e => e.GroomerId == hotel.Id && MatchesMeds(e.Name)))
            {
                toAdd.Add(new ServiceExtra
                {
                    GroomerId = hotel.Id,
                    Name = MedsName,
                    Description = MedsDescription,
                    Price = medsDefault,
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
