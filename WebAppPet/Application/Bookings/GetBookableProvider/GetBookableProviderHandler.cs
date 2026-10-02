using Microsoft.EntityFrameworkCore;
using WebAppPet.Domain;
using WebAppPet.Infrastructure.Persistence;

namespace WebAppPet.Application.Bookings.GetBookableProvider;

/// <summary>The provider picked in a category flow, with its services and amenities.</summary>
public class GetBookableProviderHandler
{
    private readonly AppDbContext _db;

    public GetBookableProviderHandler(AppDbContext db) => _db = db;

    /// <summary>Null when the business doesn't exist or is inactive.</summary>
    public Task<GroomerProfile?> HandleAsync(GetBookableProviderQuery query, CancellationToken ct = default) =>
        _db.Groomers.AsNoTracking()
            .Include(g => g.Services)
            .Include(g => g.Amenities)
            .FirstOrDefaultAsync(g => g.Id == query.BusinessId && g.IsActive, ct);
}
