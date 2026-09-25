namespace WebAppPet.Application.Payments.DeletePaymentMethod;

public sealed record DeletePaymentMethodCommand(int UserId, int PaymentMethodId);
