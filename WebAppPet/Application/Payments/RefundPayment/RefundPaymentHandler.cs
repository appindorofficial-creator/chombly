using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Common;
using WebAppPet.Application.Payments.Shared;
using WebAppPet.Data;
using WebAppPet.Localization;

namespace WebAppPet.Application.Payments.RefundPayment;

/// <summary>Admin refund of a single charge. Only the money moves; the booking it paid for is left as is.</summary>
public class RefundPaymentHandler
{
    private readonly AppDbContext _db;
    private readonly PaymentService _payments;

    public RefundPaymentHandler(AppDbContext db, PaymentService payments)
    {
        _db = db;
        _payments = payments;
    }

    public async Task<Result> HandleAsync(RefundPaymentCommand command, CancellationToken ct = default)
    {
        var transaction = await _db.PaymentTransactions.FirstOrDefaultAsync(t => t.Id == command.TransactionId, ct);
        if (transaction is null)
            return Result.Fail(CatalogLocalizer.Loc("No encontramos ese pago.", "Payment not found."));

        var reason = string.IsNullOrWhiteSpace(command.Reason) ? "admin_refund" : command.Reason.Trim();
        return await _payments.RefundAsync(transaction, reason, command.ActorUserId, ct)
            ? Result.Ok()
            : Result.Fail(CatalogLocalizer.Loc(
                "Este pago no se puede reembolsar (fallido o ya reembolsado).",
                "This payment can't be refunded (failed or already refunded)."));
    }
}
