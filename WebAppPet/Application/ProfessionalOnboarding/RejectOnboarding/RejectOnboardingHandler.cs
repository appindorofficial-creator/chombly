using Microsoft.EntityFrameworkCore;
using WebAppPet.Domain;
using WebAppPet.Infrastructure.Persistence;
using WebAppPet.Services;

namespace WebAppPet.Application.ProfessionalOnboarding.RejectOnboarding;

/// <summary>
/// Rejects the application with the reviewer's notes and tells the applicant, who can correct
/// it and apply again. Returns the application, or null when it does not exist.
/// </summary>
public class RejectOnboardingHandler
{
    private readonly AppDbContext _db;
    private readonly VetAuditService _audit;

    public RejectOnboardingHandler(AppDbContext db, VetAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<ProfessionalOnboardingApplication?> HandleAsync(RejectOnboardingCommand command, CancellationToken ct = default)
    {
        var application = await _db.ProfessionalOnboardingApplications
            .FirstOrDefaultAsync(a => a.Id == command.ApplicationId, ct);
        if (application is null)
            return null;

        application.Status = ProfessionalOnboardingStatus.Rejected;
        application.ReviewedUtc = DateTime.UtcNow;
        application.ReviewerNotes = command.Notes;
        _db.Notifications.Add(new AppNotification
        {
            UserId = application.UserId,
            Title = "Onboarding profesional no aprobado",
            Message = string.IsNullOrWhiteSpace(command.Notes)
                ? "Tu solicitud necesita correcciones. Revisa las notas del revisor."
                : command.Notes,
            Type = "professional",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("onboarding_rejected", command.ReviewerUserId, "ProfessionalOnboarding", application.Id,
            new { notes = command.Notes }, ct);

        return application;
    }
}
