namespace WebAppPet.Application.Bookings.CancelBooking;

public sealed record CancelBookingCommand(int ClientId, int AppointmentId);
