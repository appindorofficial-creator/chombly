namespace WebAppPet.Application.Businesses.UpdateBusiness;

/// <summary>Prices for the extras every hotel manages from its profile. Ignored for other categories.</summary>
public sealed record HotelExtraPrices(decimal BathPrice, decimal MedsPrice, bool OffersPrivateCamera, decimal PrivateCameraPrice);

public sealed record UpdateBusinessCommand
{
    public required int BusinessId { get; init; }
    public required string BusinessName { get; init; }

    /// <summary>Preferred primary category. Falls back to the first selected one by sort order.</summary>
    public int? CategoryId { get; init; }
    public IReadOnlyList<int> CategoryIds { get; init; } = [];
    public string Address { get; init; } = "";
    public string City { get; init; } = "";
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string About { get; init; } = "";
    public string? Phone { get; init; }
    public string? ImageUrl { get; init; }
    public string PriceUnit { get; init; } = "";
    public decimal StartingPrice { get; init; }
    public string? AcceptedSpecies { get; init; }
    public bool AcceptsSeniorPets { get; init; }
    public bool AcceptsAnxiousPets { get; init; }
    public bool IsFeatured { get; init; }
    public bool IsActive { get; init; }
    public required HotelExtraPrices HotelExtras { get; init; }
}

public enum UpdateBusinessError
{
    None,
    NotFound,
    NoCategory,
    PhoneInvalid
}
