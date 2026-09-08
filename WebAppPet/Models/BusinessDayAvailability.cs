using System.ComponentModel.DataAnnotations;

namespace WebAppPet.Models;

/// <summary>Disponibilidad del negocio por día (configurada o generada).</summary>
public class BusinessDayAvailability
{
    public int Id { get; set; }

    public int GroomerId { get; set; }
    public GroomerProfile Groomer { get; set; } = null!;

    /// <summary>Solo fecha (sin hora).</summary>
    public DateTime Day { get; set; }

    public bool IsAvailable { get; set; } = true;

    [MaxLength(120)]
    public string? Note { get; set; }
}
