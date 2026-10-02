namespace WebAppPet.Application.Bookings.GetBookingForm;

/// <param name="ClientId">Signed-in client; null loads no pets or payment methods.</param>
public sealed record GetBookingFormQuery(int BusinessId, int? ClientId);
