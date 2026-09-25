using WebAppPet.Application.Bookings.CancelBooking;
using WebAppPet.Application.Bookings.CreateBooking;
using WebAppPet.Application.Bookings.GetBookings;
using WebAppPet.Application.Bookings.SaveClinicalNote;
using WebAppPet.Application.Bookings.Shared;
using WebAppPet.Application.Bookings.UpdateBookingStatus;
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

        services.AddScoped<ClinicalNotes>();
        services.AddScoped<CreateBookingHandler>();
        services.AddScoped<CancelBookingHandler>();
        services.AddScoped<UpdateBookingStatusHandler>();
        services.AddScoped<SaveClinicalNoteHandler>();
        services.AddScoped<GetClientBookingsHandler>();
        services.AddScoped<GetBusinessBookingsHandler>();

        services.AddScoped<RatingCalculator>();
        services.AddScoped<CanReviewHandler>();
        services.AddScoped<CreateReviewHandler>();
        services.AddScoped<GetReviewsHandler>();

        return services;
    }
}
