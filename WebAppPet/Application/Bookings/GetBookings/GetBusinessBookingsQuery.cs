namespace WebAppPet.Application.Bookings.GetBookings;

public enum BusinessBookingsTab
{
    Requests,
    Upcoming,
    History
}

public sealed record GetBusinessBookingsQuery(int BusinessId, BusinessBookingsTab Tab);
