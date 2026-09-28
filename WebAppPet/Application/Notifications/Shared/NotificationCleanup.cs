using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;

namespace WebAppPet.Application.Notifications.Shared;

public static class NotificationCleanup
{
    /// <summary>
    /// Unlinks the reminder deliveries that produced these notifications so they can be deleted;
    /// the delivery history is kept. Changes are saved with the caller's next SaveChanges.
    /// </summary>
    public static async Task DetachDeliveriesAsync(this AppDbContext db, IReadOnlyCollection<int> notificationIds, CancellationToken ct = default)
    {
        if (notificationIds.Count == 0)
            return;

        var deliveries = await db.ReminderDeliveries
            .Where(d => d.AppNotificationId != null && notificationIds.Contains(d.AppNotificationId.Value))
            .ToListAsync(ct);
        foreach (var delivery in deliveries)
            delivery.AppNotificationId = null;
    }
}
