using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Businesses.UpdateBusiness;

/// <summary>
/// Saves the business profile from the owner's panel. Everything is validated before anything changes.
/// The owner's home market and the license country follow the business location.
/// </summary>
public class UpdateBusinessHandler
{
    private readonly AppDbContext _db;

    public UpdateBusinessHandler(AppDbContext db) => _db = db;

    public async Task<UpdateBusinessError> HandleAsync(UpdateBusinessCommand command, CancellationToken ct = default)
    {
        var business = await _db.Groomers
            .Include(g => g.User)
            .FirstOrDefaultAsync(g => g.Id == command.BusinessId, ct);
        if (business is null)
            return UpdateBusinessError.NotFound;

        var categories = await _db.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);
        var selected = command.CategoryIds
            .Where(id => id > 0 && categories.Any(c => c.Id == id))
            .Distinct()
            .ToList();
        if (selected.Count == 0)
            return UpdateBusinessError.NoCategory;

        if (!PhoneValidator.TryNormalize(command.Phone, out var phoneNorm, required: true))
            return UpdateBusinessError.PhoneInvalid;

        var primary = command.CategoryId is int keep && selected.Contains(keep)
            ? keep
            : categories.First(c => selected.Contains(c.Id)).Id;

        business.BusinessName = command.BusinessName.Trim();
        business.CategoryId = primary;
        business.ExtraCategoryIds = GroomerProfile.JoinExtraCategoryIds(selected, primary);
        business.Address = command.Address.Trim();
        business.City = command.City.Trim();
        var lat = command.Latitude;
        var lng = command.Longitude;
        GeoHelper.TryRepairCoordinates(ref lat, ref lng);
        business.Latitude = lat;
        business.Longitude = lng;
        if (business.User is { } owner)
        {
            MarketCountry.ApplyFromLocation(owner, business.City, business.Latitude, business.Longitude);
            business.LicenseCountry = owner.CountryCode;
        }
        else
        {
            business.LicenseCountry = MarketCountry.ResolveFromLocation(business.City, business.Latitude, business.Longitude);
        }
        business.About = command.About.Trim();
        business.Phone = phoneNorm;
        business.ImageUrl = command.ImageUrl;
        business.PriceUnit = command.PriceUnit;
        business.StartingPrice = command.StartingPrice;
        business.AcceptedSpecies = string.IsNullOrWhiteSpace(command.AcceptedSpecies)
            ? PetSpecies.DefaultAcceptedList
            : command.AcceptedSpecies;
        business.AcceptsSeniorPets = command.AcceptsSeniorPets;
        business.AcceptsAnxiousPets = command.AcceptsAnxiousPets;
        business.IsFeatured = command.IsFeatured;
        business.IsActive = command.IsActive;
        await _db.SaveChangesAsync(ct);

        var isHotel = categories.Any(c => selected.Contains(c.Id)
            && string.Equals(c.Slug, "hotel", StringComparison.OrdinalIgnoreCase));
        if (isHotel)
        {
            var extras = command.HotelExtras;
            var iso = business.User?.CountryCode ?? business.LicenseCountry ?? AppTimeZones.CurrentCountryCode;
            await HotelCoreExtras.SyncAsync(_db, business.Id, extras.BathPrice, extras.MedsPrice, iso);
            await HotelPrivateCameraExtra.SyncAsync(_db, business.Id, extras.OffersPrivateCamera, extras.PrivateCameraPrice, iso);
        }
        else
        {
            await HotelPrivateCameraExtra.SyncAsync(_db, business.Id, enabled: false);
        }

        return UpdateBusinessError.None;
    }
}
