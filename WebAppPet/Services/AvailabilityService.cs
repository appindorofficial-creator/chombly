using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;

namespace WebAppPet.Services;

public class AvailabilityService
{
    private readonly AppDbContext _db;
    private static readonly Random Rng = new();

    public AvailabilityService(AppDbContext db) => _db = db;

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

        // Legacy sin agenda configurada
        return true;
    }

    public async Task<Dictionary<int, bool>> TodayMapAsync(IEnumerable<int> groomerIds)
    {
        var ids = groomerIds.Distinct().ToList();
        var today = DateTime.Today;
        var dow = (int)today.DayOfWeek;

        var dayRows = await _db.DayAvailabilities
            .AsNoTracking()
            .Where(a => ids.Contains(a.GroomerId) && a.Day == today)
            .ToListAsync();

        var weekRows = await _db.WeeklyHours
            .AsNoTracking()
            .Where(h => ids.Contains(h.GroomerId) && h.DayOfWeek == dow)
            .ToListAsync();

        var map = ids.ToDictionary(id => id, _ => true);
        foreach (var id in ids)
        {
            var day = dayRows.FirstOrDefault(r => r.GroomerId == id);
            if (day != null)
            {
                map[id] = day.IsAvailable;
                continue;
            }
            var week = weekRows.FirstOrDefault(r => r.GroomerId == id);
            if (week != null)
                map[id] = week.IsOpen;
        }
        return map;
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
        var start = DateTime.Today;
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
        var start = DateTime.Today;
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
}
