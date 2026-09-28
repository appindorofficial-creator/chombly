using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Behavior.Shared;
using WebAppPet.Infrastructure.Persistence;
using WebAppPet.Models;
using WebAppPet.Pages.Shared;
using WebAppPet.Services;

namespace WebAppPet.Application.Behavior.GetBehaviorProviders;

/// <summary>
/// Approved behavior specialists in the client's market, the client's cards and the free slots
/// of the chosen specialist on the chosen day (in the current market's time).
/// </summary>
public class GetBehaviorProvidersHandler
{
    private const int MaxProviders = 30;

    private readonly AppDbContext _db;
    private readonly ServiceCatalogService _catalog;

    public GetBehaviorProvidersHandler(AppDbContext db, ServiceCatalogService catalog)
    {
        _db = db;
        _catalog = catalog;
    }

    public async Task<BehaviorProviderOptions> HandleAsync(GetBehaviorProvidersQuery query, CancellationToken ct = default)
    {
        var behaviorCase = await _db.OwnedBehaviorCaseAsync(query.ClientId, query.CaseId, ct);
        if (BehaviorFlow.RedirectBeforeBooking(behaviorCase) is BehaviorStep redirect)
            return BehaviorProviderOptions.RedirectTo(redirect);

        var providers = BusinessMarketResolver.FilterHomeMarket(
                await _db.Groomers.AsNoTracking()
                    .Where(g => g.IsActive
                                && g.PublishStatus == BusinessPublishStatus.Approved
                                && g.VetProviderKind == VetProviderKind.BehaviorSpecialist)
                    .OrderByDescending(g => g.Rating)
                    .ToListAsync(ct),
                AppTimeZones.CurrentCountryCode)
            .Take(MaxProviders)
            .ToList();

        var payments = await _db.PaymentMethods.AsNoTracking()
            .Where(p => p.UserId == query.ClientId)
            .OrderByDescending(p => p.IsDefault)
            .ToListAsync(ct);

        var providerId = providers.Any(p => p.Id == query.ProviderId) ? query.ProviderId : 0;

        var (when, date) = BookingDate.NormalizeFromLegacy(query.When, query.Date);
        var hasDate = BookingDate.TryParseSelected(date, out var day);
        if (!hasDate)
            day = AppTimeZones.TodayLocalDate();

        var past = BookingTime.MarkPastSlots(BehaviorProviderOptions.TimeSlots, day);
        var occupied = hasDate && providerId > 0
            ? await OccupiedSlotsAsync(providerId, day, ct)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var slot = BehaviorProviderOptions.TimeSlots.FirstOrDefault(s => string.Equals(s, query.Slot, StringComparison.OrdinalIgnoreCase));
        if (slot is not null && (past.Contains(slot) || occupied.Contains(slot)))
            slot = null;

        return new BehaviorProviderOptions(
            null,
            behaviorCase,
            await _db.SelectedDogsAsync(query.ClientId, behaviorCase!, ct),
            await _catalog.GetAsync(ServiceCatalogCodes.BehaviorSession, ct),
            providers,
            payments,
            await DistanceLabelsAsync(query.ClientId, providers, ct),
            providerId,
            date,
            when,
            slot,
            past,
            occupied);
    }

    private async Task<HashSet<string>> OccupiedSlotsAsync(int providerId, DateTime day, CancellationToken ct)
    {
        var from = AppTimeZones.LocalDateAndTimeToUtc(day, TimeSpan.Zero);
        var to = AppTimeZones.LocalDateAndTimeToUtc(day.AddDays(1), TimeSpan.Zero);
        var taken = await _db.Appointments.AsNoTracking()
            .Where(a => a.GroomerId == providerId
                        && a.Status != AppointmentStatus.Cancelled
                        && a.ScheduledAt >= from
                        && a.ScheduledAt < to)
            .Select(a => a.ScheduledAt)
            .ToListAsync(ct);

        var occupied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var utc in taken)
        {
            var local = AppTimeZones.ToAppLocal(utc);
            foreach (var label in BehaviorProviderOptions.TimeSlots)
            {
                if (AppTimeZones.TryParseSlotToTimeSpan(label, out var timeOfDay) && timeOfDay == local.TimeOfDay)
                    occupied.Add(label);
            }
        }
        return occupied;
    }

    private async Task<Dictionary<int, string>> DistanceLabelsAsync(int clientId, List<GroomerProfile> providers, CancellationToken ct)
    {
        var labels = new Dictionary<int, string>();
        if (providers.Count == 0)
            return labels;

        var client = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == clientId, ct);
        if (client?.Latitude is not double lat || client.Longitude is not double lng)
            return labels;

        foreach (var provider in providers)
        {
            if (provider.Latitude == 0 && provider.Longitude == 0) continue;
            var label = GeoHelper.FormatDistanceOrPlace(GeoHelper.KmBetween(lat, lng, provider.Latitude, provider.Longitude), provider.City);
            if (!string.IsNullOrEmpty(label))
                labels[provider.Id] = label;
        }
        return labels;
    }
}
