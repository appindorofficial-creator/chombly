namespace WebAppPet.Application.Businesses.EnsureHotelCoreExtras;

public sealed record EnsureHotelCoreExtrasCommand(IReadOnlyCollection<int> HotelIds);
