using System.ComponentModel.DataAnnotations;

namespace WebAppPet.Models;

public class Appointment
{
    public int Id { get; set; }

    public int ClientId { get; set; }
    public AppUser Client { get; set; } = null!;

    public int PetId { get; set; }
    public Pet Pet { get; set; } = null!;

    public int GroomerId { get; set; }
    public GroomerProfile Groomer { get; set; } = null!;

    public int ServiceId { get; set; }
    public GroomerService Service { get; set; } = null!;

    /// <summary>Inicio (cita) o check-in (hotel/daycare).</summary>
    public DateTime ScheduledAt { get; set; }

    /// <summary>Check-out / fin. Null = servicio de una sola sesión.</summary>
    public DateTime? EndAt { get; set; }

    public int Nights { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;

    public decimal TotalPrice { get; set; }
    public decimal DepositPaid { get; set; }

    [MaxLength(40)]
    public string? PromoCode { get; set; }

    public decimal DiscountAmount { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AppointmentExtra> Extras { get; set; } = new List<AppointmentExtra>();
}
