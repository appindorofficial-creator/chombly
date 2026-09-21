using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;

namespace WebAppPet.Services;

/// <summary>Shared client reviews for any marketplace category (grooming, hotel, walkers, …).</summary>
public class ReviewService
{
    private readonly AppDbContext _db;

    public ReviewService(AppDbContext db) => _db = db;

    public async Task<bool> HasReviewedAsync(int clientId, int groomerId) =>
        await _db.Reviews.AsNoTracking()
            .AnyAsync(r => r.ClientId == clientId && r.GroomerId == groomerId);

    /// <summary>
    /// Eligible when the client has a completed visit, or a confirmed visit whose start time already passed.
    /// </summary>
    public async Task<bool> CanReviewAsync(int clientId, int groomerId, int? appointmentId = null)
    {
        if (await HasReviewedAsync(clientId, groomerId))
            return false;

        var now = DateTime.UtcNow;
        var q = _db.Appointments.AsNoTracking()
            .Where(a => a.ClientId == clientId && a.GroomerId == groomerId);

        if (appointmentId is int aid)
            q = q.Where(a => a.Id == aid);

        return await q.AnyAsync(a =>
            a.Status == AppointmentStatus.Completed
            || (a.Status == AppointmentStatus.Confirmed && a.ScheduledAt <= now));
    }

    public async Task<List<Review>> ListForGroomerAsync(int groomerId, int take = 20) =>
        await _db.Reviews.AsNoTracking()
            .Include(r => r.Client)
            .Where(r => r.GroomerId == groomerId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(take)
            .ToListAsync();

    public async Task<(bool Ok, string? Error)> SubmitAsync(
        int clientId,
        int groomerId,
        int rating,
        string? comment,
        int? appointmentId = null)
    {
        if (rating is < 1 or > 5)
            return (false, CatalogLocalizer.Loc("Elige una calificación de 1 a 5.", "Choose a rating from 1 to 5."));

        if (!await CanReviewAsync(clientId, groomerId, appointmentId))
        {
            return (false, CatalogLocalizer.Loc(
                "Solo puedes valorar después de una visita confirmada o completada.",
                "You can only review after a confirmed or completed visit."));
        }

        if (await HasReviewedAsync(clientId, groomerId))
        {
            return (false, CatalogLocalizer.Loc(
                "Ya valoraste este negocio.",
                "You already reviewed this business."));
        }

        var text = (comment ?? "").Trim();
        if (text.Length > 600)
            text = text[..600];

        if (appointmentId is int aid)
        {
            var appt = await _db.Appointments
                .FirstOrDefaultAsync(a => a.Id == aid && a.ClientId == clientId && a.GroomerId == groomerId);
            if (appt != null && appt.Status == AppointmentStatus.Confirmed && appt.ScheduledAt <= DateTime.UtcNow)
                appt.Status = AppointmentStatus.Completed;
        }

        _db.Reviews.Add(new Review
        {
            ClientId = clientId,
            GroomerId = groomerId,
            Rating = rating,
            Comment = text,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        await RecalculateAsync(groomerId);
        return (true, null);
    }

    public async Task RecalculateAsync(int groomerId)
    {
        var g = await _db.Groomers.FirstOrDefaultAsync(x => x.Id == groomerId);
        if (g == null) return;

        var stats = await _db.Reviews.AsNoTracking()
            .Where(r => r.GroomerId == groomerId)
            .GroupBy(_ => 1)
            .Select(grp => new { Count = grp.Count(), Avg = grp.Average(r => (double)r.Rating) })
            .FirstOrDefaultAsync();

        if (stats == null || stats.Count == 0)
        {
            g.ReviewCount = 0;
            g.Rating = 0;
        }
        else
        {
            g.ReviewCount = stats.Count;
            g.Rating = Math.Round(stats.Avg, 1);
        }

        await _db.SaveChangesAsync();
    }
}
