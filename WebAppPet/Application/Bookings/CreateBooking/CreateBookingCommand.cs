using WebAppPet.Application.Bookings.Shared;

namespace WebAppPet.Application.Bookings.CreateBooking;

public sealed record BookingExtraLine(int ServiceExtraId, string Name, decimal Price);

public sealed record BookingNotice(string Title, string Message);

public sealed record CreateBookingCommand
{
    public required int ClientId { get; init; }
    public required int BusinessId { get; init; }
    public required int ServiceId { get; init; }

    /// <summary>The first pet is stored as the appointment's primary pet.</summary>
    public required IReadOnlyList<int> PetIds { get; init; }

    public required DateTime StartUtc { get; init; }
    public DateTime? EndUtc { get; init; }
    public int Nights { get; init; }

    /// <summary>Price before promotion, including extras.</summary>
    public required decimal Subtotal { get; init; }
    public string? PromoCode { get; init; }
    public decimal MinimumDeposit { get; init; } = BookingPricing.MinimumDeposit;

    /// <summary>Card charged for the deposit; the client's default card when null.</summary>
    public int? PaymentMethodId { get; init; }

    public IReadOnlyList<BookingExtraLine> Extras { get; init; } = [];
    public IReadOnlyList<string> NoteParts { get; init; } = [];

    /// <summary>Extra restriction on top of the business's accepted species (walks are dogs only).</summary>
    public IReadOnlyCollection<string>? AllowedSpecies { get; init; }

    public required BookingNotice ClientNotice { get; init; }
    public required BookingNotice BusinessNotice { get; init; }
}
