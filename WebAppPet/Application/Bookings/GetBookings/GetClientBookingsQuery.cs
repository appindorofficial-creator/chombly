namespace WebAppPet.Application.Bookings.GetBookings;

/// <param name="Now">Reference time compared against ScheduledAt.</param>
public sealed record GetClientBookingsQuery(int ClientId, bool History, DateTime Now);
