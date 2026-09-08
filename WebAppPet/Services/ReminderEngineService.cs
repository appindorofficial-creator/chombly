using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;

namespace WebAppPet.Services;

public class ReminderEngineService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ReminderEngineService> _logger;

    public ReminderEngineService(AppDbContext db, ILogger<ReminderEngineService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ReminderSchedule> CreateScheduleAsync(ReminderSchedule schedule, CancellationToken ct = default)
    {
        schedule.CreatedUtc = DateTime.UtcNow;
        schedule.IsActive = true;
        if (string.IsNullOrWhiteSpace(schedule.TimeZoneId))
            schedule.TimeZoneId = "America/New_York";
        _db.ReminderSchedules.Add(schedule);
        await _db.SaveChangesAsync(ct);
        return schedule;
    }

    public async Task<ReminderSchedule?> UpdateScheduleAsync(
        int scheduleId,
        int userId,
        Action<ReminderSchedule> apply,
        CancellationToken ct = default)
    {
        var s = await _db.ReminderSchedules.FirstOrDefaultAsync(x => x.Id == scheduleId && x.UserId == userId, ct);
        if (s is null) return null;
        apply(s);
        await _db.SaveChangesAsync(ct);
        return s;
    }

    public async Task<bool> DeactivateAsync(int scheduleId, int userId, CancellationToken ct = default)
    {
        var s = await _db.ReminderSchedules.FirstOrDefaultAsync(x => x.Id == scheduleId && x.UserId == userId, ct);
        if (s is null) return false;
        s.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public Task<List<ReminderSchedule>> ListForPetAsync(int userId, int petId, CancellationToken ct = default) =>
        _db.ReminderSchedules.AsNoTracking()
            .Where(r => r.UserId == userId && r.PetId == petId && r.IsActive)
            .OrderBy(r => r.NextDueUtc)
            .ToListAsync(ct);

    public Task<List<ReminderSchedule>> ListForUserAsync(int userId, CancellationToken ct = default) =>
        _db.ReminderSchedules.AsNoTracking()
            .Include(r => r.Pet)
            .Where(r => r.UserId == userId && r.IsActive)
            .OrderBy(r => r.NextDueUtc)
            .ToListAsync(ct);

    /// <summary>
    /// Processes due reminders. If local time is inside quiet hours, schedules delivery for quiet-hours end
    /// (SuppressedQuietHours) without advancing NextDueUtc until actually sent.
    /// </summary>
    public async Task<int> ProcessDueRemindersAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var due = await _db.ReminderSchedules
            .Where(r => r.IsActive && r.NextDueUtc <= now)
            .Take(100)
            .ToListAsync(ct);

        var processed = 0;
        foreach (var schedule in due)
        {
            try
            {
                await ProcessOneAsync(schedule, now, ct);
                processed++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Reminder {Id} failed", schedule.Id);
                _db.ReminderDeliveries.Add(new ReminderDelivery
                {
                    ReminderScheduleId = schedule.Id,
                    ScheduledForUtc = now,
                    Status = ReminderDeliveryStatus.Failed,
                    Message = ex.Message.Length > 400 ? ex.Message[..400] : ex.Message
                });
                await _db.SaveChangesAsync(ct);
            }
        }

        return processed;
    }

    private async Task ProcessOneAsync(ReminderSchedule schedule, DateTime nowUtc, CancellationToken ct)
    {
        var tz = ResolveTimeZone(schedule.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), tz);

        if (IsInQuietHours(localNow.TimeOfDay, schedule.QuietHoursStartLocal, schedule.QuietHoursEndLocal))
        {
            var sendLocal = NextQuietHoursEnd(localNow, schedule.QuietHoursEndLocal ?? TimeSpan.FromHours(8));
            var scheduledUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(sendLocal, DateTimeKind.Unspecified), tz);

            var alreadyDeferred = await _db.ReminderDeliveries.AnyAsync(
                d => d.ReminderScheduleId == schedule.Id &&
                     d.Status == ReminderDeliveryStatus.SuppressedQuietHours &&
                     d.ScheduledForUtc == scheduledUtc, ct);
            if (!alreadyDeferred)
            {
                _db.ReminderDeliveries.Add(new ReminderDelivery
                {
                    ReminderScheduleId = schedule.Id,
                    ScheduledForUtc = scheduledUtc,
                    Status = ReminderDeliveryStatus.SuppressedQuietHours,
                    Message = "Deferred until quiet hours end"
                });
            }

            // Hold NextDueUtc at quiet-hours end so background job sends then.
            schedule.NextDueUtc = scheduledUtc;
            await _db.SaveChangesAsync(ct);
            return;
        }

        var petName = "";
        if (schedule.PetId is int petId)
        {
            petName = await _db.Pets.AsNoTracking()
                .Where(p => p.Id == petId)
                .Select(p => p.Name)
                .FirstOrDefaultAsync(ct) ?? "";
        }

        var message = string.IsNullOrWhiteSpace(schedule.Notes)
            ? schedule.Title + (string.IsNullOrEmpty(petName) ? "" : $" · {petName}")
            : schedule.Notes;

        var notification = new AppNotification
        {
            UserId = schedule.UserId,
            Title = schedule.Title,
            Message = message.Length > 400 ? message[..400] : message,
            Type = "reminder-" + schedule.Type.ToString().ToLowerInvariant(),
            CreatedAt = DateTime.UtcNow
        };
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(ct);

        _db.ReminderDeliveries.Add(new ReminderDelivery
        {
            ReminderScheduleId = schedule.Id,
            ScheduledForUtc = nowUtc,
            SentUtc = DateTime.UtcNow,
            Status = ReminderDeliveryStatus.Sent,
            Message = notification.Message,
            AppNotificationId = notification.Id
        });

        schedule.LastSentUtc = DateTime.UtcNow;
        if (schedule.FrequencyDays is int days && days > 0)
            schedule.NextDueUtc = nowUtc.AddDays(days);
        else
            schedule.IsActive = false;

        await _db.SaveChangesAsync(ct);
    }

    public static bool IsInQuietHours(TimeSpan localTime, TimeSpan? start, TimeSpan? end)
    {
        if (start is null || end is null) return false;
        var s = start.Value;
        var e = end.Value;
        // Overnight window e.g. 21:00–08:00
        if (s <= e)
            return localTime >= s && localTime < e;
        return localTime >= s || localTime < e;
    }

    private static DateTime NextQuietHoursEnd(DateTime localNow, TimeSpan end)
    {
        var candidate = localNow.Date.Add(end);
        if (candidate <= localNow)
            candidate = candidate.AddDays(1);
        return candidate;
    }

    private static TimeZoneInfo ResolveTimeZone(string? id)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(id))
                return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch
        {
            /* fall through */
        }

        try { return TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
    }
}
