using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;

namespace WebAppPet.Application.Reviews.Shared;

/// <summary>Keeps <c>GroomerProfile.Rating</c> / <c>ReviewCount</c> in sync with the Reviews table.</summary>
public class RatingCalculator
{
    private readonly AppDbContext _db;

    public RatingCalculator(AppDbContext db) => _db = db;

    public async Task RecalculateAsync(int groomerId, CancellationToken ct = default)
    {
        var g = await _db.Groomers.FirstOrDefaultAsync(x => x.Id == groomerId, ct);
        if (g == null) return;

        var stats = await _db.Reviews.AsNoTracking()
            .Where(r => r.GroomerId == groomerId)
            .GroupBy(_ => 1)
            .Select(grp => new { Count = grp.Count(), Avg = grp.Average(r => (double)r.Rating) })
            .FirstOrDefaultAsync(ct);

        if (stats == null || stats.Count == 0)
        {
            g.ReviewCount = 0;
            g.Rating = 0;
        }
        else
        {
            g.ReviewCount = stats.Count;
            g.Rating = Math.Round(stats.Avg, 1);
        }

        await _db.SaveChangesAsync(ct);
    }
}
