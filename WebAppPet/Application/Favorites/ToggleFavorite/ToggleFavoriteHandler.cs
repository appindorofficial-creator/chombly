using Microsoft.EntityFrameworkCore;
using WebAppPet.Infrastructure.Persistence;
using WebAppPet.Models;

namespace WebAppPet.Application.Favorites.ToggleFavorite;

public enum ToggleFavoriteOutcome
{
    NotFound,
    Added,
    Removed
}

/// <summary>
/// Removes the business from the user's favorites when it was there, even if it is no longer published;
/// otherwise adds it, which only published businesses allow.
/// </summary>
public class ToggleFavoriteHandler
{
    private readonly AppDbContext _db;

    public ToggleFavoriteHandler(AppDbContext db) => _db = db;

    public async Task<ToggleFavoriteOutcome> HandleAsync(ToggleFavoriteCommand command, CancellationToken ct = default)
    {
        var favorite = await _db.Favorites
            .FirstOrDefaultAsync(f => f.UserId == command.UserId && f.GroomerId == command.GroomerId, ct);
        if (favorite is not null)
        {
            _db.Favorites.Remove(favorite);
            await _db.SaveChangesAsync(ct);
            return ToggleFavoriteOutcome.Removed;
        }

        if (!await _db.Groomers.AnyAsync(g => g.Id == command.GroomerId
                                              && g.IsActive
                                              && g.PublishStatus == BusinessPublishStatus.Approved, ct))
            return ToggleFavoriteOutcome.NotFound;

        _db.Favorites.Add(new Favorite { UserId = command.UserId, GroomerId = command.GroomerId });
        await _db.SaveChangesAsync(ct);
        return ToggleFavoriteOutcome.Added;
    }
}
