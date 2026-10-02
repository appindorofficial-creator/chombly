namespace WebAppPet.Application.Bookings.GetOccupiedSlots;

/// <param name="Day">Local calendar day in the market time zone.</param>
/// <param name="Slots">Wall-clock labels to check (e.g. "10:00 AM").</param>
public sealed record GetOccupiedSlotsQuery(int BusinessId, DateTime Day, IReadOnlyList<string> Slots);
