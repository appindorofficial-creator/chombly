using System.ComponentModel.DataAnnotations;

namespace WebAppPet.Models;

/// <summary>Extra opcional al reservar (baño adicional, medicamentos, etc.).</summary>
public class ServiceExtra
{
    public int Id { get; set; }

    public int GroomerId { get; set; }
    public GroomerProfile Groomer { get; set; } = null!;

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Description { get; set; }

    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
}
