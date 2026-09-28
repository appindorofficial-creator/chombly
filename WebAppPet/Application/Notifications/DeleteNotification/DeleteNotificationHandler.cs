using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Notifications.Shared;
using WebAppPet.Data;

namespace WebAppPet.Application.Notifications.DeleteNotification;

/// <summary>Deletes one of the user's notifications. False when it is not theirs or no longer exists.</summary>
public class DeleteNotificationHandler
{
    private readonly AppDbContext _db;

    public DeleteNotificationHandler(AppDbContext db) => _db = db;

    public async Task<bool> HandleAsync(DeleteNotificationCommand command, CancellationToken ct = default)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == command.NotificationId && n.UserId == command.UserId, ct);
        if (notification is null)
            return false;

        await _db.DetachDeliveriesAsync([notification.Id], ct);
        _db.Notifications.Remove(notification);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
