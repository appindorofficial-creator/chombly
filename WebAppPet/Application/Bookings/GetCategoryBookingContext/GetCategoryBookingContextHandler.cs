using Microsoft.EntityFrameworkCore;
using WebAppPet.Domain;
using WebAppPet.Infrastructure.Persistence;

namespace WebAppPet.Application.Bookings.GetCategoryBookingContext;

/// <param name="Category">Null when the category doesn't exist or is inactive.</param>
/// <param name="Pets">By name.</param>
/// <param name="Payments">Default card first.</param>
public sealed record CategoryBookingContext(
    ServiceCategory? Category,
    double? ClientLatitude,
    double? ClientLongitude,
    List<Pet> Pets,
    List<PaymentMethod> Payments);

/// <summary>What the Hotel, Daycare, Walkers and Trainers flows need before searching providers.</summary>
public class GetCategoryBookingContextHandler
{
    private readonly AppDbContext _db;

    public GetCategoryBookingContextHandler(AppDbContext db) => _db = db;

    public async Task<CategoryBookingContext> HandleAsync(GetCategoryBookingContextQuery query, CancellationToken ct = default)
    {
        var category = await _db.Categories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Slug == query.CategorySlug && c.IsActive, ct);

        if (query.ClientId is not int clientId)
            return new CategoryBookingContext(category, null, null, new List<Pet>(), new List<PaymentMethod>());

        var client = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == clientId, ct);
        var pets = await _db.Pets.AsNoTracking()
            .Where(p => p.OwnerId == clientId)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
        var payments = await _db.PaymentMethods.AsNoTracking()
            .Where(p => p.UserId == clientId)
            .OrderByDescending(p => p.IsDefault)
            .ToListAsync(ct);

        return new CategoryBookingContext(category, client?.Latitude, client?.Longitude, pets, payments);
    }
}
