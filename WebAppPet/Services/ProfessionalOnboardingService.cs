using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;

namespace WebAppPet.Services;

public class ProfessionalOnboardingService
{
    private readonly AppDbContext _db;
    private readonly VetAuditService _audit;
    private readonly ProviderPayoutService _payouts;

    public ProfessionalOnboardingService(AppDbContext db, VetAuditService audit, ProviderPayoutService payouts)
    {
        _db = db;
        _audit = audit;
        _payouts = payouts;
    }

    public Task<ProfessionalOnboardingApplication?> GetLatestAsync(int userId, CancellationToken ct = default) =>
        _db.ProfessionalOnboardingApplications
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedUtc)
            .FirstOrDefaultAsync(ct);

    public Task<ProfessionalOnboardingApplication?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _db.ProfessionalOnboardingApplications
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<ProfessionalOnboardingApplication> StartOrUpdateDraftAsync(
        int userId,
        ProfessionalOnboardingTrack track,
        Action<ProfessionalOnboardingApplication> apply,
        CancellationToken ct = default)
    {
        var app = await _db.ProfessionalOnboardingApplications
            .Where(a => a.UserId == userId &&
                        (a.Status == ProfessionalOnboardingStatus.Draft ||
                         a.Status == ProfessionalOnboardingStatus.Rejected))
            .OrderByDescending(a => a.CreatedUtc)
            .FirstOrDefaultAsync(ct);

        if (app is null || app.Status == ProfessionalOnboardingStatus.Rejected)
        {
            app = new ProfessionalOnboardingApplication
            {
                UserId = userId,
                Track = track,
                Status = ProfessionalOnboardingStatus.Draft,
                CreatedUtc = DateTime.UtcNow
            };
            _db.ProfessionalOnboardingApplications.Add(app);
        }
        else
        {
            app.Track = track;
        }

        apply(app);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("onboarding_draft_saved", userId, "ProfessionalOnboarding", app.Id,
            new { app.Track, app.Status }, ct);

        return app;
    }

    public async Task<ProfessionalOnboardingApplication?> SubmitAsync(int userId, int applicationId, CancellationToken ct = default)
    {
        var app = await _db.ProfessionalOnboardingApplications
            .FirstOrDefaultAsync(a => a.Id == applicationId && a.UserId == userId, ct);
        if (app is null) return null;
        if (app.Status is not (ProfessionalOnboardingStatus.Draft or ProfessionalOnboardingStatus.Rejected))
            return app;

        if (string.IsNullOrWhiteSpace(app.LegalName) || string.IsNullOrWhiteSpace(app.LicenseJurisdiction))
            throw new InvalidOperationException("Legal name and license jurisdiction are required.");

        app.Status = ProfessionalOnboardingStatus.Submitted;
        app.SubmittedUtc = DateTime.UtcNow;
        app.ReviewedUtc = null;
        app.ReviewerNotes = null;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("onboarding_submitted", userId, "ProfessionalOnboarding", app.Id,
            new { app.Track }, ct);

        return app;
    }

    public async Task<ProfessionalOnboardingApplication?> ApproveAsync(
        int applicationId,
        int reviewerUserId,
        string? notes = null,
        CancellationToken ct = default)
    {
        var app = await _db.ProfessionalOnboardingApplications
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == applicationId, ct);
        if (app is null) return null;
        if (app.Status is not (ProfessionalOnboardingStatus.Submitted or ProfessionalOnboardingStatus.UnderReview))
            return app;

        var groomer = await _db.Groomers
            .Include(g => g.Licenses)
            .FirstOrDefaultAsync(g => g.UserId == app.UserId, ct);

        if (groomer is null)
            throw new InvalidOperationException("Applicant has no business profile. Register as business first.");

        app.Status = ProfessionalOnboardingStatus.Approved;
        app.ReviewedUtc = DateTime.UtcNow;
        app.ReviewerNotes = notes;

        switch (app.Track)
        {
            case ProfessionalOnboardingTrack.Local:
                groomer.VetProviderKind = VetProviderKind.LocalVet;
                groomer.VerifiedLicense = true;
                EnsureLicense(groomer, app.LicenseJurisdiction, app.LicenseNumber, app.LicenseExpiry, isUsState: true);
                break;

            case ProfessionalOnboardingTrack.International:
                groomer.VetProviderKind = VetProviderKind.InternationalAdvisor;
                groomer.LicenseCountry = app.LicenseJurisdiction.Trim().ToUpperInvariant();
                if (groomer.LicenseCountry.Length > 8)
                    groomer.LicenseCountry = groomer.LicenseCountry[..8];
                groomer.SpokenLanguages = app.Languages;
                groomer.VerifiedLicense = true;
                EnsureLicense(groomer, groomer.LicenseCountry!, app.LicenseNumber, app.LicenseExpiry, isUsState: false);
                await SeedBreedExpertiseAsync(groomer.Id, app.BreedExpertiseCsv, ct);
                break;

            case ProfessionalOnboardingTrack.Behavior:
                groomer.VetProviderKind = VetProviderKind.BehaviorSpecialist;
                groomer.BehaviorRole ??= BehaviorSpecialistRole.Consultant;
                groomer.SpokenLanguages = string.IsNullOrWhiteSpace(app.Languages) ? groomer.SpokenLanguages : app.Languages;
                groomer.VerifiedLicense = true;
                break;
        }

        if (!string.IsNullOrWhiteSpace(app.ClinicOrPracticeName))
            groomer.BusinessName = app.ClinicOrPracticeName.Trim();

        _db.Notifications.Add(new AppNotification
        {
            UserId = app.UserId,
            Title = "Onboarding profesional aprobado",
            Message = "Tu solicitud profesional fue aprobada. Ya puedes atender en Chombly.",
            Type = "professional",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);

        var compType = app.Track switch
        {
            ProfessionalOnboardingTrack.International => CompensationServiceType.InternationalVet,
            ProfessionalOnboardingTrack.Behavior => CompensationServiceType.Behavior,
            _ => CompensationServiceType.LocalVet
        };
        await _payouts.EnsureProviderRuleAsync(app.UserId, compType, ct);

        await _audit.LogAsync("onboarding_approved", reviewerUserId, "ProfessionalOnboarding", app.Id,
            new { app.UserId, app.Track }, ct);

        return app;
    }

    public async Task<ProfessionalOnboardingApplication?> RejectAsync(
        int applicationId,
        int reviewerUserId,
        string notes,
        CancellationToken ct = default)
    {
        var app = await _db.ProfessionalOnboardingApplications
            .FirstOrDefaultAsync(a => a.Id == applicationId, ct);
        if (app is null) return null;

        app.Status = ProfessionalOnboardingStatus.Rejected;
        app.ReviewedUtc = DateTime.UtcNow;
        app.ReviewerNotes = notes;
        await _db.SaveChangesAsync(ct);

        _db.Notifications.Add(new AppNotification
        {
            UserId = app.UserId,
            Title = "Onboarding profesional no aprobado",
            Message = string.IsNullOrWhiteSpace(notes)
                ? "Tu solicitud necesita correcciones. Revisa las notas del revisor."
                : notes,
            Type = "professional",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("onboarding_rejected", reviewerUserId, "ProfessionalOnboarding", app.Id,
            new { notes }, ct);

        return app;
    }

    public Task<List<ProfessionalOnboardingApplication>> ListPendingAsync(CancellationToken ct = default) =>
        _db.ProfessionalOnboardingApplications.AsNoTracking()
            .Include(a => a.User)
            .Where(a => a.Status == ProfessionalOnboardingStatus.Submitted ||
                        a.Status == ProfessionalOnboardingStatus.UnderReview)
            .OrderBy(a => a.SubmittedUtc)
            .ToListAsync(ct);

    private static void EnsureLicense(
        GroomerProfile groomer,
        string jurisdiction,
        string licenseNumber,
        DateTime? expiry,
        bool isUsState)
    {
        var j = jurisdiction.Trim().ToUpperInvariant();
        if (j.Length > 8) j = j[..8];

        var existing = groomer.Licenses.FirstOrDefault(l =>
            string.Equals(l.Jurisdiction, j, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.LicenseNumber = licenseNumber;
            existing.ExpiresAt = expiry;
            existing.IsVerified = true;
            existing.IsUsState = isUsState;
            return;
        }

        groomer.Licenses.Add(new ProviderLicense
        {
            GroomerId = groomer.Id,
            Jurisdiction = j,
            LicenseNumber = licenseNumber,
            ExpiresAt = expiry,
            IsVerified = true,
            IsUsState = isUsState
        });
    }

    private async Task SeedBreedExpertiseAsync(int groomerId, string? csv, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(csv)) return;

        var breeds = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(b => b.Length > 80 ? b[..80] : b)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();

        var existing = await _db.ProviderBreedExpertises
            .Where(e => e.GroomerId == groomerId)
            .Select(e => e.Breed)
            .ToListAsync(ct);
        var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        foreach (var breed in breeds)
        {
            if (existingSet.Contains(breed)) continue;
            existingSet.Add(breed);
            _db.ProviderBreedExpertises.Add(new ProviderBreedExpertise
            {
                GroomerId = groomerId,
                Breed = breed,
                EvidenceNote = "From onboarding",
                IsVerified = true,
                VerifiedAt = DateTime.UtcNow
            });
        }
    }
}
