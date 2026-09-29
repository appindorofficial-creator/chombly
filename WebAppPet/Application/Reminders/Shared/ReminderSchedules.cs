using Microsoft.EntityFrameworkCore;
using WebAppPet.Domain;
using WebAppPet.Domain.Markets;
using WebAppPet.Infrastructure.Persistence;

namespace WebAppPet.Application.Reminders.Shared;

public static class ReminderSchedules
{
    public const string CheckupTitlePrefix = "Vacunas / chequeo ·";
    private const string EnglishCheckupTitlePrefix = "Vaccines / checkup ·";

    /// <summary>
    /// The pet's active vaccines/checkup reminder created from its Care page, if any.
    /// Older rows were saved in English when the screen was in English.
    /// </summary>
    public static Task<ReminderSchedule?> ActiveCheckupAsync(this AppDbContext db, int userId, int petId, CancellationToken ct = default) =>
        db.ReminderSchedules.AsNoTracking()
            .Where(r => r.UserId == userId && r.PetId == petId && r.IsActive &&
                        r.Type == ReminderType.Vaccine &&
                        (r.Title.StartsWith(CheckupTitlePrefix) || r.Title.StartsWith(EnglishCheckupTitlePrefix)))
            .OrderBy(r => r.NextDueUtc)
            .FirstOrDefaultAsync(ct);

    public static Task<Pet?> OwnedPetAsync(this AppDbContext db, int userId, int petId, CancellationToken ct = default) =>
        db.Pets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == petId && p.OwnerId == userId, ct);

    public static Task<List<ReminderSchedule>> ActiveForPetAsync(this AppDbContext db, int userId, int petId, CancellationToken ct = default) =>
        db.ReminderSchedules.AsNoTracking()
            .Where(r => r.UserId == userId && r.PetId == petId && r.IsActive)
            .OrderBy(r => r.NextDueUtc)
            .ToListAsync(ct);

    /// <summary>
    /// Saves a new active reminder. Quiet hours are evaluated in the family's market zone, the same
    /// one used to turn the chosen local date into NextDueUtc.
    /// </summary>
    public static async Task AddScheduleAsync(this AppDbContext db, ReminderSchedule schedule, CancellationToken ct = default)
    {
        schedule.CreatedUtc = DateTime.UtcNow;
        schedule.IsActive = true;
        schedule.TimeZoneId = AppTimeZones.TimeZoneId;
        db.ReminderSchedules.Add(schedule);
        await db.SaveChangesAsync(ct);
    }
}
