using System.ComponentModel.DataAnnotations;

namespace WebAppPet.Models;

public enum CountryMarketStatus
{
    ComingSoon = 0,
    Available = 1,
    Paused = 2
}

public class CountryCatalogEntry
{
    public int Id { get; set; }

    [Required, MaxLength(2)]
    public string Iso2 { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string NameEs { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string NameEn { get; set; } = string.Empty;

    [MaxLength(40)]
    public string? PrimaryLanguage { get; set; }

    public CountryMarketStatus Status { get; set; } = CountryMarketStatus.ComingSoon;

    public DateTime? OpenedAt { get; set; }
}

public class CountryWaitlistEntry
{
    public int Id { get; set; }

    [Required, MaxLength(2)]
    public string Iso2 { get; set; } = string.Empty;

    public int? UserId { get; set; }

    [MaxLength(150)]
    public string? Email { get; set; }

    [MaxLength(10)]
    public string PreferredLanguage { get; set; } = "es";

    public bool ConsentMarketing { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ProviderBreedExpertise
{
    public int Id { get; set; }

    public int GroomerId { get; set; }
    public GroomerProfile Groomer { get; set; } = null!;

    [Required, MaxLength(80)]
    public string Breed { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? EvidenceNote { get; set; }

    public bool IsVerified { get; set; }

    public DateTime? VerifiedAt { get; set; }
}

public enum IntlMatchMode
{
    Best = 0,
    Country = 1,
    Breed = 2
}
