using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Localization;

namespace WebAppPet.Application.Businesses.ToggleAvailabilityDay;

/// <summary>Opens or closes a single agenda day by hand. Days of other businesses are ignored.</summary>
public class ToggleAvailabilityDayHandler
{
    private readonly AppDbContext _db;

    public ToggleAvailabilityDayHandler(AppDbContext db) => _db = db;

    /// <returns>True when the day was found and toggled.</returns>
    public async Task<bool> HandleAsync(ToggleAvailabilityDayCommand command, CancellationToken ct = default)
    {
        var row = await _db.DayAvailabilities
            .FirstOrDefaultAsync(a => a.Id == command.DayId && a.GroomerId == command.BusinessId, ct);
        if (row is null)
            return false;

        row.IsAvailable = !row.IsAvailable;
        row.Note = row.IsAvailable
            ? CatalogLocalizer.Loc("Abierto (manual)", "Open (manual)")
            : CatalogLocalizer.Loc("Cerrado (manual)", "Closed (manual)");
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
