using System.ComponentModel.DataAnnotations;

namespace WebAppPet.Models;

public class BehaviorCase
{
    public int Id { get; set; }

    public int ClientId { get; set; }
    public AppUser Client { get; set; } = null!;

    public int? PetId { get; set; }
    public Pet? Pet { get; set; }

    public int? ProviderId { get; set; }
    public GroomerProfile? Provider { get; set; }

    public int? AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }

    public BehaviorCaseStatus Status { get; set; } = BehaviorCaseStatus.Draft;

    [MaxLength(120)]
    public string? ProblemType { get; set; }

    [MaxLength(40)]
    public string? Frequency { get; set; }

    [MaxLength(800)]
    public string? ContextNotes { get; set; }

    [MaxLength(260)]
    public string? VideoUrl { get; set; }

    public bool ClinicalRedFlag { get; set; }

    public DateTime? ScheduledAt { get; set; }

    public decimal PriceCharged { get; set; }

    [MaxLength(400)]
    public string? Goals { get; set; }

    [MaxLength(800)]
    public string? Exercises { get; set; }

    [MaxLength(400)]
    public string? FollowUpNotes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
