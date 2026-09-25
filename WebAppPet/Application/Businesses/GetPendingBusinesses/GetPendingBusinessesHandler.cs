using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;

namespace WebAppPet.Application.Businesses.GetPendingBusinesses;

/// <summary>Businesses waiting for admin review, newest first.</summary>
public class GetPendingBusinessesHandler
{
    private readonly AppDbContext _db;

    public GetPendingBusinessesHandler(AppDbContext db) => _db = db;

    public Task<List<GroomerProfile>> HandleAsync(CancellationToken ct = default) =>
        _db.Groomers
            .AsNoTracking()
            .Include(g => g.Category)
            .Include(g => g.User)
            .Include(g => g.WeeklyHours)
            .Include(g => g.Services)
            .Where(g => g.PublishStatus == BusinessPublishStatus.PendingReview)
            .OrderByDescending(g => g.Id)
            .ToListAsync(ct);
}
