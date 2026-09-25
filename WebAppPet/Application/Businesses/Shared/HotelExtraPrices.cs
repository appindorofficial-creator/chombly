namespace WebAppPet.Application.Businesses.Shared;

/// <summary>Prices for the extras every hotel manages from its profile. Ignored for other categories.</summary>
public sealed record HotelExtraPrices(decimal BathPrice, decimal MedsPrice, bool OffersPrivateCamera, decimal PrivateCameraPrice);
