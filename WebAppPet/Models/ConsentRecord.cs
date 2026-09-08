using System.ComponentModel.DataAnnotations;

namespace WebAppPet.Models;

public class ConsentRecord
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public int? ConsultationId { get; set; }
    public Consultation? Consultation { get; set; }

    [Required, MaxLength(80)]
    public string DocumentKey { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string DocumentVersion { get; set; } = "1.0";

    public bool Accepted { get; set; }

    public DateTime AcceptedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(64)]
    public string? IpAddress { get; set; }

    [MaxLength(260)]
    public string? UserAgent { get; set; }
}
