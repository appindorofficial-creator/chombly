namespace WebAppPet.Application.Bookings.FindNextFreeStart;

/// <param name="Day">Local calendar day to start from.</param>
/// <param name="PreferredSlot">On <paramref name="Day"/>, earlier slots are skipped; later days try every slot.</param>
/// <param name="Slots">Wall-clock labels in display order.</param>
public sealed record FindNextFreeStartQuery(
    int BusinessId,
    DateTime Day,
    string? PreferredSlot,
    IReadOnlyList<string> Slots,
    DateTime NowUtc);

/// <param name="Day">Local calendar day of the start.</param>
public sealed record FreeStart(DateTime StartUtc, DateTime Day, string Slot);
