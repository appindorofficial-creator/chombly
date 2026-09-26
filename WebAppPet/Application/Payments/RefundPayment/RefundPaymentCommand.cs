namespace WebAppPet.Application.Payments.RefundPayment;

public sealed record RefundPaymentCommand(int TransactionId, int? ActorUserId, string Reason);
