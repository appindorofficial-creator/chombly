using System.ComponentModel.DataAnnotations;

namespace WebAppPet.Models;

public enum ProfessionalOnboardingTrack
{
    Local = 0,
    International = 1,
    Behavior = 2
}

public enum ProfessionalOnboardingStatus
{
    Draft = 0,
    Submitted = 1,
    UnderReview = 2,
    Approved = 3,
    Rejected = 4
}

public class ProfessionalOnboardingApplication
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public ProfessionalOnboardingTrack Track { get; set; } = ProfessionalOnboardingTrack.Local;

    public ProfessionalOnboardingStatus Status { get; set; } = ProfessionalOnboardingStatus.Draft;

    [MaxLength(160)]
    public string LegalName { get; set; } = string.Empty;

    [MaxLength(160)]
    public string ClinicOrPracticeName { get; set; } = string.Empty;

    [MaxLength(80)]
    public string LicenseNumber { get; set; } = string.Empty;

    /// <summary>US state for Local; country ISO for International.</summary>
    [MaxLength(40)]
    public string LicenseJurisdiction { get; set; } = string.Empty;

    public DateTime? LicenseExpiry { get; set; }

    [MaxLength(200)]
    public string Languages { get; set; } = string.Empty;

    [MaxLength(300)]
    public string Specialties { get; set; } = string.Empty;

    [MaxLength(400)]
    public string BreedExpertiseCsv { get; set; } = string.Empty;

    public bool AcceptsInternationalClients { get; set; }

    public bool HasPhysicalClinic { get; set; }

    public bool VcprCapable { get; set; }

    [MaxLength(400)]
    public string? DocumentsNote { get; set; }

    [MaxLength(260)]
    public string? UploadPath { get; set; }

    public DateTime? SubmittedUtc { get; set; }

    public DateTime? ReviewedUtc { get; set; }

    [MaxLength(600)]
    public string? ReviewerNotes { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
