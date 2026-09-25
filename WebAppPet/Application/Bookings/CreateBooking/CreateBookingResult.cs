using WebAppPet.Application.Bookings.Shared;

namespace WebAppPet.Application.Bookings.CreateBooking;

public enum CreateBookingError
{
    None,
    MissingData,
    SpeciesNotAccepted,
    InvalidPromo
}

public sealed record CreateBookingResult
{
    public int? AppointmentId { get; init; }
    public CreateBookingError Error { get; init; }
    public string? RejectedSpecies { get; init; }
    public string? PromoError { get; init; }
    public BookingQuote? Quote { get; init; }

    public bool Success => Error == CreateBookingError.None;

    public static CreateBookingResult Created(int appointmentId, BookingQuote quote) =>
        new() { AppointmentId = appointmentId, Quote = quote };

    public static CreateBookingResult Fail(CreateBookingError error) => new() { Error = error };
}
