using System.ComponentModel.DataAnnotations;

namespace WebAppPet.Models;

public class BusinessAmenity
{
    public int Id { get; set; }

    public int GroomerId { get; set; }
    public GroomerProfile Groomer { get; set; } = null!;

    [Required, MaxLength(80)]
    public string Label { get; set; } = string.Empty;

    [MaxLength(40)]
    public string Icon { get; set; } = "✓";

    public int SortOrder { get; set; }
}
