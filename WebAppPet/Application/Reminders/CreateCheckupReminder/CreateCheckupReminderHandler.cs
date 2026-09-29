using WebAppPet.Application.Reminders.Shared;
using WebAppPet.Domain;
using WebAppPet.Domain.Markets;
using WebAppPet.Infrastructure.Persistence;

namespace WebAppPet.Application.Reminders.CreateCheckupReminder;

/// <summary>
/// One-tap yearly vaccines/checkup reminder from the pet's Care page: first one in a week at 9:00,
/// held back between 21:00 and 8:00. False when the pet is not the user's.
/// </summary>
public class CreateCheckupReminderHandler
{
    private readonly AppDbContext _db;

    public CreateCheckupReminderHandler(AppDbContext db) => _db = db;

    public async Task<bool> HandleAsync(CreateCheckupReminderCommand command, CancellationToken ct = default)
    {
        var pet = await _db.OwnedPetAsync(command.UserId, command.PetId, ct);
        if (pet is null)
            return false;

        var nextLocal = AppTimeZones.TodayLocalDate().AddDays(7);

        await _db.AddScheduleAsync(new ReminderSchedule
        {
            UserId = command.UserId,
            PetId = pet.Id,
            Type = ReminderType.Vaccine,
            Title = $"Vacunas / chequeo · {pet.Name}",
            Notes = "Aviso creado desde Control. Ajusta fecha o frecuencia si lo necesitas.",
            FrequencyDays = 365,
            NextDueUtc = AppTimeZones.LocalDateAndTimeToUtc(nextLocal, TimeSpan.FromHours(9)),
            QuietHoursStartLocal = TimeSpan.FromHours(21),
            QuietHoursEndLocal = TimeSpan.FromHours(8),
            Channel = ReminderChannel.InApp
        }, ct);

        return true;
    }
}
