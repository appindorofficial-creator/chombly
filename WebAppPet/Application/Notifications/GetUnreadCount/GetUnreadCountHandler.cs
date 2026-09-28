using Microsoft.EntityFrameworkCore;
using WebAppPet.Infrastructure.Persistence;

namespace WebAppPet.Application.Notifications.GetUnreadCount;

/// <summary>How many notifications the user has not seen yet, for the bell badge.</summary>
public class GetUnreadCountHandler
{
    private readonly AppDbContext _db;

    public GetUnreadCountHandler(AppDbContext db) => _db = db;

    public Task<int> HandleAsync(GetUnreadCountQuery query, CancellationToken ct = default) =>
        _db.Notifications.CountAsync(n => n.UserId == query.UserId && !n.IsRead, ct);
}
