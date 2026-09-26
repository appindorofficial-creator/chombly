using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Services;

namespace WebAppPet.Application.Consultations.Shared;

/// <summary>The client's home market (ISO code) from their profile country, city or map pin.</summary>
public class ClientHomeCountry
{
    private readonly AppDbContext _db;

    public ClientHomeCountry(AppDbContext db) => _db = db;

    public async Task<string> ResolveAsync(int userId, CancellationToken ct = default)
    {
        var user = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.CountryCode, u.City, u.Latitude, u.Longitude })
            .FirstOrDefaultAsync(ct);
        return MarketCountry.ResolveForUser(user?.CountryCode, user?.City, user?.Latitude, user?.Longitude);
    }
}
