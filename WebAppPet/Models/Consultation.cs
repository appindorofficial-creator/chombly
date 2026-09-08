using System.ComponentModel.DataAnnotations;

namespace WebAppPet.Models;

public class Consultation
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

    public VetModality Modality { get; set; } = VetModality.Virtual;

    public ConsultationStatus Status { get; set; } = ConsultationStatus.Draft;

    [MaxLength(8)]
    public string PetUsState { get; set; } = "NC";

    [MaxLength(8)]
    public string? ContextCountry { get; set; }

    public IntlMatchMode MatchMode { get; set; } = IntlMatchMode.Best;

    [MaxLength(2)]
    public string? PreferredCountry { get; set; }

    [MaxLength(80)]
    public string? PreferredBreed { get; set; }

    [MaxLength(40)]
    public string? ServiceCatalogCode { get; set; }

    [MaxLength(500)]
    public string? Symptoms { get; set; }

    [MaxLength(1000)]
    public string? SafetyAnswersJson { get; set; }

    public bool HasRedFlags { get; set; }

    public bool HasActiveVcpr { get; set; }

    [MaxLength(260)]
    public string? MediaUrl1 { get; set; }

    [MaxLength(260)]
    public string? MediaUrl2 { get; set; }

    public DateTime? ScheduledAt { get; set; }

    [MaxLength(800)]
    public string? ClinicalNotes { get; set; }

    [MaxLength(200)]
    public string? ResponsibleName { get; set; }

    public decimal PriceCharged { get; set; }

    /// <summary>True when paid via Chombly Care included quick consult.</summary>
    public bool UsesCareBenefit { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
