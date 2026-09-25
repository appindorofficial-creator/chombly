using WebAppPet.Models;

namespace WebAppPet.Application.Accounts.UpdateProfile;

/// <summary>Leave both password fields blank to keep the current password.</summary>
public sealed record UpdateProfileCommand(
    int UserId,
    string? FullName,
    string? Email,
    string? Phone,
    string? City,
    string? Latitude,
    string? Longitude,
    string? NewPassword,
    string? ConfirmPassword);

public enum UpdateProfileError
{
    None,
    NotFound,
    PhoneInvalid,
    NameRequired,
    EmailInvalid,
    EmailTaken,
    CityLocationRequired,
    PasswordWeak,
    PasswordMismatch
}

/// <param name="Latitude">Coordinates saved for the city, or null when the city is blank.</param>
public sealed record UpdateProfileResult(UpdateProfileError Error, AppUser? User, double? Latitude, double? Longitude)
{
    public bool Success => Error == UpdateProfileError.None;

    public static UpdateProfileResult Fail(UpdateProfileError error) => new(error, null, null, null);
}
