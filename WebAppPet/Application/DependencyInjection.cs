using WebAppPet.Application.Bookings.CreateBooking;
using WebAppPet.Application.Promotions.ApplyPromoCode;
using WebAppPet.Application.Reviews.CanReview;
using WebAppPet.Application.Reviews.CreateReview;
using WebAppPet.Application.Reviews.GetReviews;
using WebAppPet.Application.Reviews.Shared;

namespace WebAppPet.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ApplyPromoCodeHandler>();

        services.AddScoped<CreateBookingHandler>();

        services.AddScoped<RatingCalculator>();
        services.AddScoped<CanReviewHandler>();
        services.AddScoped<CreateReviewHandler>();
        services.AddScoped<GetReviewsHandler>();

        return services;
    }
}
