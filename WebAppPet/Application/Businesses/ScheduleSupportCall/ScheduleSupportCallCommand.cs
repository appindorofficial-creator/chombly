namespace WebAppPet.Application.Businesses.ScheduleSupportCall;

/// <param name="Day">The chosen day as yyyy-MM-dd.</param>
/// <param name="Slot">The chosen time, e.g. "10:00 AM".</param>
public sealed record ScheduleSupportCallCommand(int BusinessId, string? Day, string? Slot);
