using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;

namespace WebAppPet.Application.Reviews.GetReviews;

public class GetReviewsHandler
{
    private readonly AppDbContext _db;

    public GetReviewsHandler(AppDbContext db) => _db = db;

    public Task<List<Review>> HandleAsync(GetReviewsQuery query, CancellationToken ct = default) =>
        _db.Reviews.AsNoTracking()
            .Include(r => r.Client)
            .Where(r => r.GroomerId == query.GroomerId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(query.Take)
            .ToListAsync(ct);
}
