namespace WebAppPet.Application.Payments.MarkPayoutPaid;

public sealed record MarkPayoutPaidCommand(int PayoutId, int? AdminUserId);
