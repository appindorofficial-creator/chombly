using WebAppPet.Application.Businesses.Shared;
using WebAppPet.Models;

namespace WebAppPet.Application.Businesses.CreateBusiness;

/// <summary>Everything the registration wizard collected, already validated step by step.</summary>
public sealed record CreateBusinessCommand
{
    /// <summary>Signed-in client converting to a business, or null for a new account.</summary>
    public int? CurrentUserId { get; init; }

    public required string ProviderKindKey { get; init; }
    public required string FullName { get; init; }
    public required string BusinessName { get; init; }
    public required string Phone { get; init; }
    public required string Email { get; init; }
    public required string City { get; init; }
    public string? Address { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public required int PrimaryCategoryId { get; init; }
    public IReadOnlyList<int> CategoryIds { get; init; } = [];

    /// <summary>"local", "domicilio" or "ambas".</summary>
    public string WorkModeKey { get; init; } = "local";
    public IReadOnlyList<WeekDayInput> Week { get; init; } = [];

    /// <summary>Radius picked in local units (km in Colombia, miles elsewhere). 0 means the whole city.</summary>
    public int ServiceArea { get; init; }
    public required string About { get; init; }
    public string? LogoUrl { get; init; }
    public string? CoverUrl { get; init; }
    public IReadOnlyList<string?> ServiceNames { get; init; } = [];
    public IReadOnlyList<decimal> ServicePrices { get; init; } = [];

    /// <summary>Only used for new accounts.</summary>
    public string? Password { get; init; }
    public required HotelExtraPrices HotelExtras { get; init; }
}

public sealed record CreateBusinessResult(RegistrationError Error, AppUser? User, int? BusinessId)
{
    public bool Success => Error == RegistrationError.None;

    public static CreateBusinessResult Fail(RegistrationError error) => new(error, null, null);
}
