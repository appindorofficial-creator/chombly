using System.ComponentModel.DataAnnotations;

namespace WebAppPet.Models;

public class Review
{
    public int Id { get; set; }

    public int GroomerId { get; set; }
    public GroomerProfile Groomer { get; set; } = null!;

    public int ClientId { get; set; }
    public AppUser Client { get; set; } = null!;

    public int Rating { get; set; }

    [MaxLength(600)]
    public string Comment { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Favorite
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public int GroomerId { get; set; }
    public GroomerProfile Groomer { get; set; } = null!;
}

public class AppNotification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    [MaxLength(120)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(400)]
    public string Message { get; set; } = string.Empty;

    [MaxLength(40)]
    public string Type { get; set; } = "info";

    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
