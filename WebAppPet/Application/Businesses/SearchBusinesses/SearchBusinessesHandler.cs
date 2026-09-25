using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Businesses.Shared;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Businesses.SearchBusinesses;

/// <summary>
/// Approved businesses of one primary category, in the home market, accepting every selected pet.
/// Ordered by distance then rating when the user has a location, otherwise featured then rating.
/// </summary>
public class SearchBusinessesHandler
{
    private readonly AppDbContext _db;
    private readonly AvailabilityService _availability;

    public SearchBusinessesHandler(AppDbContext db, AvailabilityService availability)
    {
        _db = db;
        _availability = availability;
    }

    public async Task<List<BusinessMatch>> HandleAsync(SearchBusinessesQuery query, CancellationToken ct = default)
    {
        if (query.PetSpecies.Count == 0) return new List<BusinessMatch>();

        var businesses = await _db.Groomers
            .Include(g => g.Category)
            .Include(g => g.Amenities)
            .Include(g => g.Services)
            .Where(g => g.IsActive && g.PublishStatus == BusinessPublishStatus.Approved
                        && g.Category != null && g.Category.Slug == query.CategorySlug)
            .OrderByDescending(g => g.IsFeatured)
            .ThenByDescending(g => g.Rating)
            .ToListAsync(ct);

        businesses = BusinessMarketResolver.FilterHomeMarket(businesses, query.CountryCode)
            .Where(g => query.RequiredSpecies is null || g.AcceptsSpecies(query.RequiredSpecies))
            .Where(g => BusinessListing.AcceptsAll(g, query.PetSpecies))
            .ToList();

        var openNow = await _availability.TodayMapAsync(businesses.Select(g => g.Id));

        if (query.OpenOn is DateTime day)
        {
            var open = await _availability.OpenOnAsync(businesses.Select(g => g.Id), day);
            businesses = businesses.Where(g => open.Contains(g.Id)).ToList();
        }

        var matches = businesses.Select(g =>
        {
            var (miles, label) = BusinessListing.DistanceFrom(query.UserLatitude, query.UserLongitude, g);
            return new BusinessMatch(g, miles, label, openNow.GetValueOrDefault(g.Id, false));
        }).ToList();

        if (query.UserLatitude != null)
            matches = matches.OrderBy(m => m.Miles ?? double.MaxValue)
                .ThenByDescending(m => m.Business.Rating)
                .ToList();

        return matches;
    }
}
