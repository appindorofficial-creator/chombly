using WebAppPet.Application.Businesses.Shared;
using WebAppPet.Application.Common;
using WebAppPet.Localization;

namespace WebAppPet.Application.Businesses.SaveWeeklySchedule;

/// <summary>Replaces the weekly hours and rebuilds the next 60 days of agenda, including manual overrides.</summary>
public class SaveWeeklyScheduleHandler
{
    public const int AgendaDays = 60;

    private readonly AvailabilityService _availability;

    public SaveWeeklyScheduleHandler(AvailabilityService availability) => _availability = availability;

    public async Task<Result> HandleAsync(SaveWeeklyScheduleCommand command, CancellationToken ct = default)
    {
        if (!command.Week.Any(d => d.IsOpen))
            return Result.Fail(CatalogLocalizer.Loc(
                "Debes tener al menos un día abierto.",
                "Open at least one day in your schedule."));

        await _availability.SaveWeeklyAndGenerateAsync(command.BusinessId, command.Week, days: AgendaDays);
        return Result.Ok();
    }
}
