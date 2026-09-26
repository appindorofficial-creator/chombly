using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Payments.Shared;

public sealed record ChargeRequest
{
    public required int UserId { get; init; }
    public required PaymentMethod Card { get; init; }
    public required decimal Amount { get; init; }

    /// <summary>Full service price used for commission; defaults to <see cref="Amount"/>.</summary>
    public decimal? ServiceTotal { get; init; }

    public required PaymentPurpose Purpose { get; init; }
    public required string Description { get; init; }
    public int? ProviderId { get; init; }
    public int? ConsultationId { get; init; }
    public int? BehaviorCaseId { get; init; }
    public int? CareSubscriptionId { get; init; }
}

/// <summary>
/// Charges and refunds cards through <see cref="IPaymentGateway"/> and records every attempt,
/// declined ones included, as a <see cref="PaymentTransaction"/>.
/// </summary>
public class PaymentService
{
    private readonly AppDbContext _db;
    private readonly IPaymentGateway _gateway;
    private readonly VetAuditService _audit;

    public PaymentService(AppDbContext db, IPaymentGateway gateway, VetAuditService audit)
    {
        _db = db;
        _gateway = gateway;
        _audit = audit;
    }

    /// <summary>The user's chosen card, else their default card, else any card they own.</summary>
    public async Task<PaymentMethod?> FindCardAsync(int userId, int? paymentMethodId, CancellationToken ct = default)
    {
        var cards = await _db.PaymentMethods.AsNoTracking()
            .Where(p => p.UserId == userId)
            .ToListAsync(ct);
        return cards.FirstOrDefault(p => p.Id == paymentMethodId)
               ?? cards.FirstOrDefault(p => p.IsDefault)
               ?? cards.FirstOrDefault();
    }

    public async Task<PaymentTransaction> ChargeAsync(ChargeRequest request, CancellationToken ct = default)
    {
        var currency = AppMoney.Code();
        var description = Truncate(request.Description, 200);
        var result = await _gateway.ChargeAsync(
            new GatewayChargeRequest(request.Amount, currency, request.Card, description), ct);

        var transaction = new PaymentTransaction
        {
            UserId = request.UserId,
            ProviderId = request.ProviderId,
            ConsultationId = request.ConsultationId,
            BehaviorCaseId = request.BehaviorCaseId,
            CareSubscriptionId = request.CareSubscriptionId,
            Purpose = request.Purpose,
            Status = result.Succeeded ? PaymentTransactionStatus.Succeeded : PaymentTransactionStatus.Failed,
            Amount = request.Amount,
            ServiceTotal = request.ServiceTotal ?? request.Amount,
            Currency = currency,
            Gateway = _gateway.Name,
            ExternalReference = result.Reference,
            PaymentMethodId = request.Card.Id,
            CardBrand = request.Card.Brand,
            CardLast4 = request.Card.Last4,
            FailureCode = result.FailureCode,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };
        _db.PaymentTransactions.Add(transaction);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(result.Succeeded ? "payment_succeeded" : "payment_failed", request.UserId,
            "PaymentTransaction", transaction.Id,
            new { transaction.Amount, transaction.Currency, purpose = transaction.Purpose.ToString(), transaction.FailureCode }, ct);
        return transaction;
    }

    /// <summary>Links charges made before their booking existed to the booking's appointment.</summary>
    public async Task AttachAppointmentAsync(PaymentTransaction transaction, int appointmentId, CancellationToken ct = default)
    {
        transaction.AppointmentId = appointmentId;
        await _db.SaveChangesAsync(ct);
    }

    public async Task AttachCareSubscriptionAsync(PaymentTransaction transaction, int subscriptionId, CancellationToken ct = default)
    {
        transaction.CareSubscriptionId = subscriptionId;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Refunds the full amount of a successful charge. Returns false when nothing was refunded.</summary>
    public async Task<bool> RefundAsync(PaymentTransaction transaction, string reason, int? actorUserId, CancellationToken ct = default)
    {
        if (transaction.Status != PaymentTransactionStatus.Succeeded) return false;

        // Legacy rows have no processor charge behind them, so there is nothing to call.
        var result = transaction.Gateway == PaymentTransactionsSchema.LegacyGateway
            ? new GatewayRefundResult(true, $"legacy_refund_{transaction.Id}", null)
            : await _gateway.RefundAsync(transaction.ExternalReference, transaction.Amount, ct);
        if (!result.Succeeded) return false;

        transaction.Status = PaymentTransactionStatus.Refunded;
        transaction.RefundedAt = DateTime.UtcNow;
        transaction.RefundReference = result.Reference;
        transaction.RefundReason = Truncate(reason, 200);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("payment_refunded", actorUserId, "PaymentTransaction", transaction.Id,
            new { transaction.Amount, transaction.Currency, transaction.RefundReference, reason }, ct);
        return true;
    }

    public async Task<int> RefundAppointmentAsync(int appointmentId, string reason, int? actorUserId, CancellationToken ct = default)
    {
        var charges = await _db.PaymentTransactions
            .Where(t => t.AppointmentId == appointmentId && t.Status == PaymentTransactionStatus.Succeeded)
            .ToListAsync(ct);
        var refunded = 0;
        foreach (var charge in charges)
        {
            if (await RefundAsync(charge, reason, actorUserId, ct))
                refunded++;
        }
        return refunded;
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
