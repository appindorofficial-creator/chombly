using System.ComponentModel.DataAnnotations;

namespace WebAppPet.Models;

public class ServiceCatalogItem
{
    public int Id { get; set; }

    [Required, MaxLength(40)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string NameEs { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string NameEn { get; set; } = string.Empty;

    [MaxLength(400)]
    public string ScopeEs { get; set; } = string.Empty;

    [MaxLength(400)]
    public string ScopeEn { get; set; } = string.Empty;

    public decimal Price { get; set; }

    [MaxLength(8)]
    public string Currency { get; set; } = "USD";

    public int DurationMinutes { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsBookable { get; set; } = true;

    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
}
