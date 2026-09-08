using System.ComponentModel.DataAnnotations;

namespace WebAppPet.Models;

public class VcprRecord
{
    public int Id { get; set; }

    public int PetId { get; set; }
    public Pet Pet { get; set; } = null!;

    public int ProviderId { get; set; }
    public GroomerProfile Provider { get; set; } = null!;

    [Required, MaxLength(8)]
    public string UsState { get; set; } = "NC";

    public DateTime ExamDate { get; set; }

    [MaxLength(400)]
    public string? EvidenceNote { get; set; }

    [MaxLength(260)]
    public string? EvidenceUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
