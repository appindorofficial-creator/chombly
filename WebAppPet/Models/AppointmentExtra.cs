using System.ComponentModel.DataAnnotations;

namespace WebAppPet.Models;

public class AppointmentExtra
{
    public int Id { get; set; }

    public int AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = null!;

    public int ServiceExtraId { get; set; }
    public ServiceExtra ServiceExtra { get; set; } = null!;

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }
}
