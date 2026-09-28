using Microsoft.EntityFrameworkCore;
using WebAppPet.Infrastructure.Persistence;

namespace WebAppPet.Application.Payments.DeletePaymentMethod;

/// <summary>
/// Removes one of the user's cards. Cards of other users are ignored. When the default card is removed,
/// the most recently added remaining card becomes the default.
/// </summary>
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
        if (card.IsDefault)
        {
            var next = await _db.PaymentMethods
                .Where(p => p.UserId == command.UserId && p.Id != card.Id)
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync(ct);
            if (next is not null)
                next.IsDefault = true;
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }
}
