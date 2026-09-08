using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebAppPet.Models;

public class GroomerProfile
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public int? CategoryId { get; set; }
    public ServiceCategory? Category { get; set; }

    [Required, MaxLength(120)]
    public string BusinessName { get; set; } = string.Empty;

    public GroomerType Type { get; set; } = GroomerType.Salon;

    public ProviderKind ProviderKind { get; set; } = ProviderKind.Business;
    public WorkMode WorkMode { get; set; } = WorkMode.Local;

    /// <summary>Radio de cobertura en millas (0 = toda la ciudad).</summary>
    public int ServiceAreaMiles { get; set; } = 10;

    [MaxLength(200)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(80)]
    public string City { get; set; } = string.Empty;

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    [MaxLength(800)]
    public string About { get; set; } = string.Empty;

    [MaxLength(260)]
    public string? ImageUrl { get; set; }

    [MaxLength(260)]
    public string? LogoUrl { get; set; }

    [MaxLength(260)]
    public string? CoverUrl { get; set; }

    [MaxLength(30)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? Website { get; set; }

    [Column("AcceptsSeniorDogs")]
    public bool AcceptsSeniorPets { get; set; }

    [Column("AcceptsAnxiousDogs")]
    public bool AcceptsAnxiousPets { get; set; }

    [MaxLength(200)]
    public string AcceptedSpecies { get; set; } = PetSpecies.DefaultAcceptedList;

    public bool IsVerified { get; set; } = true;
    public bool IsFeatured { get; set; }
    public bool IsActive { get; set; } = true;

    public bool VerifiedIdentity { get; set; }
    public bool VerifiedLicense { get; set; }
    public bool VerifiedInsurance { get; set; }
    public bool VerifiedBank { get; set; }

    /// <summary>None for non-vet; LocalVet / InternationalAdvisor / BehaviorSpecialist.</summary>
    public VetProviderKind VetProviderKind { get; set; } = VetProviderKind.None;

    public BehaviorSpecialistRole? BehaviorRole { get; set; }

    /// <summary>Country ISO for international advisors (CO, SV…).</summary>
    [MaxLength(8)]
    public string? LicenseCountry { get; set; }

    [MaxLength(120)]
    public string? SpokenLanguages { get; set; }

    public bool OffersEmergency24x7 { get; set; }

    public DateTime? SupportCallAt { get; set; }

    public ICollection<ProviderLicense> Licenses { get; set; } = new List<ProviderLicense>();

    /// <summary>Draft / PendingReview / Approved / Rejected</summary>
    public BusinessPublishStatus PublishStatus { get; set; } = BusinessPublishStatus.Approved;

    public double Rating { get; set; }
    public int ReviewCount { get; set; }
    public decimal StartingPrice { get; set; }

    /// <summary>Ej: / noche, / sesión, / visita</summary>
    [MaxLength(40)]
    public string? PriceUnit { get; set; }

    public ICollection<GroomerService> Services { get; set; } = new List<GroomerService>();
    public ICollection<BusinessAmenity> Amenities { get; set; } = new List<BusinessAmenity>();
    public ICollection<ServiceExtra> Extras { get; set; } = new List<ServiceExtra>();
    public ICollection<BusinessDayAvailability> DayAvailabilities { get; set; } = new List<BusinessDayAvailability>();
    public ICollection<BusinessWeeklyHour> WeeklyHours { get; set; } = new List<BusinessWeeklyHour>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public ICollection<Favorite> FavoritedBy { get; set; } = new List<Favorite>();

    public bool IsPublished => IsActive && PublishStatus == BusinessPublishStatus.Approved;

    public bool AcceptsSpecies(string? species) => PetSpecies.ListIncludes(AcceptedSpecies, species);
    public IEnumerable<string> AcceptedSpeciesList => PetSpecies.ParseList(AcceptedSpecies);
}
