using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;

namespace WebAppPet.Application.Favorites.GetFavoriteIds;

/// <summary>Ids of the user's favorite businesses, to fill the hearts on listings and detail pages.</summary>
public class GetFavoriteIdsHandler
{
    private readonly AppDbContext _db;

    public GetFavoriteIdsHandler(AppDbContext db) => _db = db;

    public async Task<HashSet<int>> HandleAsync(GetFavoriteIdsQuery query, CancellationToken ct = default) =>
        (await _db.Favorites.AsNoTracking()
            .Where(f => f.UserId == query.UserId)
            .Select(f => f.GroomerId)
            .ToListAsync(ct))
        .ToHashSet();
}
