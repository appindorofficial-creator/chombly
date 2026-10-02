using Microsoft.EntityFrameworkCore;
using WebAppPet.Domain;
using WebAppPet.Infrastructure.Persistence;

namespace WebAppPet.Application.Bookings.GetBookingForm;

public class GetBookingFormHandler
{
    private readonly AppDbContext _db;

    public GetBookingFormHandler(AppDbContext db) => _db = db;

    /// <summary>Null when the business doesn't exist or is inactive.</summary>
    public async Task<BookingForm?> HandleAsync(GetBookingFormQuery query, CancellationToken ct = default)
    {
        var business = await _db.Groomers.AsNoTracking()
            .Include(g => g.Category)
            .FirstOrDefaultAsync(g => g.Id == query.BusinessId && g.IsActive, ct);
        if (business is null)
            return null;

        var services = await _db.Services.AsNoTracking()
            .Where(s => s.GroomerId == business.Id)
            .ToListAsync(ct);
        var extras = await _db.ServiceExtras.AsNoTracking()
            .Where(e => e.GroomerId == business.Id && e.IsActive)
            .ToListAsync(ct);

        var pets = new List<Pet>();
        var payments = new List<PaymentMethod>();
        if (query.ClientId is int clientId)
        {
            pets = await _db.Pets.AsNoTracking()
                .Where(p => p.OwnerId == clientId)
                .ToListAsync(ct);
            payments = await _db.PaymentMethods.AsNoTracking()
                .Where(p => p.UserId == clientId)
                .OrderByDescending(p => p.IsDefault).ThenBy(p => p.Id)
                .ToListAsync(ct);
        }

        return new BookingForm(business, IsOvernight(business.Category), services, extras, pets, payments);
    }

    public static bool IsOvernight(ServiceCategory? category) =>
        category?.IsOvernight == true
        || string.Equals(category?.Slug, "hotel", StringComparison.OrdinalIgnoreCase)
        || string.Equals(category?.Slug, "daycare", StringComparison.OrdinalIgnoreCase);
}
