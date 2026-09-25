namespace WebAppPet.Application.Bookings.GetBookings;

/// <param name="NowUtc">Current instant; appointments before it count as past.</param>
/// <param name="TodayStartUtc">Midnight of the client's local day (market time zone) expressed in UTC.</param>
public sealed record GetClientBookingsQuery(int ClientId, bool History, DateTime NowUtc, DateTime TodayStartUtc);
