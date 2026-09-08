using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;

    public RegisterModel(AppDbContext db, AuthService auth)
    {
        _db = db;
        _auth = auth;
    }

    [BindProperty, Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [BindProperty, Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string? Phone { get; set; }

    [BindProperty]
    public string City { get; set; } = string.Empty;

    [BindProperty, Required, MinLength(6)]
    public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (await _db.Users.AnyAsync(u => u.Email == Email))
        {
            ErrorMessage = "Ese email ya está registrado.";
            return Page();
        }

        if (!PhoneValidator.TryNormalize(Phone, out var phoneNorm))
        {
            ErrorMessage = "Teléfono inválido. Solo números (mín. 7 dígitos). Ej: 7045551234";
            return Page();
        }

        var user = new AppUser
        {
            FullName = FullName,
            Email = Email,
            Phone = phoneNorm,
            City = City,
            PasswordHash = PasswordHasher.Hash(Password),
            Role = UserRole.Client
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _db.Notifications.Add(new AppNotification
        {
            UserId = user.Id,
            Title = "¡Bienvenido a Chombly!",
            Message = "Tu cuenta fue creada. Agrega tu perro, gato u otra mascota y reserva tu primera cita.",
            Type = "promo"
        });
        await _db.SaveChangesAsync();

        await _auth.SignInAsync(user);
        return RedirectToPage("/Index");
    }
}
