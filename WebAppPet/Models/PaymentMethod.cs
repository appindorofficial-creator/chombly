using System.ComponentModel.DataAnnotations;

namespace WebAppPet.Models;

public class PaymentMethod
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    [MaxLength(20)]
    public string Brand { get; set; } = "Visa";

    [MaxLength(4)]
    public string Last4 { get; set; } = string.Empty;

    [MaxLength(100)]
    public string HolderName { get; set; } = string.Empty;

    public int ExpMonth { get; set; }
    public int ExpYear { get; set; }

    public bool IsDefault { get; set; }
}

public class GroomerPhoto
{
    public int Id { get; set; }

    public int GroomerId { get; set; }
    public GroomerProfile Groomer { get; set; } = null!;

    [MaxLength(260)]
    public string BeforeUrl { get; set; } = string.Empty;

    [MaxLength(260)]
    public string AfterUrl { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? Caption { get; set; }
}
