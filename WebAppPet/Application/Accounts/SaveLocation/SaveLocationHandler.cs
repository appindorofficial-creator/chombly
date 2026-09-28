using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Accounts.Shared;
using WebAppPet.Infrastructure.Persistence;
using WebAppPet.Services;

namespace WebAppPet.Application.Accounts.SaveLocation;

public enum SaveLocationOutcome
{
    InvalidCoordinates,
    UserNotFound,
    Saved
}

/// <param name="UsState">US state inferred from the location, for US-only flows; null elsewhere.</param>
public sealed record SaveLocationResult(SaveLocationOutcome Outcome, string? UsState = null, string? City = null);

/// <summary>
/// Stores the browser's current position (and city, when known) for nearby searches.
/// The home country is not changed here; only registration and the profile set it.
/// </summary>
public class SaveLocationHandler
{
    private const int MaxCityLength = 120;

    private readonly AppDbContext _db;

    public SaveLocationHandler(AppDbContext db) => _db = db;

    public async Task<SaveLocationResult> HandleAsync(SaveLocationCommand command, CancellationToken ct = default)
    {
        if (!Coordinates.TryParse(command.Latitude, out var latitude) || !Coordinates.TryParse(command.Longitude, out var longitude)
            || latitude is < -90 or > 90 || longitude is < -180 or > 180)
            return new SaveLocationResult(SaveLocationOutcome.InvalidCoordinates);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == command.UserId, ct);
        if (user is null)
            return new SaveLocationResult(SaveLocationOutcome.UserNotFound);

        user.Latitude = latitude;
        user.Longitude = longitude;
        user.LocationUpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(command.City))
        {
            var city = command.City.Trim();
            user.City = city.Length > MaxCityLength ? city[..MaxCityLength] : city;
        }

        await _db.SaveChangesAsync(ct);

        return new SaveLocationResult(SaveLocationOutcome.Saved, GeoHelper.ResolveUsState(user.City, latitude, longitude), user.City);
    }
}
