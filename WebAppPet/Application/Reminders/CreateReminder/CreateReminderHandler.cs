using System.Globalization;
using WebAppPet.Application.Reminders.Shared;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Reminders.CreateReminder;

public enum CreateReminderOutcome
{
    PetNotFound,
    NoType,
    NoDate,
    PastDate,
    Created
}

public class CreateReminderHandler
{
    private readonly AppDbContext _db;

    public CreateReminderHandler(AppDbContext db) => _db = db;

    public async Task<CreateReminderOutcome> HandleAsync(CreateReminderCommand command, CancellationToken ct = default)
    {
        if (await _db.OwnedPetAsync(command.UserId, command.PetId, ct) is null)
            return CreateReminderOutcome.PetNotFound;
        if (command.Type is not ReminderType type)
            return CreateReminderOutcome.NoType;
        if (command.NextDueLocal is not DateTime nextDueLocal)
            return CreateReminderOutcome.NoDate;
        if (nextDueLocal.Date < AppTimeZones.TodayLocalDate())
            return CreateReminderOutcome.PastDate;

        var title = string.IsNullOrWhiteSpace(command.Title) ? DefaultTitle(type) : command.Title.Trim();

        await _db.AddScheduleAsync(new ReminderSchedule
        {
            UserId = command.UserId,
            PetId = command.PetId,
            Type = type,
            Title = title,
            Notes = command.Notes,
            FrequencyDays = command.FrequencyDays is > 0 ? command.FrequencyDays : null,
            NextDueUtc = AppTimeZones.LocalDateAndTimeToUtc(nextDueLocal.Date, TimeSpan.FromHours(9)),
            QuietHoursStartLocal = ParseTime(command.QuietStart),
            QuietHoursEndLocal = ParseTime(command.QuietEnd),
            Channel = ReminderChannel.InApp
        }, ct);

        return CreateReminderOutcome.Created;
    }

    private static string DefaultTitle(ReminderType type) => type switch
    {
        ReminderType.Vaccine => "Recordatorio de vacuna",
        ReminderType.Medication => "Recordatorio de medicamento",
        ReminderType.Appointment => "Recordatorio de cita",
        _ => "Recordatorio de cuidado"
    };

    private static TimeSpan? ParseTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var ts)) return ts;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt.TimeOfDay;
        return null;
    }
}
