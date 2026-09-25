using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Businesses.Shared;

public class AvailabilityService
{
    private readonly AppDbContext _db;
    private static readonly Random Rng = new();

    public AvailabilityService(AppDbContext db) => _db = db;

    /// <summary>Day-level availability (booking calendar). Does not check wall-clock time.</summary>
    public async Task<bool> IsAvailableOnAsync(int groomerId, DateTime day)
    {
        var d = day.Date;
        var row = await _db.DayAvailabilities
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.GroomerId == groomerId && a.Day == d);
        if (row != null) return row.IsAvailable;

        var weekly = await _db.WeeklyHours.AsNoTracking()
            .FirstOrDefaultAsync(h => h.GroomerId == groomerId && h.DayOfWeek == (int)d.DayOfWeek);
        if (weekly != null) return weekly.IsOpen;

        // Sin agenda: no asumir abierto
        return false;
    }

    /// <summary>
    /// Open-now map for search badges. Uses Eastern local time, weekly hours,
    /// day overrides, and OffersEmergency24x7.
    /// </summary>
    public async Task<Dictionary<int, bool>> TodayMapAsync(IEnumerable<int> groomerIds)
    {
        var ids = groomerIds.Distinct().ToList();
        var map = ids.ToDictionary(id => id, _ => false);
        if (ids.Count == 0) return map;

        var now = AppTimeZones.NowLocal();
        var today = now.Date;
        var dow = (int)today.DayOfWeek;
        var nowMinutes = now.Hour * 60 + now.Minute;

        var emergencyIds = await _db.Groomers.AsNoTracking()
            .Where(g => ids.Contains(g.Id) && g.OffersEmergency24x7)
            .Select(g => g.Id)
            .ToListAsync();
        foreach (var id in emergencyIds)
            map[id] = true;

        var dayRows = await _db.DayAvailabilities
            .AsNoTracking()
            .Where(a => ids.Contains(a.GroomerId) && a.Day == today)
            .ToListAsync();

        var weekRows = await _db.WeeklyHours
            .AsNoTracking()
            .Where(h => ids.Contains(h.GroomerId) && h.DayOfWeek == dow)
            .ToListAsync();

        foreach (var id in ids)
        {
            if (map[id]) continue; // 24/7

            var day = dayRows.FirstOrDefault(r => r.GroomerId == id);
            if (day != null && !day.IsAvailable)
            {
                map[id] = false;
                continue;
            }

            var week = weekRows.FirstOrDefault(r => r.GroomerId == id);
            if (week == null || !week.IsOpen)
            {
                map[id] = false;
                continue;
            }

            map[id] = IsWithinOpenWindow(nowMinutes, week.OpenMinutes, week.CloseMinutes);
        }

        return map;
    }

    /// <summary>True if current minutes fall in [open, close). Supports overnight and 24h (open==close).</summary>
    public static bool IsWithinOpenWindow(int nowMinutes, int openMinutes, int closeMinutes)
    {
        nowMinutes = ((nowMinutes % (24 * 60)) + (24 * 60)) % (24 * 60);
        openMinutes = Math.Clamp(openMinutes, 0, 24 * 60);
        closeMinutes = Math.Clamp(closeMinutes, 0, 24 * 60);

        if (openMinutes == closeMinutes)
            return true; // 24 horas ese día

        if (closeMinutes > openMinutes)
            return nowMinutes >= openMinutes && nowMinutes < closeMinutes;

        // Cruza medianoche (ej. 22:00–06:00)
        return nowMinutes >= openMinutes || nowMinutes < closeMinutes;
    }

    /// <summary>Guarda horario semanal y genera días abiertos/cerrados según esa regla.</summary>
    public async Task SaveWeeklyAndGenerateAsync(int groomerId, IEnumerable<WeekDayInput> week, int days = 60)
    {
        var inputs = week.ToList();
        var existing = await _db.WeeklyHours.Where(h => h.GroomerId == groomerId).ToListAsync();
        _db.WeeklyHours.RemoveRange(existing);

        foreach (var d in inputs)
        {
            _db.WeeklyHours.Add(new BusinessWeeklyHour
            {
                GroomerId = groomerId,
                DayOfWeek = d.DayOfWeek,
                IsOpen = d.IsOpen,
                OpenMinutes = WeekDayInput.ParseTimeToMinutes(d.OpenTime),
                CloseMinutes = WeekDayInput.ParseTimeToMinutes(d.CloseTime)
            });
        }
        await _db.SaveChangesAsync();
        await GenerateFromWeeklyAsync(groomerId, days, replaceExisting: true);
    }

    public async Task<int> GenerateFromWeeklyAsync(int groomerId, int days = 60, bool replaceExisting = true)
    {
        var weekly = await _db.WeeklyHours
            .AsNoTracking()
            .Where(h => h.GroomerId == groomerId)
            .ToListAsync();

        if (weekly.Count == 0)
            return 0;

        var openByDow = weekly.ToDictionary(h => h.DayOfWeek, h => h);
        var start = AppTimeZones.TodayLocalDate();
        var end = start.AddDays(days);

        var existing = await _db.DayAvailabilities
            .Where(a => a.GroomerId == groomerId && a.Day >= start && a.Day < end)
            .ToListAsync();

        var byDay = existing.ToDictionary(e => e.Day.Date);
        var touched = 0;

        for (var i = 0; i < days; i++)
        {
            var day = start.AddDays(i);
            var dow = (int)day.DayOfWeek;
            var open = openByDow.TryGetValue(dow, out var wh) && wh.IsOpen;
            var note = open
                ? (wh != null ? $"{wh.OpenLabel}–{wh.CloseLabel}" : "Abierto")
                : "Cerrado (horario semanal)";

            if (byDay.TryGetValue(day, out var row))
            {
                if (replaceExisting)
                {
                    row.IsAvailable = open;
                    row.Note = note;
                    touched++;
                }
            }
            else
            {
                _db.DayAvailabilities.Add(new BusinessDayAvailability
                {
                    GroomerId = groomerId,
                    Day = day,
                    IsAvailable = open,
                    Note = note
                });
                touched++;
            }
        }

        await _db.SaveChangesAsync();
        return touched;
    }

    /// <summary>Legacy: disponibilidad aleatoria (solo panel, si no hay horario semanal).</summary>
    public async Task<int> GenerateRandomAsync(int groomerId, int days = 30, bool keepExisting = true)
    {
        var start = AppTimeZones.TodayLocalDate();
        var existing = await _db.DayAvailabilities
            .Where(a => a.GroomerId == groomerId && a.Day >= start && a.Day < start.AddDays(days))
            .ToListAsync();

        var existingDays = existing.Select(e => e.Day.Date).ToHashSet();
        var added = 0;

        for (var i = 0; i < days; i++)
        {
            var day = start.AddDays(i);
            if (keepExisting && existingDays.Contains(day))
                continue;

            var row = existing.FirstOrDefault(e => e.Day.Date == day);
            var available = Rng.NextDouble() < 0.8;

            if (row == null)
            {
                _db.DayAvailabilities.Add(new BusinessDayAvailability
                {
                    GroomerId = groomerId,
                    Day = day,
                    IsAvailable = available,
                    Note = available ? null : "Generado: no disponible"
                });
                added++;
            }
            else if (!keepExisting)
            {
                row.IsAvailable = available;
                row.Note = available ? "Regenerado" : "Regenerado: no disponible";
            }
        }

        await _db.SaveChangesAsync();
        return added;
    }

    /// <summary>Seeds Mon–Sat 08:00–18:00 (Sun closed) for businesses without weekly hours.</summary>
    public async Task EnsureDefaultWeeklyHoursAsync(CancellationToken ct = default)
    {
        var missingIds = await _db.Groomers.AsNoTracking()
            .Where(g => g.IsActive && !g.OffersEmergency24x7)
            .Where(g => !_db.WeeklyHours.Any(h => h.GroomerId == g.Id))
            .Select(g => g.Id)
            .ToListAsync(ct);

        foreach (var id in missingIds)
        {
            for (var dow = 0; dow < 7; dow++)
            {
                _db.WeeklyHours.Add(new BusinessWeeklyHour
                {
                    GroomerId = id,
                    DayOfWeek = dow,
                    IsOpen = dow is >= 1 and <= 6,
                    OpenMinutes = 8 * 60,
                    CloseMinutes = 18 * 60
                });
            }
        }

        // 24/7 clinics: every day open all day
        var erIds = await _db.Groomers.AsNoTracking()
            .Where(g => g.OffersEmergency24x7)
            .Where(g => !_db.WeeklyHours.Any(h => h.GroomerId == g.Id))
            .Select(g => g.Id)
            .ToListAsync(ct);

        foreach (var id in erIds)
        {
            for (var dow = 0; dow < 7; dow++)
            {
                _db.WeeklyHours.Add(new BusinessWeeklyHour
                {
                    GroomerId = id,
                    DayOfWeek = dow,
                    IsOpen = true,
                    OpenMinutes = 0,
                    CloseMinutes = 0 // mismo valor = 24h
                });
            }
        }

        if (missingIds.Count > 0 || erIds.Count > 0)
            await _db.SaveChangesAsync(ct);

        // Weekly hours alone do not open booking days — generate the rolling agenda when missing.
        await EnsureUpcomingDayAgendaAsync(ct);
    }

    /// <summary>
    /// For each active business with weekly hours but no day rows in the next 30 days,
    /// generate 60 days from the weekly template (does not overwrite existing day overrides).
    /// </summary>
    public async Task EnsureUpcomingDayAgendaAsync(CancellationToken ct = default)
    {
        var start = AppTimeZones.TodayLocalDate();
        var end = start.AddDays(30);

        var needy = await _db.Groomers.AsNoTracking()
            .Where(g => g.IsActive)
            .Where(g => _db.WeeklyHours.Any(h => h.GroomerId == g.Id))
            .Where(g => !_db.DayAvailabilities.Any(d => d.GroomerId == g.Id && d.Day >= start && d.Day < end))
            .Select(g => g.Id)
            .ToListAsync(ct);

        foreach (var id in needy)
            await GenerateFromWeeklyAsync(id, days: 60, replaceExisting: false);
    }

    /// <summary>Generate agenda if this business has weekly hours but no upcoming day rows.</summary>
    public async Task EnsureUpcomingDayAgendaForAsync(int groomerId, CancellationToken ct = default)
    {
        var start = AppTimeZones.TodayLocalDate();
        var end = start.AddDays(30);
        var hasWeek = await _db.WeeklyHours.AsNoTracking().AnyAsync(h => h.GroomerId == groomerId, ct);
        if (!hasWeek) return;
        var hasDays = await _db.DayAvailabilities.AsNoTracking()
            .AnyAsync(d => d.GroomerId == groomerId && d.Day >= start && d.Day < end, ct);
        if (hasDays) return;
        await GenerateFromWeeklyAsync(groomerId, days: 60, replaceExisting: false);
    }
}
