using Microsoft.EntityFrameworkCore;
using WebAppPet.Domain;
using WebAppPet.Infrastructure.Persistence;

namespace WebAppPet.Application.Businesses.ResubmitBusiness;

/// <summary>
/// A rejected or draft business asks again to be published: it goes back to review, hidden, and
/// every admin is notified. False when the business is not rejected or draft.
/// </summary>
public class ResubmitBusinessHandler
{
    private readonly AppDbContext _db;

    public ResubmitBusinessHandler(AppDbContext db) => _db = db;

    public async Task<bool> HandleAsync(ResubmitBusinessCommand command, CancellationToken ct = default)
    {
        var business = await _db.Groomers.FirstOrDefaultAsync(g => g.Id == command.BusinessId, ct);
        if (business is null || business.PublishStatus is not (BusinessPublishStatus.Rejected or BusinessPublishStatus.Draft))
            return false;

        business.PublishStatus = BusinessPublishStatus.PendingReview;
        business.IsActive = false;
        business.IsVerified = false;

        var adminIds = await _db.Users.Where(u => u.Role == UserRole.Admin).Select(u => u.Id).ToListAsync(ct);
        foreach (var adminId in adminIds)
        {
            _db.Notifications.Add(new AppNotification
            {
                UserId = adminId,
                Title = "Negocio reenviado a revisión",
                Message = $"{business.BusinessName} vuelve a solicitar publicación.",
                Type = "business"
            });
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }
}
