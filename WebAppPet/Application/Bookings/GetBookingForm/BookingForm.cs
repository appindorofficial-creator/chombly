using WebAppPet.Domain;

namespace WebAppPet.Application.Bookings.GetBookingForm;

/// <param name="IsOvernight">Booked by check-in/check-out nights instead of a time slot.</param>
/// <param name="Payments">Default card first.</param>
public sealed record BookingForm(
    GroomerProfile Business,
    bool IsOvernight,
    List<GroomerService> Services,
    List<ServiceExtra> Extras,
    List<Pet> Pets,
    List<PaymentMethod> Payments);
