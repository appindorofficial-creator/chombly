using Microsoft.EntityFrameworkCore;
using WebAppPet.Infrastructure.Persistence;

namespace WebAppPet.Application.Reminders.DeactivateReminder;

/// <summary>Stops a reminder; its delivery history is kept. False when it is not the user's.</summary>
public class DeactivateReminderHandler
{
    private readonly AppDbContext _db;

    public DeactivateReminderHandler(AppDbContext db) => _db = db;

    public async Task<bool> HandleAsync(DeactivateReminderCommand command, CancellationToken ct = default)
    {
        var schedule = await _db.ReminderSchedules
            .FirstOrDefaultAsync(r => r.Id == command.ScheduleId && r.UserId == command.UserId, ct);
        if (schedule is null)
            return false;

        schedule.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
