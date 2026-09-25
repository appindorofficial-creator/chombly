namespace WebAppPet.Application.Promotions.ApplyPromoCode;

public sealed record ApplyPromoCodeCommand(int? UserId, string? Code, decimal Subtotal);
