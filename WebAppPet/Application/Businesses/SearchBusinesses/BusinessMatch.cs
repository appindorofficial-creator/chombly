using WebAppPet.Models;

namespace WebAppPet.Application.Businesses.SearchBusinesses;

/// <param name="OpenNow">Open at this moment (card badge), not the <c>OpenOn</c> day filter.</param>
public sealed record BusinessMatch(GroomerProfile Business, double? Miles, string? DistanceLabel, bool OpenNow);
