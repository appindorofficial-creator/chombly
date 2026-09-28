using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Common;
using WebAppPet.Application.ProfessionalOnboarding.Shared;
using WebAppPet.Domain;
using WebAppPet.Infrastructure.Persistence;

namespace WebAppPet.Application.ProfessionalOnboarding.SaveOnboardingApplication;

/// <summary>
/// Saves the business's draft application and optionally submits it for review. A rejected
/// application is kept as history and a new draft is started.
/// </summary>
public class SaveOnboardingApplicationHandler
{
    private readonly AppDbContext _db;
    private readonly CountryCatalogService _countries;
    private readonly VetAuditService _audit;

    public SaveOnboardingApplicationHandler(AppDbContext db, CountryCatalogService countries, VetAuditService audit)
    {
        _db = db;
        _countries = countries;
        _audit = audit;
    }

    public async Task<SaveOnboardingApplicationResult> HandleAsync(SaveOnboardingApplicationCommand command, CancellationToken ct = default)
    {
        if (!await _db.HasBusinessProfileAsync(command.UserId, ct))
            return new SaveOnboardingApplicationResult(SaveOnboardingApplicationOutcome.NoBusinessProfile, null);

        var details = command.Details;
        var international = command.Track == ProfessionalOnboardingTrack.International;
        string jurisdiction;
        if (international)
        {
            var iso = await _countries.ResolveIsoAsync(
                !string.IsNullOrWhiteSpace(details.LicenseJurisdiction) ? details.LicenseJurisdiction : command.CountrySearch, ct);
            if (string.IsNullOrWhiteSpace(iso))
                return new SaveOnboardingApplicationResult(SaveOnboardingApplicationOutcome.CountryNotFound, details.LicenseJurisdiction);
            jurisdiction = iso;
        }
        else
        {
            jurisdiction = (details.LicenseJurisdiction ?? "").Trim().ToUpperInvariant();
        }

        var uploadPath = command.Document is null ? null : await command.Document.SaveAsync(ct);

        var application = await EditableDraftAsync(command.UserId, command.Track, ct);
        application.LegalName = (details.LegalName ?? "").Trim();
        application.ClinicOrPracticeName = (details.ClinicOrPracticeName ?? "").Trim();
        application.LicenseNumber = (details.LicenseNumber ?? "").Trim();
        application.LicenseJurisdiction = jurisdiction;
        application.LicenseExpiry = details.LicenseExpiry;
        application.Languages = (details.Languages ?? "").Trim();
        application.Specialties = (details.Specialties ?? "").Trim();
        application.BreedExpertiseCsv = international ? (details.BreedExpertiseCsv ?? "").Trim() : "";
        application.AcceptsInternationalClients = international && details.AcceptsInternationalClients;
        application.HasPhysicalClinic = !international && details.HasPhysicalClinic;
        application.VcprCapable = !international && details.VcprCapable;
        application.DocumentsNote = string.IsNullOrWhiteSpace(details.DocumentsNote) ? null : details.DocumentsNote.Trim();
        if (uploadPath is not null)
            application.UploadPath = uploadPath;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("onboarding_draft_saved", command.UserId, "ProfessionalOnboarding", application.Id,
            new { application.Track, application.Status }, ct);

        if (!command.Submit)
            return new SaveOnboardingApplicationResult(SaveOnboardingApplicationOutcome.Saved, jurisdiction);

        if (string.IsNullOrWhiteSpace(application.LegalName) || string.IsNullOrWhiteSpace(application.LicenseJurisdiction))
            return new SaveOnboardingApplicationResult(SaveOnboardingApplicationOutcome.MissingRequired, jurisdiction);

        application.Status = ProfessionalOnboardingStatus.Submitted;
        application.SubmittedUtc = DateTime.UtcNow;
        application.ReviewedUtc = null;
        application.ReviewerNotes = null;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("onboarding_submitted", command.UserId, "ProfessionalOnboarding", application.Id,
            new { application.Track }, ct);

        return new SaveOnboardingApplicationResult(SaveOnboardingApplicationOutcome.Submitted, jurisdiction);
    }

    private async Task<ProfessionalOnboardingApplication> EditableDraftAsync(int userId, ProfessionalOnboardingTrack track, CancellationToken ct)
    {
        var application = await _db.ProfessionalOnboardingApplications
            .Where(a => a.UserId == userId
                        && (a.Status == ProfessionalOnboardingStatus.Draft || a.Status == ProfessionalOnboardingStatus.Rejected))
            .OrderByDescending(a => a.CreatedUtc)
            .FirstOrDefaultAsync(ct);

        if (application is { Status: ProfessionalOnboardingStatus.Draft })
        {
            application.Track = track;
            return application;
        }

        application = new ProfessionalOnboardingApplication
        {
            UserId = userId,
            Track = track,
            Status = ProfessionalOnboardingStatus.Draft,
            CreatedUtc = DateTime.UtcNow
        };
        _db.ProfessionalOnboardingApplications.Add(application);
        return application;
    }
}
