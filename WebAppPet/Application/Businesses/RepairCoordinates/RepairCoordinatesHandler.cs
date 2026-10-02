using Microsoft.EntityFrameworkCore;
using WebAppPet.Domain.Markets;
using WebAppPet.Infrastructure.Persistence;

namespace WebAppPet.Application.Businesses.RepairCoordinates;

/// <summary>
/// Fixes coordinates whose decimal point was read as a thousands separator (see <see cref="GeoHelper.TryRepairCoordinates"/>).
/// True when they were changed.
/// </summary>
public class RepairCoordinatesHandler
{
    private readonly AppDbContext _db;

    public RepairCoordinatesHandler(AppDbContext db) => _db = db;

    public async Task<bool> HandleAsync(RepairCoordinatesCommand command, CancellationToken ct = default)
    {
        var business = await _db.Groomers.FirstOrDefaultAsync(g => g.Id == command.BusinessId, ct);
        if (business is null)
            return false;

        var lat = business.Latitude;
        var lng = business.Longitude;
        if (!GeoHelper.TryRepairCoordinates(ref lat, ref lng))
            return false;

        business.Latitude = lat;
        business.Longitude = lng;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
