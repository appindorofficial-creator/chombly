using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;

namespace WebAppPet.Application.Businesses.AddAmenity;

/// <summary>Appends an amenity at the end of the business list. A blank label is ignored.</summary>
public class AddAmenityHandler
{
    public const string DefaultIcon = "✓";

    private readonly AppDbContext _db;

    public AddAmenityHandler(AppDbContext db) => _db = db;

    /// <returns>True when an amenity was added.</returns>
    public async Task<bool> HandleAsync(AddAmenityCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Label))
            return false;

        _db.Amenities.Add(new BusinessAmenity
        {
            GroomerId = command.BusinessId,
            Label = command.Label.Trim(),
            Icon = string.IsNullOrWhiteSpace(command.Icon) ? DefaultIcon : command.Icon,
            SortOrder = await _db.Amenities.CountAsync(a => a.GroomerId == command.BusinessId, ct)
        });
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
