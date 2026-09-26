using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Services;

namespace WebAppPet.Application.Consultations.Shared;

internal static class ScreeningContextLoader
{
    public static async Task<ScreeningContext?> LoadAsync(AppDbContext db, int clientId, int consultationId, string? next, CancellationToken ct)
    {
        var consultation = await db.OwnedConsultationAsync(clientId, consultationId, ct);
        if (consultation is null)
            return null;

        var pets = await db.Pets.Where(p => p.OwnerId == clientId).OrderBy(p => p.Name).ToListAsync(ct);
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == clientId)
            .Select(u => new { u.CountryCode, u.City, u.Latitude, u.Longitude })
            .FirstOrDefaultAsync(ct);

        return new ScreeningContext(
            consultation,
            ConsultationPath.Normalize(next, consultation.ServiceCatalogCode),
            pets,
            MarketCountry.ResolveForUser(user?.CountryCode, user?.City, user?.Latitude, user?.Longitude),
            string.IsNullOrWhiteSpace(user?.City) ? null : user.City.Trim());
    }
}
