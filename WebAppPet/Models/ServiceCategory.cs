using System.ComponentModel.DataAnnotations;

namespace WebAppPet.Models;

/// <summary>Categoría de servicio del marketplace (Grooming, Hotel, Vet…).</summary>
public class ServiceCategory
{
    public int Id { get; set; }

    [Required, MaxLength(40)]
    public string Slug { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(80)]
    public string NameEn { get; set; } = string.Empty;

    [MaxLength(160)]
    public string Subtitle { get; set; } = string.Empty;

    [MaxLength(160)]
    public string SubtitleEn { get; set; } = string.Empty;

    [MaxLength(120)]
    public string Icon { get; set; } = "paw";

    [MaxLength(40)]
    public string Emoji { get; set; } = "🐾";

    /// <summary>Si true, la reserva usa check-in / check-out (noches).</summary>
    public bool IsOvernight { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<GroomerProfile> Businesses { get; set; } = new List<GroomerProfile>();
}
