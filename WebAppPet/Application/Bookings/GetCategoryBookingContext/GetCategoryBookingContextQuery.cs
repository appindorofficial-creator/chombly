namespace WebAppPet.Application.Bookings.GetCategoryBookingContext;

/// <param name="ClientId">Signed-in client; null loads no location, pets or payment methods.</param>
public sealed record GetCategoryBookingContextQuery(string CategorySlug, int? ClientId);
