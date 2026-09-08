using System.ComponentModel.DataAnnotations;

namespace WebAppPet.Models;

public class ProviderLicense
{
    public int Id { get; set; }

    public int GroomerId { get; set; }
    public GroomerProfile Groomer { get; set; } = null!;

    /// <summary>US state code (NC) or country ISO (CO, SV).</summary>
    [Required, MaxLength(8)]
    public string Jurisdiction { get; set; } = string.Empty;

    [MaxLength(80)]
    public string LicenseNumber { get; set; } = string.Empty;

    public DateTime? ExpiresAt { get; set; }

    public bool IsVerified { get; set; }

    public bool IsUsState { get; set; } = true;
}
