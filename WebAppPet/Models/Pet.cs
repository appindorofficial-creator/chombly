using System.ComponentModel.DataAnnotations;

namespace WebAppPet.Models;

public class Pet
{
    public int Id { get; set; }

    public int OwnerId { get; set; }
    public AppUser Owner { get; set; } = null!;

    [Required, MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(40)]
    public string Species { get; set; } = PetSpecies.Dog;

    [MaxLength(80)]
    public string Breed { get; set; } = string.Empty;

    public PetSize Size { get; set; } = PetSize.Medium;

    public int AgeYears { get; set; }

    /// <summary>Optional weight in pounds for clinical context.</summary>
    public decimal? WeightLbs { get; set; }

    [MaxLength(80)]
    public string Temperament { get; set; } = string.Empty;

    [MaxLength(260)]
    public string? PhotoUrl { get; set; }

    public bool IsSenior { get; set; }
    public bool IsAnxious { get; set; }
    public bool HasSpecialNeeds { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
