using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Businesses.Shared;
using WebAppPet.Domain;
using WebAppPet.Domain.Markets;
using WebAppPet.Infrastructure.Persistence;

namespace WebAppPet.Application.Businesses.SearchHotels;

/// <summary>
/// Approved businesses in the home market that offer hotel stays, as primary or extra category.
/// Featured first, then by rating; species, amenity and availability filters are left to the caller.
/// </summary>
public class SearchHotelsHandler
{
    private readonly AppDbContext _db;

    public SearchHotelsHandler(AppDbContext db) => _db = db;

    public async Task<List<GroomerProfile>> HandleAsync(SearchHotelsQuery query, CancellationToken ct = default)
    {
        var hotels = await _db.Groomers.AsNoTracking()
            .Include(g => g.Category)
            .Include(g => g.Amenities)
            .Include(g => g.Services)
            .Where(g => g.IsActive && g.PublishStatus == BusinessPublishStatus.Approved)
            .OrderByDescending(g => g.IsFeatured)
            .ThenByDescending(g => g.Rating)
            .ToListAsync(ct);

        hotels = query.HotelCategoryId is int categoryId
            ? hotels.Where(h => h.OffersCategory(categoryId)).ToList()
            : hotels.Where(h => h.Category != null && h.Category.Slug == "hotel").ToList();

        return BusinessMarketResolver.FilterHomeMarket(hotels, query.CountryCode).ToList();
    }
}
