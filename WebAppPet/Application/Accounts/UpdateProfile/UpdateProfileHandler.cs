using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Accounts.Shared;
using WebAppPet.Data;
using WebAppPet.Infrastructure.Security;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Accounts.UpdateProfile;

/// <summary>
/// Updates profile data and, optionally, the password. Everything is validated before anything is saved.
/// The page refreshes the auth cookie so a new name or email shows up right away.
/// </summary>
public class UpdateProfileHandler
{
    private readonly AppDbContext _db;

    public UpdateProfileHandler(AppDbContext db) => _db = db;

    public async Task<UpdateProfileResult> HandleAsync(UpdateProfileCommand command, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == command.UserId, ct);
        if (user is null)
            return UpdateProfileResult.Fail(UpdateProfileError.NotFound);

        var fullName = (command.FullName ?? "").Trim();
        var email = (command.Email ?? "").Trim().ToLowerInvariant();
        var phone = string.IsNullOrWhiteSpace(command.Phone) ? null : command.Phone.Trim();
        var city = (command.City ?? "").Trim();

        if (!PhoneValidator.TryNormalize(phone, out var phoneNorm))
            return UpdateProfileResult.Fail(UpdateProfileError.PhoneInvalid);

        if (string.IsNullOrWhiteSpace(fullName))
            return UpdateProfileResult.Fail(UpdateProfileError.NameRequired);

        if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email))
            return UpdateProfileResult.Fail(UpdateProfileError.EmailInvalid);

        if (await _db.Users.AnyAsync(u => u.Email == email && u.Id != user.Id, ct))
            return UpdateProfileResult.Fail(UpdateProfileError.EmailTaken);

        if (!TryResolveCityCoordinates(user, city, command.Latitude, command.Longitude, out var lat, out var lng))
            return UpdateProfileResult.Fail(UpdateProfileError.CityLocationRequired);

        var changingPassword = !string.IsNullOrWhiteSpace(command.NewPassword) || !string.IsNullOrWhiteSpace(command.ConfirmPassword);
        if (changingPassword)
        {
            if (string.IsNullOrWhiteSpace(command.NewPassword) || !PasswordPolicy.IsValid(command.NewPassword))
                return UpdateProfileResult.Fail(UpdateProfileError.PasswordWeak);

            if (!string.Equals(command.NewPassword, command.ConfirmPassword, StringComparison.Ordinal))
                return UpdateProfileResult.Fail(UpdateProfileError.PasswordMismatch);

            user.PasswordHash = PasswordHasher.Hash(command.NewPassword);
        }

        user.FullName = fullName;
        user.Email = email;
        user.Phone = phoneNorm;
        user.City = city;
        if (lat is not null && lng is not null)
        {
            user.Latitude = lat;
            user.Longitude = lng;
            user.LocationUpdatedAt = DateTime.UtcNow;
            MarketCountry.ApplyFromLocation(user);
        }
        else if (string.IsNullOrWhiteSpace(user.CountryCode))
        {
            MarketCountry.ApplyFromLocation(user);
        }

        await _db.SaveChangesAsync(ct);
        return new UpdateProfileResult(UpdateProfileError.None, user, lat, lng);
    }

    /// <summary>
    /// A blank city needs no coordinates. Otherwise use the posted map coordinates, or keep the stored
    /// ones when the city did not change.
    /// </summary>
    private static bool TryResolveCityCoordinates(
        AppUser user, string city, string? latitude, string? longitude, out double? lat, out double? lng)
    {
        lat = null;
        lng = null;

        if (string.IsNullOrWhiteSpace(city))
            return true;

        if (Coordinates.TryParsePair(latitude, longitude, out var postedLat, out var postedLng))
        {
            lat = postedLat;
            lng = postedLng;
            return true;
        }

        var cityUnchanged = string.Equals(city, user.City?.Trim(), StringComparison.OrdinalIgnoreCase);
        var hasStoredCoords = user.Latitude is double slat && user.Longitude is double slng
                              && Coordinates.IsValidPair(slat, slng);
        if (cityUnchanged && hasStoredCoords)
        {
            lat = user.Latitude;
            lng = user.Longitude;
            return true;
        }

        return false;
    }
}
