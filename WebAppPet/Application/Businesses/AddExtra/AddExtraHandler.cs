using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Businesses.AddExtra;

/// <summary>
/// Adds a bookable extra. Blank names are ignored, and so are the hotel extras (bath, meds, private camera),
/// which are managed from the hotel section of the profile.
/// </summary>
public class AddExtraHandler
{
    private readonly AppDbContext _db;

    public AddExtraHandler(AppDbContext db) => _db = db;

    /// <returns>True when an extra was added.</returns>
    public async Task<bool> HandleAsync(AddExtraCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return false;

        var name = command.Name.Trim();
        if (HotelCoreExtras.IsManagedHotelExtra(name))
            return false;

        _db.ServiceExtras.Add(new ServiceExtra
        {
            GroomerId = command.BusinessId,
            Name = name,
            Price = command.Price < 0 ? 0 : command.Price,
            IsActive = true
        });
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
