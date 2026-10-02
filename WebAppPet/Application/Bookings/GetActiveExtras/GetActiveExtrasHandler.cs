using Microsoft.EntityFrameworkCore;
using WebAppPet.Domain;
using WebAppPet.Infrastructure.Persistence;

namespace WebAppPet.Application.Bookings.GetActiveExtras;

public class GetActiveExtrasHandler
{
    private readonly AppDbContext _db;

    public GetActiveExtrasHandler(AppDbContext db) => _db = db;

    /// <summary>Active add-ons of the given businesses, in storage order.</summary>
    public async Task<List<ServiceExtra>> HandleAsync(GetActiveExtrasQuery query, CancellationToken ct = default)
    {
        if (query.BusinessIds.Count == 0)
            return new List<ServiceExtra>();

        var ids = query.BusinessIds.ToList();
        return await _db.ServiceExtras.AsNoTracking()
            .Where(e => ids.Contains(e.GroomerId) && e.IsActive)
            .ToListAsync(ct);
    }
}
