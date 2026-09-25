using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Common;
using WebAppPet.Application.Reviews.CanReview;
using WebAppPet.Application.Reviews.Shared;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;

namespace WebAppPet.Application.Reviews.CreateReview;

public class CreateReviewHandler
{
    public const int MaxCommentLength = 600;

    private readonly AppDbContext _db;
    private readonly CanReviewHandler _canReview;
    private readonly RatingCalculator _rating;

    public CreateReviewHandler(AppDbContext db, CanReviewHandler canReview, RatingCalculator rating)
    {
        _db = db;
        _canReview = canReview;
        _rating = rating;
    }

    public async Task<Result> HandleAsync(CreateReviewCommand command, CancellationToken ct = default)
    {
        if (command.Rating is < 1 or > 5)
            return Result.Fail(CatalogLocalizer.Loc("Elige una calificación de 1 a 5.", "Choose a rating from 1 to 5."));

        var eligibility = await _canReview.HandleAsync(
            new CanReviewQuery(command.ClientId, command.GroomerId, command.AppointmentId), ct);
        if (!eligibility.CanReview)
        {
            return Result.Fail(CatalogLocalizer.Loc(
                "Solo puedes valorar después de una visita confirmada o completada.",
                "You can only review after a confirmed or completed visit."));
        }

        var text = (command.Comment ?? "").Trim();
        if (text.Length > MaxCommentLength)
            text = text[..MaxCommentLength];

        if (command.AppointmentId is int aid)
        {
            var appt = await _db.Appointments.FirstOrDefaultAsync(
                a => a.Id == aid && a.ClientId == command.ClientId && a.GroomerId == command.GroomerId, ct);
            if (appt != null && appt.Status == AppointmentStatus.Confirmed && appt.ScheduledAt <= DateTime.UtcNow)
                appt.Status = AppointmentStatus.Completed;
        }

        _db.Reviews.Add(new Review
        {
            ClientId = command.ClientId,
            GroomerId = command.GroomerId,
            Rating = command.Rating,
            Comment = text,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        await _rating.RecalculateAsync(command.GroomerId, ct);
        return Result.Ok();
    }
}
