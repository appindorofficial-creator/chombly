namespace WebAppPet.Application.Bookings.GetActiveExtras;

public sealed record GetActiveExtrasQuery(IReadOnlyCollection<int> BusinessIds);
