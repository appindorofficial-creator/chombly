namespace WebAppPet.Application.Bookings.UpdateBookingStatus;

public enum BookingStatusAction
{
    Accept,
    Reject,
    Complete
}

public sealed record UpdateBookingStatusCommand(
    int BusinessId,
    int AppointmentId,
    BookingStatusAction Action,
    string? ClinicalNotes = null);
