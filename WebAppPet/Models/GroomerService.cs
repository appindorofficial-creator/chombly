using System.ComponentModel.DataAnnotations;

namespace WebAppPet.Models;

public class GroomerService
{
    public int Id { get; set; }

    public int GroomerId { get; set; }
    public GroomerProfile Groomer { get; set; } = null!;

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(300)]
    public string Description { get; set; } = string.Empty;

    public decimal PriceSmall { get; set; }
    public decimal PriceMedium { get; set; }
    public decimal PriceLarge { get; set; }
    public decimal PriceGiant { get; set; }

    public int DurationMinutes { get; set; } = 60;

    [MaxLength(40)]
    public string Icon { get; set; } = "bath";

    /// <summary>noche | sesion | visita | dia</summary>
    [MaxLength(20)]
    public string BillingUnit { get; set; } = "sesion";

    public decimal PriceFor(PetSize size) => size switch
    {
        PetSize.Small => PriceSmall,
        PetSize.Medium => PriceMedium,
        PetSize.Large => PriceLarge,
        PetSize.Giant => PriceGiant,
        _ => PriceMedium
    };
}
