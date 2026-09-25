using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Businesses.GetAvailability;

public class GetAvailabilityHandler
{
    public const int AgendaDaysShown = 30;

    private readonly AppDbContext _db;

    public GetAvailabilityHandler(AppDbContext db) => _db = db;

    public async Task<AvailabilityView> HandleAsync(GetAvailabilityQuery query, CancellationToken ct = default)
    {
        var hours = await _db.WeeklyHours
            .AsNoTracking()
            .Where(h => h.GroomerId == query.BusinessId)
            .OrderBy(h => h.DayOfWeek)
            .ToListAsync(ct);

        var week = WeekDayInput.DefaultWeek();
        foreach (var h in hours)
        {
            var row = week.FirstOrDefault(w => w.DayOfWeek == h.DayOfWeek);
            if (row == null) continue;
            row.IsOpen = h.IsOpen;
            row.OpenTime = h.OpenLabel;
            row.CloseTime = h.CloseLabel;
        }

        var start = AppTimeZones.TodayLocalDate();
        var end = start.AddDays(AgendaDaysShown);
        var days = await _db.DayAvailabilities
            .AsNoTracking()
            .Where(a => a.GroomerId == query.BusinessId && a.Day >= start && a.Day < end)
            .OrderBy(a => a.Day)
            .ToListAsync(ct);

        return new AvailabilityView(week, days);
    }
}
