using WebAppPet.Application.Accounts.Login;
using WebAppPet.Application.Accounts.Register;
using WebAppPet.Application.Accounts.UpdateProfile;
using WebAppPet.Application.Bookings.CancelBooking;
using WebAppPet.Application.Bookings.CreateBooking;
using WebAppPet.Application.Bookings.GetBookings;
using WebAppPet.Application.Bookings.SaveClinicalNote;
using WebAppPet.Application.Bookings.Shared;
using WebAppPet.Application.Bookings.UpdateBookingStatus;
using WebAppPet.Application.Consultations.BookConsultation;
using WebAppPet.Application.Consultations.CheckEligibility;
using WebAppPet.Application.Consultations.ChooseConsultationService;
using WebAppPet.Application.Consultations.ChooseEmergency;
using WebAppPet.Application.Consultations.ContinueVirtual;
using WebAppPet.Application.Consultations.EscalateToEmergency;
using WebAppPet.Application.Consultations.FindIntlVet;
using WebAppPet.Application.Consultations.GetConsultationCheckout;
using WebAppPet.Application.Consultations.GetConsultationServices;
using WebAppPet.Application.Consultations.GetConsultationSummary;
using WebAppPet.Application.Consultations.GetIntlHome;
using WebAppPet.Application.Consultations.GetIntlMatches;
using WebAppPet.Application.Consultations.GetIntlSchedule;
using WebAppPet.Application.Consultations.GetLocalVets;
using WebAppPet.Application.Consultations.PrepareScreening;
using WebAppPet.Application.Consultations.ScheduleIntlConsultation;
using WebAppPet.Application.Consultations.ScreenConsultation;
using WebAppPet.Application.Consultations.SelectIntlVet;
using WebAppPet.Application.Consultations.SelectLocalVet;
using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Application.Consultations.StartConsultation;
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

        services.AddScoped<ClientHomeCountry>();
        services.AddScoped<ConsultationRouter>();
        services.AddScoped<StartConsultationHandler>();
        services.AddScoped<PrepareScreeningHandler>();
        services.AddScoped<ScreenConsultationHandler>();
        services.AddScoped<ContinueVirtualHandler>();
        services.AddScoped<ChooseEmergencyHandler>();
        services.AddScoped<GetConsultationServicesHandler>();
        services.AddScoped<ChooseConsultationServiceHandler>();
        services.AddScoped<CheckEligibilityHandler>();
        services.AddScoped<GetLocalVetsHandler>();
        services.AddScoped<SelectLocalVetHandler>();
        services.AddScoped<GetConsultationCheckoutHandler>();
        services.AddScoped<BookConsultationHandler>();
        services.AddScoped<GetConsultationSummaryHandler>();
        services.AddScoped<EscalateToEmergencyHandler>();
        services.AddScoped<GetIntlHomeHandler>();
        services.AddScoped<FindIntlVetHandler>();
        services.AddScoped<GetIntlMatchesHandler>();
        services.AddScoped<SelectIntlVetHandler>();
        services.AddScoped<GetIntlScheduleHandler>();
        services.AddScoped<ScheduleIntlConsultationHandler>();

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

        services.AddScoped<RatingCalculator>();
        services.AddScoped<CanReviewHandler>();
        services.AddScoped<CreateReviewHandler>();
        services.AddScoped<GetReviewsHandler>();

        return services;
    }
}
