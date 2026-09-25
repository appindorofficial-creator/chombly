using WebAppPet.Models;

namespace WebAppPet.Application.Businesses.GetAvailability;

public sealed record GetAvailabilityQuery(int BusinessId);

/// <param name="Week">Seven days, Sunday first. Days without saved hours use the default week.</param>
/// <param name="Days">Agenda for the next 30 days, starting today in the market's time zone.</param>
public sealed record AvailabilityView(List<WeekDayInput> Week, List<BusinessDayAvailability> Days);
