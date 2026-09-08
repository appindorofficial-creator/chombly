using System.ComponentModel.DataAnnotations;

namespace WebAppPet.Models;

public class AppUser
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Phone { get; set; }

    [MaxLength(120)]
    public string City { get; set; } = string.Empty;

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public DateTime? LocationUpdatedAt { get; set; }

    public UserRole Role { get; set; } = UserRole.Client;

    /// <summary>Preferencia de idioma: es | en.</summary>
    [MaxLength(10)]
    public string PreferredLanguage { get; set; } = "es";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Pet> Pets { get; set; } = new List<Pet>();
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public ICollection<AppNotification> Notifications { get; set; } = new List<AppNotification>();
    public GroomerProfile? GroomerProfile { get; set; }
}
