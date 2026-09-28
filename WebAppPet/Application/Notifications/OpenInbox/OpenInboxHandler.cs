using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;

namespace WebAppPet.Application.Notifications.OpenInbox;

/// <summary>
/// The user's notifications, newest first, as they were before opening: unread ones still come back
/// unread so the page can highlight them, but they are marked read in the database.
/// </summary>
public class OpenInboxHandler
{
    private readonly AppDbContext _db;

    public OpenInboxHandler(AppDbContext db) => _db = db;

    public async Task<List<AppNotification>> HandleAsync(OpenInboxCommand command, CancellationToken ct = default)
    {
        var items = await _db.Notifications.AsNoTracking()
            .Where(n => n.UserId == command.UserId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(ct);

        if (items.Any(n => !n.IsRead))
        {
            await _db.Notifications
                .Where(n => n.UserId == command.UserId && !n.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
        }

        return items;
    }
}
