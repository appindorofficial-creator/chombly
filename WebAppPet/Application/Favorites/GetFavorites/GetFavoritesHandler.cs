using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;

namespace WebAppPet.Application.Favorites.GetFavorites;

/// <summary>The businesses the user saved as favorites, with their category for the card image.</summary>
public class GetFavoritesHandler
{
    private readonly AppDbContext _db;

    public GetFavoritesHandler(AppDbContext db) => _db = db;

    public Task<List<GroomerProfile>> HandleAsync(GetFavoritesQuery query, CancellationToken ct = default) =>
        _db.Favorites.AsNoTracking()
            .Where(f => f.UserId == query.UserId)
            .Include(f => f.Groomer).ThenInclude(g => g.Category)
            .Select(f => f.Groomer)
            .ToListAsync(ct);
}
