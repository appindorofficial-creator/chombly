using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;

namespace WebAppPet.Application.Payments.DeletePaymentMethod;

/// <summary>Removes one of the user's cards. Cards of other users are ignored.</summary>
public class DeletePaymentMethodHandler
{
    private readonly AppDbContext _db;

    public DeletePaymentMethodHandler(AppDbContext db) => _db = db;

    /// <returns>True when the card was found and removed.</returns>
    public async Task<bool> HandleAsync(DeletePaymentMethodCommand command, CancellationToken ct = default)
    {
        var card = await _db.PaymentMethods
            .FirstOrDefaultAsync(p => p.Id == command.PaymentMethodId && p.UserId == command.UserId, ct);
        if (card is null)
            return false;

        _db.PaymentMethods.Remove(card);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
