namespace WebAppPet.Application.Reviews.CanReview;

public sealed record CanReviewQuery(int ClientId, int GroomerId, int? AppointmentId = null);
