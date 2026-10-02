namespace WebAppPet.Application.Businesses.MarkVerificationItem;

/// <param name="Item">"identity", "license", "insurance" or "bank".</param>
public sealed record MarkVerificationItemCommand(int BusinessId, string? Item);
