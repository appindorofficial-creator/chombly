using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Accounts.Shared;
using WebAppPet.Data;
using WebAppPet.Infrastructure.Security;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Accounts.Register;

/// <summary>Creates a client account with a welcome notification. The page signs the user in.</summary>
public class RegisterHandler
{
    private readonly AppDbContext _db;

    public RegisterHandler(AppDbContext db) => _db = db;

    public async Task<RegisterResult> HandleAsync(RegisterCommand command, CancellationToken ct = default)
    {
        var fullName = (command.FullName ?? "").Trim();
        var email = (command.Email ?? "").Trim().ToLowerInvariant();
        var phone = (command.Phone ?? "").Trim();
        var city = (command.City ?? "").Trim();

        if (string.IsNullOrWhiteSpace(fullName))
            return RegisterResult.Fail(RegisterError.NameRequired);

        if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email))
            return RegisterResult.Fail(RegisterError.EmailInvalid);

        if (string.IsNullOrWhiteSpace(phone))
            return RegisterResult.Fail(RegisterError.PhoneRequired);

        if (!PhoneValidator.TryNormalize(phone, out var phoneNorm, required: true))
            return RegisterResult.Fail(RegisterError.PhoneInvalid);

        if (string.IsNullOrWhiteSpace(command.Password) || !PasswordPolicy.IsValid(command.Password))
            return RegisterResult.Fail(RegisterError.PasswordWeak);

        if (!Coordinates.TryParsePair(command.Latitude, command.Longitude, out var lat, out var lng))
            return RegisterResult.Fail(RegisterError.LocationRequired);

        if (string.IsNullOrWhiteSpace(city))
            city = command.DefaultCity;

        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            return RegisterResult.Fail(RegisterError.EmailTaken);

        var user = new AppUser
        {
            FullName = fullName,
            Email = email,
            Phone = phoneNorm,
            City = city,
            Latitude = lat,
            Longitude = lng,
            LocationUpdatedAt = DateTime.UtcNow,
            PasswordHash = PasswordHasher.Hash(command.Password),
            Role = UserRole.Client
        };
        MarketCountry.ApplyFromLocation(user);

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        _db.Notifications.Add(new AppNotification
        {
            UserId = user.Id,
            Title = "¡Bienvenido a Chombly!",
            Message = "Tu cuenta fue creada. Agrega tu perro, gato u otra mascota y reserva tu primera cita.",
            Type = "promo"
        });
        await _db.SaveChangesAsync(ct);

        return new RegisterResult(RegisterError.None, user);
    }
}
