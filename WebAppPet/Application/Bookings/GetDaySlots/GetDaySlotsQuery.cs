namespace WebAppPet.Application.Bookings.GetDaySlots;

/// <param name="OpenAllDay">24/7 businesses: always open, weekly hours ignored.</param>
/// <param name="Day">Local calendar day in the market time zone.</param>
public sealed record GetDaySlotsQuery(int BusinessId, bool OpenAllDay, DateTime Day);
