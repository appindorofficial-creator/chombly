namespace WebAppPet.Application.Reviews.GetReviews;

public sealed record GetReviewsQuery(int GroomerId, int Take = 20);
