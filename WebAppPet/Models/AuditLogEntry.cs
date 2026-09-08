using System.ComponentModel.DataAnnotations;

namespace WebAppPet.Models;

public class AuditLogEntry
{
    public int Id { get; set; }

    public int? ActorUserId { get; set; }

    [Required, MaxLength(80)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(80)]
    public string? EntityType { get; set; }

    public int? EntityId { get; set; }

    [MaxLength(2000)]
    public string? PayloadJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
