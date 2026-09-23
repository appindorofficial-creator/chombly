using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;

namespace WebAppPet.Services;

/// <summary>
/// Optional bookable hotel extra: private suite camera during the stay.
/// Distinct from amenity “Cámaras 24/7” (listing filter).
/// </summary>
public static class HotelPrivateCameraExtra
{
    public const string Name = "Cámara privada";
    public const string Description = "Acceso a cámara privada de la suite durante la estadía";

    public const decimal DefaultPriceUsd = 10m;
    public const decimal DefaultPriceCop = 10_000m;

    /// <summary>Legacy alias — US default. Prefer <see cref="DefaultFor"/>.</summary>
    public const decimal DefaultPrice = DefaultPriceUsd;

    public static decimal DefaultFor(string? countryIso = null) =>
        MarketCountry.IsUnitedStates(countryIso ?? AppTimeZones.CurrentCountryCode)
            ? DefaultPriceUsd
            : DefaultPriceCop;

    public static bool Matches(string? name) =>
        string.Equals(name?.Trim(), Name, StringComparison.OrdinalIgnoreCase);

    public static async Task<(bool Enabled, decimal Price)> GetAsync(
        AppDbContext db, int groomerId, string? countryIso = null)
    {
        var fallback = DefaultFor(countryIso);
        var row = await db.ServiceExtras.AsNoTracking()
            .Where(e => e.GroomerId == groomerId && e.IsActive)
            .ToListAsync();
        var hit = row.FirstOrDefault(e => Matches(e.Name));
        return hit == null
            ? (false, fallback)
            : (true, hit.Price < 0 ? fallback : hit.Price);
    }

    public static async Task SyncAsync(
        AppDbContext db, int groomerId, bool enabled, decimal? price = null, string? countryIso = null)
    {
        var existing = await db.ServiceExtras
            .Where(e => e.GroomerId == groomerId)
            .ToListAsync();
        var row = existing.FirstOrDefault(e => Matches(e.Name));
        // Allow $0; only fall back when price is omitted or negative.
        var p = price is null or < 0 ? DefaultFor(countryIso) : price.Value;

        if (enabled)
        {
            if (row == null)
            {
                db.ServiceExtras.Add(new ServiceExtra
                {
                    GroomerId = groomerId,
                    Name = Name,
                    Description = Description,
                    Price = p,
                    IsActive = true
                });
            }
            else
            {
                row.IsActive = true;
                row.Price = p;
                if (string.IsNullOrWhiteSpace(row.Description))
                    row.Description = Description;
            }
        }
        else if (row != null)
        {
            row.IsActive = false;
        }

        await db.SaveChangesAsync();
    }
}
