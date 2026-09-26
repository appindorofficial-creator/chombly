using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Businesses.Shared;
using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Pages.Shared;
using WebAppPet.Services;

namespace WebAppPet.Application.Consultations.GetIntlSchedule;

/// <summary>
/// The chosen advisor's slots for today or tomorrow in the advisor's market, with the price and
/// whether the client's Care quick consult covers it. Null until an advisor is chosen.
/// </summary>
public class GetIntlScheduleHandler
{
    private readonly AppDbContext _db;
    private readonly ServiceCatalogService _catalog;
    private readonly ChomblyCareService _care;
    private readonly AvailabilityService _availability;
    private readonly ClientHomeCountry _homeCountry;

    public GetIntlScheduleHandler(
        AppDbContext db,
        ServiceCatalogService catalog,
        ChomblyCareService care,
        AvailabilityService availability,
        ClientHomeCountry homeCountry)
    {
        _db = db;
        _catalog = catalog;
        _care = care;
        _availability = availability;
        _homeCountry = homeCountry;
    }

    public async Task<IntlSchedule?> HandleAsync(GetIntlScheduleQuery query, CancellationToken ct = default)
    {
        var consultation = await _db.OwnedConsultationAsync(query.ClientId, query.ConsultationId, ct);
        if (consultation?.ProviderId is not int providerId)
            return null;

        var provider = await _db.Groomers.AsNoTracking().FirstOrDefaultAsync(g => g.Id == providerId, ct);
        var item = await _catalog.GetAsync(ServiceCatalogCodes.VetIntl30, ct);
        var catalogPrice = item?.Price ?? 30m;
        var price = provider?.StartingPrice > 0 ? provider.StartingPrice : catalogPrice;

        var usingCare = consultation.UsesCareBenefit;
        if (!usingCare && await _care.HasQuickConsultAvailableAsync(query.ClientId, ct))
        {
            usingCare = true;
            consultation.UsesCareBenefit = true;
            await _db.TouchAsync(consultation, ct);
        }

        var homeCountry = await _homeCountry.ResolveAsync(query.ClientId, ct);
        if (item is null || provider is null)
            return null;

        var market = AppTimeZones.MarketFromCountry(consultation.ContextCountry);
        using var _ = AppTimeZones.UseMarket(market);

        var day = AppTimeZones.TodayLocalDate(market);
        if (string.Equals(query.When, "mañana", StringComparison.OrdinalIgnoreCase))
            day = day.AddDays(1);

        var (slots, dayOpen) = await SlotsAsync(providerId, day);
        var past = BookingTime.MarkPastSlots(slots, day, market);

        var slot = query.Slot;
        if (!string.IsNullOrWhiteSpace(slot) && (!slots.Contains(slot, StringComparer.OrdinalIgnoreCase) || past.Contains(slot)))
            slot = null;

        return new IntlSchedule(consultation, provider, item, price, usingCare, homeCountry, slot, slots, past, dayOpen);
    }

    /// <summary>
    /// Half-hour slots inside the advisor's weekly hours for that day. Without weekly hours the
    /// default slots are offered.
    /// </summary>
    private async Task<(List<string> Slots, bool DayOpen)> SlotsAsync(int providerId, DateTime day)
    {
        var dayOpen = await _availability.IsAvailableOnAsync(providerId, day);

        var weekly = await _db.WeeklyHours.AsNoTracking()
            .FirstOrDefaultAsync(h => h.GroomerId == providerId && h.DayOfWeek == (int)day.DayOfWeek);

        if (weekly is { IsOpen: true })
            return (SlotsInWindow(weekly.OpenMinutes, weekly.CloseMinutes), true);
        if (dayOpen)
            return (BookingTime.DefaultSlots.ToList(), true);
        if (!await _db.WeeklyHours.AsNoTracking().AnyAsync(h => h.GroomerId == providerId))
            return (BookingTime.DefaultSlots.ToList(), true);
        return (new List<string>(), false);
    }

    private static List<string> SlotsInWindow(int openMinutes, int closeMinutes)
    {
        if (openMinutes == closeMinutes)
            return BookingTime.DefaultSlots.ToList();

        var slots = new List<string>();
        var end = closeMinutes > openMinutes ? closeMinutes : openMinutes + 8 * 60;
        for (var m = openMinutes; m + 30 <= end; m += 30)
        {
            var ts = TimeSpan.FromMinutes(m % (24 * 60));
            var dt = DateTime.Today.Add(ts);
            slots.Add(dt.ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture));
        }

        return slots.Count > 0 ? slots : BookingTime.DefaultSlots.ToList();
    }
}
