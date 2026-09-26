using WebAppPet.Application.Businesses.Shared;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Businesses.AddService;

/// <summary>Adds a service priced by pet size. Needs a name and a price above zero.</summary>
public class AddServiceHandler
{
    public const string NightUnit = "noche";

    private readonly AppDbContext _db;

    public AddServiceHandler(AppDbContext db) => _db = db;

    /// <returns>True when a service was added.</returns>
    public async Task<bool> HandleAsync(AddServiceCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name) || command.Price <= 0)
            return false;

        var name = command.Name.Trim();
        var price = command.Price;
        var step = HotelCoreExtras.SizeStepFor(command.CountryCode);
        _db.Services.Add(new GroomerService
        {
            GroomerId = command.BusinessId,
            Name = name,
            Description = name,
            BillingUnit = command.BillingUnit,
            PriceSmall = price,
            PriceMedium = price + step,
            PriceLarge = price + step * 2,
            PriceGiant = price + step * 3,
            DurationMinutes = command.BillingUnit == NightUnit ? ServiceDurations.NightMinutes : ServiceDurations.FromName(name)
        });
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
