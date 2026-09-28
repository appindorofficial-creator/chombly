using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Payments.Shared;
using WebAppPet.Application.ProfessionalOnboarding.Shared;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.ProfessionalOnboarding.ApproveOnboarding;

/// <summary>
/// Approves a pending application and turns the applicant's business into a verified local vet,
/// international advisor or behavior specialist, with its license, breeds and compensation rule.
/// Notifies the applicant.
/// </summary>
public class ApproveOnboardingHandler
{
    private const int MaxJurisdictionLength = 8;
    private const int MaxBreeds = 20;
    private const int MaxBreedLength = 80;

    private readonly AppDbContext _db;
    private readonly ProviderPayoutService _payouts;
    private readonly VetAuditService _audit;

    public ApproveOnboardingHandler(AppDbContext db, ProviderPayoutService payouts, VetAuditService audit)
    {
        _db = db;
        _payouts = payouts;
        _audit = audit;
    }

    public async Task<ApproveOnboardingResult> HandleAsync(ApproveOnboardingCommand command, CancellationToken ct = default)
    {
        var application = await _db.ProfessionalOnboardingApplications
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == command.ApplicationId, ct);
        if (application is null)
            return new ApproveOnboardingResult(ApproveOnboardingOutcome.NotFound, null);
        if (!application.IsPendingReview())
            return new ApproveOnboardingResult(ApproveOnboardingOutcome.NotPending, application);

        var business = await _db.Groomers
            .Include(g => g.Licenses)
            .FirstOrDefaultAsync(g => g.UserId == application.UserId, ct);
        if (business is null)
            return new ApproveOnboardingResult(ApproveOnboardingOutcome.NoBusinessProfile, application);

        application.Status = ProfessionalOnboardingStatus.Approved;
        application.ReviewedUtc = DateTime.UtcNow;
        application.ReviewerNotes = command.Notes;

        switch (application.Track)
        {
            case ProfessionalOnboardingTrack.Local:
                business.VetProviderKind = VetProviderKind.LocalVet;
                business.VerifiedLicense = true;
                UpsertLicense(business, application.LicenseJurisdiction, application.LicenseNumber, application.LicenseExpiry, isUsState: true);
                break;

            case ProfessionalOnboardingTrack.International:
                business.VetProviderKind = VetProviderKind.InternationalAdvisor;
                business.LicenseCountry = Jurisdiction(application.LicenseJurisdiction);
                business.SpokenLanguages = application.Languages;
                business.VerifiedLicense = true;
                UpsertLicense(business, business.LicenseCountry, application.LicenseNumber, application.LicenseExpiry, isUsState: false);
                await AddBreedExpertiseAsync(business.Id, application.BreedExpertiseCsv, ct);
                break;

            case ProfessionalOnboardingTrack.Behavior:
                business.VetProviderKind = VetProviderKind.BehaviorSpecialist;
                business.BehaviorRole ??= BehaviorSpecialistRole.Consultant;
                if (!string.IsNullOrWhiteSpace(application.Languages))
                    business.SpokenLanguages = application.Languages;
                business.VerifiedLicense = true;
                break;
        }

        if (!string.IsNullOrWhiteSpace(application.ClinicOrPracticeName))
            business.BusinessName = application.ClinicOrPracticeName.Trim();

        _db.Notifications.Add(new AppNotification
        {
            UserId = application.UserId,
            Title = "Onboarding profesional aprobado",
            Message = "Tu solicitud profesional fue aprobada. Ya puedes atender en Chombly.",
            Type = "professional",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        var compensation = application.Track switch
        {
            ProfessionalOnboardingTrack.International => CompensationServiceType.InternationalVet,
            ProfessionalOnboardingTrack.Behavior => CompensationServiceType.Behavior,
            _ => CompensationServiceType.LocalVet
        };
        await _payouts.EnsureProviderRuleAsync(application.UserId, compensation, ct);

        await _audit.LogAsync("onboarding_approved", command.ReviewerUserId, "ProfessionalOnboarding", application.Id,
            new { application.UserId, application.Track }, ct);

        return new ApproveOnboardingResult(ApproveOnboardingOutcome.Approved, application);
    }

    private static string Jurisdiction(string value)
    {
        var jurisdiction = value.Trim().ToUpperInvariant();
        return jurisdiction.Length > MaxJurisdictionLength ? jurisdiction[..MaxJurisdictionLength] : jurisdiction;
    }

    private static void UpsertLicense(GroomerProfile business, string jurisdiction, string licenseNumber, DateTime? expiry, bool isUsState)
    {
        var code = Jurisdiction(jurisdiction);
        var license = business.Licenses.FirstOrDefault(l => string.Equals(l.Jurisdiction, code, StringComparison.OrdinalIgnoreCase));
        if (license is null)
        {
            license = new ProviderLicense { GroomerId = business.Id, Jurisdiction = code };
            business.Licenses.Add(license);
        }

        license.LicenseNumber = licenseNumber;
        license.ExpiresAt = expiry;
        license.IsVerified = true;
        license.IsUsState = isUsState;
    }

    private async Task AddBreedExpertiseAsync(int businessId, string? csv, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return;

        var known = (await _db.ProviderBreedExpertises
                .Where(e => e.GroomerId == businessId)
                .Select(e => e.Breed)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var breeds = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(b => b.Length > MaxBreedLength ? b[..MaxBreedLength] : b)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxBreeds);
        foreach (var breed in breeds.Where(known.Add))
        {
            _db.ProviderBreedExpertises.Add(new ProviderBreedExpertise
            {
                GroomerId = businessId,
                Breed = breed,
                EvidenceNote = "From onboarding",
                IsVerified = true,
                VerifiedAt = DateTime.UtcNow
            });
        }
    }
}
