using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Payments.Shared;
using WebAppPet.Data;
using WebAppPet.Models;

namespace WebAppPet.Application.Payments.GetAdminPayments;

/// <summary>Every card charge on the platform, filtered, with per-currency totals over the whole filter.</summary>
public class GetAdminPaymentsHandler
{
    public const int RowsShown = 150;

    private readonly AppDbContext _db;
    private readonly ProviderPayoutService _payouts;

    public GetAdminPaymentsHandler(AppDbContext db, ProviderPayoutService payouts)
    {
        _db = db;
        _payouts = payouts;
    }

    public async Task<AdminPaymentsView> HandleAsync(GetAdminPaymentsQuery query, CancellationToken ct = default)
    {
        var q = _db.PaymentTransactions.AsNoTracking();
        if (query.Status is PaymentTransactionStatus status)
            q = q.Where(t => t.Status == status);
        if (query.Purpose is PaymentPurpose purpose)
            q = q.Where(t => t.Purpose == purpose);
        if (query.FromUtc is DateTime from)
            q = q.Where(t => t.CreatedAt >= from);
        if (query.ToUtc is DateTime to)
            q = q.Where(t => t.CreatedAt < to);

        var summary = await q
            .Select(t => new { t.Currency, t.Status, t.ProviderId, t.Amount, t.ServiceTotal })
            .ToListAsync(ct);

        var businessIds = summary.Where(t => t.ProviderId.HasValue).Select(t => t.ProviderId!.Value).Distinct().ToList();
        var businesses = await _db.Groomers.AsNoTracking()
            .Where(g => businessIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, ct);
        var commissionPercent = new Dictionary<int, decimal>();
        foreach (var business in businesses.Values)
            commissionPercent[business.Id] = await _payouts.CommissionPercentAsync(business, ct);

        var totals = summary
            .GroupBy(t => t.Currency)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var held = g.Where(t => t.Status == PaymentTransactionStatus.Succeeded).ToList();
                return new AdminPaymentTotals(
                    g.Key,
                    held.Sum(t => t.Amount),
                    g.Where(t => t.Status == PaymentTransactionStatus.Refunded).Sum(t => t.Amount),
                    g.Count(t => t.Status == PaymentTransactionStatus.Failed),
                    Math.Round(held
                        .Where(t => t.ProviderId.HasValue)
                        .Sum(t => t.ServiceTotal * commissionPercent.GetValueOrDefault(t.ProviderId!.Value, 20m) / 100m), 2),
                    held.Where(t => t.ProviderId is null).Sum(t => t.Amount));
            })
            .ToList();

        var page = await q
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.Id)
            .Take(RowsShown)
            .ToListAsync(ct);

        var clientIds = page.Select(t => t.UserId).Distinct().ToList();
        var clients = await _db.Users.AsNoTracking()
            .Where(u => clientIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var rows = page.Select(t => new AdminPaymentRow(
            t.Id,
            t.CreatedAt,
            t.Status,
            t.Purpose,
            t.Currency,
            t.Amount,
            t.ServiceTotal,
            clients.GetValueOrDefault(t.UserId) ?? $"#{t.UserId}",
            t.ProviderId is int pid ? businesses.GetValueOrDefault(pid)?.BusinessName ?? $"#{pid}" : null,
            t.CardBrand,
            t.CardLast4,
            t.Gateway,
            t.ExternalReference,
            t.FailureCode,
            t.Description,
            t.RefundedAt,
            t.RefundReason)).ToList();

        return new AdminPaymentsView(rows, totals, summary.Count);
    }
}
