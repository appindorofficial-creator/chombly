using WebAppPet.Application.Consultations.GetIntlSchedule;
using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Pages.Shared;
using WebAppPet.Services;

namespace WebAppPet.Application.Consultations.ScheduleIntlConsultation;

/// <summary>
/// Schedules the consultation with the chosen advisor at a free slot today or tomorrow (advisor's
/// local time) and records the international guidance consent.
/// </summary>
public class ScheduleIntlConsultationHandler
{
    private readonly AppDbContext _db;
    private readonly GetIntlScheduleHandler _schedule;
    private readonly ConsentService _consent;
    private readonly VetAuditService _audit;

    public ScheduleIntlConsultationHandler(AppDbContext db, GetIntlScheduleHandler schedule, ConsentService consent, VetAuditService audit)
    {
        _db = db;
        _schedule = schedule;
        _consent = consent;
        _audit = audit;
    }

    public async Task<ScheduleIntlConsultationResult> HandleAsync(ScheduleIntlConsultationCommand command, CancellationToken ct = default)
    {
        var schedule = await _schedule.HandleAsync(
            new GetIntlScheduleQuery(command.ClientId, command.ConsultationId, command.When, command.Slot), ct);
        if (schedule is null)
            return new ScheduleIntlConsultationResult(ScheduleIntlConsultationOutcome.NotFound, null);

        var invalid = new ScheduleIntlConsultationResult(ScheduleIntlConsultationOutcome.Invalid, schedule);
        if (!command.AcceptScope)
            return invalid;

        var tomorrow = string.Equals(command.When, "mañana", StringComparison.OrdinalIgnoreCase);
        if (!tomorrow && !string.Equals(command.When, "hoy", StringComparison.OrdinalIgnoreCase))
            return invalid;

        var consultation = schedule.Consultation;
        var market = AppTimeZones.MarketFromCountry(consultation.ContextCountry);
        using var _ = AppTimeZones.UseMarket(market);

        var day = AppTimeZones.TodayLocalDate(market);
        if (tomorrow)
            day = day.AddDays(1);

        if (!schedule.DayOpen || schedule.TimeSlots.Count == 0)
            return invalid;
        if (!BookingTime.IsSlotAvailable(schedule.Slot, schedule.TimeSlots, schedule.PastSlots, new HashSet<string>()))
            return invalid;
        if (!AppTimeZones.TryParseSlotToTimeSpan(schedule.Slot, out var timeOfDay))
            return invalid;

        consultation.ScheduledAt = AppTimeZones.LocalDateAndTimeToUtc(day, timeOfDay, market);
        consultation.UsesCareBenefit = schedule.UsingCare;
        consultation.ServiceCatalogCode = ServiceCatalogCodes.VetIntl30;
        consultation.Status = ConsultationStatus.ProviderSelected;
        await _db.TouchAsync(consultation, ct);

        await _consent.SaveAsync(command.ClientId, consultation.Id, new[]
        {
            (ConsentService.DocIntlOrientation, command.AcceptScope)
        }, command.IpAddress, command.UserAgent, ct);

        await _audit.LogAsync("schedule_selected", command.ClientId, "Consultation", consultation.Id,
            new { when = command.When, slot = schedule.Slot, care = schedule.UsingCare }, ct);

        return new ScheduleIntlConsultationResult(ScheduleIntlConsultationOutcome.Scheduled, schedule);
    }
}
