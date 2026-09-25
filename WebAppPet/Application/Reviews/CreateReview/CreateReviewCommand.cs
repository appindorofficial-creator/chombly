namespace WebAppPet.Application.Reviews.CreateReview;

public sealed record CreateReviewCommand(
    int ClientId,
    int GroomerId,
    int Rating,
    string? Comment,
    int? AppointmentId = null);
