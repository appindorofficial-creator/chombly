using WebAppPet.Models;

namespace WebAppPet.Application.Accounts.Register;

/// <param name="DefaultCity">City label stored when the map picker gave coordinates but no city name.</param>
public sealed record RegisterCommand(
    string? FullName,
    string? Email,
    string? Phone,
    string? City,
    string? Latitude,
    string? Longitude,
    string? Password,
    string DefaultCity);

public enum RegisterError
{
    None,
    NameRequired,
    EmailInvalid,
    PhoneRequired,
    PhoneInvalid,
    PasswordWeak,
    LocationRequired,
    EmailTaken
}

public sealed record RegisterResult(RegisterError Error, AppUser? User)
{
    public bool Success => Error == RegisterError.None;

    public static RegisterResult Fail(RegisterError error) => new(error, null);
}
