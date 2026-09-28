using Microsoft.EntityFrameworkCore;
using WebAppPet.Infrastructure.Persistence;
using WebAppPet.Models;

namespace WebAppPet.Application.Favorites.GetFavorites;

/// <summary>
/// The published businesses the user saved as favorites, most recently saved first, with their
/// category for the card image. Favorites of businesses that were deactivated stay saved but hidden.
/// </summary>
public class GetFavoritesHandler
{
    private readonly AppDbContext _db;

    public GetFavoritesHandler(AppDbContext db) => _db = db;

    public Task<List<GroomerProfile>> HandleAsync(GetFavoritesQuery query, CancellationToken ct = default) =>
        _db.Favorites.AsNoTracking()
            .Where(f => f.UserId == query.UserId
                        && f.Groomer.IsActive
                        && f.Groomer.PublishStatus == BusinessPublishStatus.Approved)
            .OrderByDescending(f => f.Id)
            .Include(f => f.Groomer).ThenInclude(g => g.Category)
            .Select(f => f.Groomer)
            .ToListAsync(ct);
}
