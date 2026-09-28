using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;

namespace WebAppPet.Application.Favorites.ToggleFavorite;

public enum ToggleFavoriteOutcome
{
    NotFound,
    Added,
    Removed
}

/// <summary>Adds the business to the user's favorites, or removes it when it was already there.</summary>
public class ToggleFavoriteHandler
{
    private readonly AppDbContext _db;

    public ToggleFavoriteHandler(AppDbContext db) => _db = db;

    public async Task<ToggleFavoriteOutcome> HandleAsync(ToggleFavoriteCommand command, CancellationToken ct = default)
    {
        if (!await _db.Groomers.AnyAsync(g => g.Id == command.GroomerId && g.IsActive, ct))
            return ToggleFavoriteOutcome.NotFound;

        var favorite = await _db.Favorites
            .FirstOrDefaultAsync(f => f.UserId == command.UserId && f.GroomerId == command.GroomerId, ct);
        if (favorite is not null)
        {
            _db.Favorites.Remove(favorite);
            await _db.SaveChangesAsync(ct);
            return ToggleFavoriteOutcome.Removed;
        }

        _db.Favorites.Add(new Favorite { UserId = command.UserId, GroomerId = command.GroomerId });
        await _db.SaveChangesAsync(ct);
        return ToggleFavoriteOutcome.Added;
    }
}
