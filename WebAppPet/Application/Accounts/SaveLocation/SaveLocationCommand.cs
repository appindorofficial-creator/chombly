namespace WebAppPet.Application.Accounts.SaveLocation;

/// <param name="Latitude">As posted by the browser; "4,6" and "4.6" are both accepted.</param>
/// <param name="City">Optional; kept as is when blank.</param>
public sealed record SaveLocationCommand(int UserId, string? Latitude, string? Longitude, string? City);
