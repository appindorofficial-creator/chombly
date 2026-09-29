using WebAppPet.Application.Reminders.Shared;
using WebAppPet.Domain;
using WebAppPet.Domain.Markets;
using WebAppPet.Infrastructure.Persistence;

namespace WebAppPet.Application.Reminders.CreateCheckupReminder;

public enum CreateCheckupReminderOutcome
{
    Created,
    AlreadyActive,
    PetNotFound
}

/// <summary>
/// One-tap yearly vaccines/checkup reminder from the pet's Care page: first one in a week at 9:00,
/// held back between 21:00 and 8:00. Only one active per pet, so repeated taps do not stack alerts.
/// </summary>
public class CreateCheckupReminderHandler
{
    private readonly AppDbContext _db;

    public CreateCheckupReminderHandler(AppDbContext db) => _db = db;

    public async Task<CreateCheckupReminderOutcome> HandleAsync(CreateCheckupReminderCommand command, CancellationToken ct = default)
    {
        var pet = await _db.OwnedPetAsync(command.UserId, command.PetId, ct);
        if (pet is null)
            return CreateCheckupReminderOutcome.PetNotFound;

        if (await _db.ActiveCheckupAsync(command.UserId, pet.Id, ct) is not null)
            return CreateCheckupReminderOutcome.AlreadyActive;

        var nextLocal = AppTimeZones.TodayLocalDate().AddDays(7);

        await _db.AddScheduleAsync(new ReminderSchedule
        {
            UserId = command.UserId,
            PetId = pet.Id,
            Type = ReminderType.Vaccine,
            Title = $"{ReminderSchedules.CheckupTitlePrefix} {pet.Name}",
            Notes = "Aviso creado desde Control. Ajusta fecha o frecuencia si lo necesitas.",
            FrequencyDays = 365,
            NextDueUtc = AppTimeZones.LocalDateAndTimeToUtc(nextLocal, TimeSpan.FromHours(9)),
            QuietHoursStartLocal = TimeSpan.FromHours(21),
            QuietHoursEndLocal = TimeSpan.FromHours(8),
            Channel = ReminderChannel.InApp
        }, ct);

        return CreateCheckupReminderOutcome.Created;
    }
}
