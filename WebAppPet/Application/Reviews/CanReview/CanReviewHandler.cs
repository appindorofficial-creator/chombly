using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;

namespace WebAppPet.Application.Reviews.CanReview;

/// <summary>
/// Eligible when the client has not reviewed the business yet and has a completed visit,
/// or a confirmed visit whose start time already passed.
/// </summary>
public class CanReviewHandler
{
    private readonly AppDbContext _db;

    public CanReviewHandler(AppDbContext db) => _db = db;

    public async Task<CanReviewResult> HandleAsync(CanReviewQuery query, CancellationToken ct = default)
    {
        var alreadyReviewed = await _db.Reviews.AsNoTracking()
            .AnyAsync(r => r.ClientId == query.ClientId && r.GroomerId == query.GroomerId, ct);
        if (alreadyReviewed)
            return new CanReviewResult(CanReview: false, AlreadyReviewed: true);

        var now = DateTime.UtcNow;
        var q = _db.Appointments.AsNoTracking()
            .Where(a => a.ClientId == query.ClientId && a.GroomerId == query.GroomerId);

        if (query.AppointmentId is int aid)
            q = q.Where(a => a.Id == aid);

        var eligible = await q.AnyAsync(a =>
            a.Status == AppointmentStatus.Completed
            || (a.Status == AppointmentStatus.Confirmed && a.ScheduledAt <= now), ct);

        return new CanReviewResult(CanReview: eligible, AlreadyReviewed: false);
    }
}
