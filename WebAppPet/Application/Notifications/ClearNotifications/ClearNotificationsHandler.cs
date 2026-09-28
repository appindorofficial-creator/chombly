using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Notifications.Shared;
using WebAppPet.Data;

namespace WebAppPet.Application.Notifications.ClearNotifications;

/// <summary>Deletes all of the user's notifications and returns how many there were.</summary>
public class ClearNotificationsHandler
{
    private readonly AppDbContext _db;

    public ClearNotificationsHandler(AppDbContext db) => _db = db;

    public async Task<int> HandleAsync(ClearNotificationsCommand command, CancellationToken ct = default)
    {
        var notifications = await _db.Notifications.Where(n => n.UserId == command.UserId).ToListAsync(ct);
        if (notifications.Count == 0)
            return 0;

        await _db.DetachDeliveriesAsync(notifications.Select(n => n.Id).ToList(), ct);
        _db.Notifications.RemoveRange(notifications);
        await _db.SaveChangesAsync(ct);
        return notifications.Count;
    }
}
