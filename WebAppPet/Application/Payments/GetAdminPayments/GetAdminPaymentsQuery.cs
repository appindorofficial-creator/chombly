using WebAppPet.Models;

namespace WebAppPet.Application.Payments.GetAdminPayments;

/// <param name="FromUtc">Inclusive lower bound on the charge date.</param>
/// <param name="ToUtc">Exclusive upper bound on the charge date.</param>
public sealed record GetAdminPaymentsQuery(
    PaymentTransactionStatus? Status = null,
    PaymentPurpose? Purpose = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null);

public sealed record AdminPaymentsView(List<AdminPaymentRow> Rows, List<AdminPaymentTotals> Totals, int MatchingCount);

public sealed record AdminPaymentRow(
    int Id,
    DateTime CreatedAt,
    PaymentTransactionStatus Status,
    PaymentPurpose Purpose,
    string Currency,
    decimal Amount,
    decimal ServiceTotal,
    string ClientName,
    string? BusinessName,
    string? CardBrand,
    string? CardLast4,
    string Gateway,
    string Reference,
    string? FailureCode,
    string? Description,
    DateTime? RefundedAt,
    string? RefundReason);

/// <param name="Collected">Charges that are still held (succeeded, not refunded).</param>
/// <param name="Commission">Chombly's commission on business charges, per each business's active rule.</param>
/// <param name="OwnRevenue">Charges for Chombly's own products (e.g. Chombly Care), with no business behind them.</param>
public sealed record AdminPaymentTotals(
    string Currency,
    decimal Collected,
    decimal Refunded,
    int FailedCount,
    decimal Commission,
    decimal OwnRevenue);
