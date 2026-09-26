using WebAppPet.Application.Accounts.Login;
using WebAppPet.Application.Accounts.Register;
using WebAppPet.Application.Accounts.UpdateProfile;
using WebAppPet.Application.Bookings.CancelBooking;
using WebAppPet.Application.Bookings.CreateBooking;
using WebAppPet.Application.Bookings.GetBookings;
using WebAppPet.Application.Bookings.SaveClinicalNote;
using WebAppPet.Application.Bookings.Shared;
using WebAppPet.Application.Bookings.UpdateBookingStatus;
using WebAppPet.Application.Businesses.AddAmenity;
using WebAppPet.Application.Businesses.AddExtra;
using WebAppPet.Application.Businesses.AddService;
using WebAppPet.Application.Businesses.ApproveBusiness;
using WebAppPet.Application.Businesses.CreateBusiness;
using WebAppPet.Application.Businesses.GetAvailability;
using WebAppPet.Application.Businesses.GetPendingBusinesses;
using WebAppPet.Application.Businesses.RejectBusiness;
using WebAppPet.Application.Businesses.SaveWeeklySchedule;
using WebAppPet.Application.Businesses.SearchBusinesses;
using WebAppPet.Application.Businesses.Shared;
using WebAppPet.Application.Businesses.ToggleAvailabilityDay;
using WebAppPet.Application.Businesses.UpdateBusiness;
using WebAppPet.Application.Payments.AddPaymentMethod;
using WebAppPet.Application.Payments.DeletePaymentMethod;
using WebAppPet.Application.Payments.GeneratePayout;
using WebAppPet.Application.Payments.GetAdminPayouts;
using WebAppPet.Application.Payments.GetPaymentMethods;
using WebAppPet.Application.Payments.GetProviderPayouts;
using WebAppPet.Application.Payments.MarkPayoutPaid;
using WebAppPet.Application.Payments.Shared;
using WebAppPet.Application.Pets.DeletePet;
using WebAppPet.Application.Pets.GetPet;
using WebAppPet.Application.Pets.GetPetHistory;
using WebAppPet.Application.Pets.GetPets;
using WebAppPet.Application.Pets.SavePet;
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
        services.AddScoped<LoginHandler>();
        services.AddScoped<RegisterHandler>();
        services.AddScoped<UpdateProfileHandler>();

        services.AddScoped<ApplyPromoCodeHandler>();

        services.AddScoped<ClinicalNotes>();
        services.AddScoped<CreateBookingHandler>();
        services.AddScoped<CancelBookingHandler>();
        services.AddScoped<UpdateBookingStatusHandler>();
        services.AddScoped<SaveClinicalNoteHandler>();
        services.AddScoped<GetClientBookingsHandler>();
        services.AddScoped<GetBusinessBookingsHandler>();

        services.AddScoped<AvailabilityService>();
        services.AddScoped<ApproveBusinessHandler>();
        services.AddScoped<RejectBusinessHandler>();
        services.AddScoped<GetPendingBusinessesHandler>();
        services.AddScoped<CreateBusinessHandler>();
        services.AddScoped<UpdateBusinessHandler>();
        services.AddScoped<AddAmenityHandler>();
        services.AddScoped<AddExtraHandler>();
        services.AddScoped<AddServiceHandler>();
        services.AddScoped<GetAvailabilityHandler>();
        services.AddScoped<SaveWeeklyScheduleHandler>();
        services.AddScoped<ToggleAvailabilityDayHandler>();
        services.AddScoped<SearchBusinessesHandler>();

        services.AddScoped<GetPaymentMethodsHandler>();
        services.AddScoped<AddPaymentMethodHandler>();
        services.AddScoped<DeletePaymentMethodHandler>();
        services.AddScoped<ProviderPayoutService>();
        services.AddScoped<GeneratePayoutHandler>();
        services.AddScoped<MarkPayoutPaidHandler>();
        services.AddScoped<GetProviderPayoutsHandler>();
        services.AddScoped<GetAdminPayoutsHandler>();

        services.AddScoped<GetPetsHandler>();
        services.AddScoped<GetPetHandler>();
        services.AddScoped<GetPetHistoryHandler>();
        services.AddScoped<SavePetHandler>();
        services.AddScoped<DeletePetHandler>();

        services.AddScoped<RatingCalculator>();
        services.AddScoped<CanReviewHandler>();
        services.AddScoped<CreateReviewHandler>();
        services.AddScoped<GetReviewsHandler>();

        return services;
    }
}
