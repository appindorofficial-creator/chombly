using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Payments.Shared;

public class ProviderPayoutService
{
    private readonly AppDbContext _db;
    private readonly VetAuditService _audit;

    public ProviderPayoutService(AppDbContext db, VetAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public static CompensationServiceType MapFromVetKind(VetProviderKind kind) => kind switch
    {
        VetProviderKind.InternationalAdvisor => CompensationServiceType.InternationalVet,
        VetProviderKind.BehaviorSpecialist => CompensationServiceType.Behavior,
        _ => CompensationServiceType.LocalVet
    };

    /// <summary>
    /// Provider-specific active rule first, else platform default (ProviderUserId null).
    /// </summary>
    public async Task<ProviderCompensationRule?> GetEffectiveRuleAsync(
        int providerUserId,
        CompensationServiceType serviceType,
        DateTime? asOfUtc = null,
        CancellationToken ct = default)
    {
        var asOf = asOfUtc ?? DateTime.UtcNow;
        var rules = await _db.ProviderCompensationRules.AsNoTracking()
            .Where(r => r.IsActive &&
                        r.ServiceType == serviceType &&
                        r.EffectiveFrom <= asOf &&
                        (r.EffectiveTo == null || r.EffectiveTo >= asOf) &&
                        (r.ProviderUserId == null || r.ProviderUserId == providerUserId))
            .OrderByDescending(r => r.ProviderUserId.HasValue)
            .ThenByDescending(r => r.EffectiveFrom)
            .ToListAsync(ct);

        return rules.FirstOrDefault();
    }

    public async Task SeedDefaultRulesAsync(CancellationToken ct = default)
    {
        async Task EnsureDefault(CompensationServiceType type, decimal pct, string note)
        {
            var exists = await _db.ProviderCompensationRules.AnyAsync(
                r => r.ProviderUserId == null && r.ServiceType == type && r.IsActive, ct);
            if (exists) return;

            _db.ProviderCompensationRules.Add(new ProviderCompensationRule
            {
                ProviderUserId = null,
                ServiceType = type,
                CommissionPercent = pct,
                FlatFeeUsd = null,
                PayoutCurrency = "USD",
                IsActive = true,
                EffectiveFrom = DateTime.UtcNow.Date,
                Notes = note,
                CreatedUtc = DateTime.UtcNow
            });
        }

        await EnsureDefault(CompensationServiceType.LocalVet, 20m, "Platform default LocalVet commission");
        await EnsureDefault(CompensationServiceType.InternationalVet, 25m, "Platform default InternationalVet commission");
        await EnsureDefault(CompensationServiceType.Behavior, 20m, "Platform default Behavior commission");
        await _db.SaveChangesAsync(ct);
    }

    public async Task EnsureProviderRuleAsync(
        int providerUserId,
        CompensationServiceType serviceType,
        CancellationToken ct = default)
    {
        var existing = await _db.ProviderCompensationRules.AnyAsync(
            r => r.ProviderUserId == providerUserId && r.ServiceType == serviceType && r.IsActive, ct);
        if (existing) return;

        var defaults = await GetEffectiveRuleAsync(providerUserId, serviceType, ct: ct);
        var pct = defaults?.CommissionPercent ?? serviceType switch
        {
            CompensationServiceType.InternationalVet => 25m,
            _ => 20m
        };

        var country = await _db.Users.AsNoTracking()
            .Where(u => u.Id == providerUserId)
            .Select(u => u.CountryCode)
            .FirstOrDefaultAsync(ct);

        _db.ProviderCompensationRules.Add(new ProviderCompensationRule
        {
            ProviderUserId = providerUserId,
            ServiceType = serviceType,
            CommissionPercent = pct,
            FlatFeeUsd = defaults?.FlatFeeUsd,
            PayoutCurrency = AppMoney.Code(country),
            IsActive = true,
            EffectiveFrom = DateTime.UtcNow,
            Notes = "Activated on professional onboarding approval",
            CreatedUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<ProviderCompensationRule>> ListRulesForProviderAsync(int providerUserId, CancellationToken ct = default)
    {
        var personal = await _db.ProviderCompensationRules.AsNoTracking()
            .Where(r => r.ProviderUserId == providerUserId && r.IsActive)
            .OrderBy(r => r.ServiceType)
            .ToListAsync(ct);

        if (personal.Count > 0) return personal;

        return await _db.ProviderCompensationRules.AsNoTracking()
            .Where(r => r.ProviderUserId == null && r.IsActive)
            .OrderBy(r => r.ServiceType)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Settles what was actually collected online: successful charges earned by the business in the
    /// period (refunded ones drop out). Commission is taken on the full service price, so the business
    /// receives the online amount minus commission and collects the remaining balance in person.
    /// </summary>
    public async Task<(decimal Gross, decimal Commission, decimal Net, int Count, ProviderCompensationRule? Rule)>
        CalculatePayoutForPeriodAsync(int providerUserId, DateTime periodStart, DateTime periodEnd, CancellationToken ct = default)
    {
        var groomer = await _db.Groomers.AsNoTracking()
            .FirstOrDefaultAsync(g => g.UserId == providerUserId, ct);
        var rule = await GetRuleForBusinessAsync(providerUserId, groomer, periodEnd, ct);

        var charges = groomer is null
            ? []
            : await _db.PaymentTransactions.AsNoTracking()
                .Where(t => t.ProviderId == groomer.Id
                    && t.Status == PaymentTransactionStatus.Succeeded
                    && t.CreatedAt >= periodStart
                    && t.CreatedAt < periodEnd)
                .Select(t => new { t.Amount, t.ServiceTotal })
                .ToListAsync(ct);

        var gross = charges.Sum(c => c.Amount);
        var commission = charges.Count == 0
            ? 0m
            : Math.Round(charges.Sum(c => c.ServiceTotal) * (CommissionPercent(rule) / 100m) + (rule?.FlatFeeUsd ?? 0m), 2);
        var net = Math.Round(gross - commission, 2);
        return (gross, commission, net, charges.Count, rule);
    }

    /// <summary>Recent card charges earned by the business, refunded ones included, for the provider dashboard.</summary>
    public async Task<List<ProviderPaymentRow>> ListRecentFamilyPaymentsAsync(
        int providerUserId,
        int take = 30,
        CancellationToken ct = default)
    {
        var groomer = await _db.Groomers.AsNoTracking()
            .FirstOrDefaultAsync(g => g.UserId == providerUserId, ct);
        if (groomer is null) return [];

        var pct = CommissionPercent(await GetRuleForBusinessAsync(providerUserId, groomer, null, ct));

        var charges = await _db.PaymentTransactions.AsNoTracking()
            .Where(t => t.ProviderId == groomer.Id && t.Status != PaymentTransactionStatus.Failed)
            .OrderByDescending(t => t.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

        var appointmentIds = charges.Where(t => t.AppointmentId.HasValue).Select(t => t.AppointmentId!.Value).ToList();
        var appointments = await _db.Appointments.AsNoTracking()
            .Include(a => a.Pet)
            .Include(a => a.Service)
            .Where(a => appointmentIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);
        var clientIds = charges.Select(t => t.UserId).Distinct().ToList();
        var clients = await _db.Users.AsNoTracking()
            .Where(u => clientIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        return charges.Select(t =>
        {
            var appt = t.AppointmentId is int id ? appointments.GetValueOrDefault(id) : null;
            var refunded = t.Status == PaymentTransactionStatus.Refunded;
            var commission = refunded ? 0m : Math.Round(t.ServiceTotal * (pct / 100m), 2);
            return new ProviderPaymentRow(
                t.Id,
                t.ExternalReference,
                t.Status,
                t.Purpose,
                t.CreatedAt,
                t.RefundedAt,
                appt?.ScheduledAt,
                appt?.Pet?.Name ?? "—",
                appt?.Service?.Name ?? t.Description ?? "—",
                clients.GetValueOrDefault(t.UserId) ?? "—",
                t.Currency,
                t.Amount,
                t.ServiceTotal,
                Math.Max(0m, t.ServiceTotal - t.Amount),
                commission,
                refunded ? 0m : Math.Round(t.Amount - commission, 2));
        }).ToList();
    }

    public sealed record ProviderPaymentRow(
        int TransactionId,
        string Reference,
        PaymentTransactionStatus Status,
        PaymentPurpose Purpose,
        DateTime PaidAtUtc,
        DateTime? RefundedAtUtc,
        DateTime? ScheduledAtUtc,
        string PetName,
        string ServiceName,
        string ClientName,
        string Currency,
        decimal ChargedOnline,
        decimal ServiceTotal,
        decimal BalanceAtBusiness,
        decimal Commission,
        decimal Net);

    private async Task<ProviderCompensationRule?> GetRuleForBusinessAsync(
        int providerUserId, GroomerProfile? groomer, DateTime? asOfUtc, CancellationToken ct)
    {
        var serviceType = MapFromVetKind(groomer?.VetProviderKind ?? VetProviderKind.None);
        return await GetEffectiveRuleAsync(providerUserId, serviceType, asOfUtc, ct)
            ?? await GetEffectiveRuleAsync(providerUserId, CompensationServiceType.LocalVet, asOfUtc, ct);
    }

    public async Task<decimal> CommissionPercentAsync(GroomerProfile business, CancellationToken ct = default) =>
        CommissionPercent(await GetRuleForBusinessAsync(business.UserId, business, null, ct));

    private static decimal CommissionPercent(ProviderCompensationRule? rule) => rule?.CommissionPercent ?? 20m;

    public async Task<ProviderPayout> CreatePendingPayoutAsync(
        int providerUserId,
        DateTime periodStart,
        DateTime periodEnd,
        int? actorUserId = null,
        CancellationToken ct = default)
    {
        // Allow a new summary when the only overlap is an empty (0-item) payout.
        var overlap = await _db.ProviderPayouts
            .Where(p => p.ProviderUserId == providerUserId &&
                        p.Status != ProviderPayoutStatus.Failed &&
                        p.PeriodStart < periodEnd &&
                        p.PeriodEnd > periodStart)
            .ToListAsync(ct);
        if (overlap.Any(p => p.ConsultationCount > 0 || p.GrossAmountUsd > 0))
            throw new PayoutPeriodOverlapException();

        foreach (var empty in overlap.Where(p => p.ConsultationCount == 0 && p.GrossAmountUsd == 0))
        {
            // Drop empty stubs so a corrected summary can be created for the same window.
            if (empty.Status == ProviderPayoutStatus.Pending)
                _db.ProviderPayouts.Remove(empty);
        }
        if (overlap.Count > 0)
            await _db.SaveChangesAsync(ct);

        var calc = await CalculatePayoutForPeriodAsync(providerUserId, periodStart, periodEnd, ct);
        var payout = new ProviderPayout
        {
            ProviderUserId = providerUserId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            GrossAmountUsd = calc.Gross,
            CommissionAmountUsd = calc.Commission,
            NetAmountUsd = calc.Net,
            ConsultationCount = calc.Count,
            CompensationRuleId = calc.Rule?.Id,
            Status = ProviderPayoutStatus.Pending,
            CreatedUtc = DateTime.UtcNow,
            Notes = calc.Count == 0 ? "No completed billable items in period (simulated summary)." : null
        };

        _db.ProviderPayouts.Add(payout);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("payout_created", actorUserId ?? providerUserId, "ProviderPayout", payout.Id,
            new
            {
                payout.ProviderUserId,
                payout.PeriodStart,
                payout.PeriodEnd,
                payout.GrossAmountUsd,
                payout.NetAmountUsd,
                payout.ConsultationCount
            }, ct);

        return payout;
    }

    public async Task<ProviderPayout?> MarkPaidAsync(int payoutId, int? actorUserId = null, CancellationToken ct = default)
    {
        var payout = await _db.ProviderPayouts
            .Include(p => p.ProviderUser)
            .FirstOrDefaultAsync(p => p.Id == payoutId, ct);
        if (payout is null) return null;
        if (payout.Status == ProviderPayoutStatus.Paid) return payout;

        // Refresh totals (includes family appointments) before settling.
        await ApplyCalculatedTotalsAsync(payout, ct);

        payout.Status = ProviderPayoutStatus.Paid;
        payout.PaidUtc = DateTime.UtcNow;
        payout.ExternalReference ??= "sim_connect_" + Guid.NewGuid().ToString("N")[..16];
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("payout_marked_paid", actorUserId, "ProviderPayout", payout.Id,
            new { payout.ExternalReference, payout.GrossAmountUsd, payout.CommissionAmountUsd, payout.NetAmountUsd }, ct);

        return payout;
    }

    /// <summary>
    /// Recompute gross/commission/net/count for unpaid period summaries
    /// (e.g. after marketplace appointments were added to the calculator).
    /// Paid summaries keep the amounts they were settled with.
    /// </summary>
    public async Task<int> RefreshAllPayoutTotalsAsync(CancellationToken ct = default) =>
        await RefreshPayoutTotalsAsync(providerUserId: null, ct);

    public async Task<int> RefreshPayoutTotalsAsync(int? providerUserId, CancellationToken ct = default)
    {
        var q = _db.ProviderPayouts.Where(p => p.Status != ProviderPayoutStatus.Paid);
        if (providerUserId is int uid)
            q = q.Where(p => p.ProviderUserId == uid);

        var payouts = await q.ToListAsync(ct);
        var changed = 0;
        foreach (var p in payouts)
        {
            if (await ApplyCalculatedTotalsAsync(p, ct))
                changed++;
        }

        if (changed > 0)
            await _db.SaveChangesAsync(ct);
        return changed;
    }

    private async Task<bool> ApplyCalculatedTotalsAsync(ProviderPayout payout, CancellationToken ct)
    {
        var calc = await CalculatePayoutForPeriodAsync(
            payout.ProviderUserId, payout.PeriodStart, payout.PeriodEnd, ct);

        var changed =
            payout.GrossAmountUsd != calc.Gross
            || payout.CommissionAmountUsd != calc.Commission
            || payout.NetAmountUsd != calc.Net
            || payout.ConsultationCount != calc.Count;

        if (!changed) return false;

        payout.GrossAmountUsd = calc.Gross;
        payout.CommissionAmountUsd = calc.Commission;
        payout.NetAmountUsd = calc.Net;
        payout.ConsultationCount = calc.Count;
        if (calc.Rule is not null)
            payout.CompensationRuleId = calc.Rule.Id;
        if (calc.Count > 0
            && payout.Notes is not null
            && payout.Notes.Contains("No completed", StringComparison.OrdinalIgnoreCase))
            payout.Notes = null;
        return true;
    }

    public Task<List<ProviderPayout>> ListForProviderAsync(int providerUserId, CancellationToken ct = default) =>
        _db.ProviderPayouts.AsNoTracking()
            .Where(p => p.ProviderUserId == providerUserId)
            .OrderByDescending(p => p.CreatedUtc)
            .ToListAsync(ct);

    public Task<List<ProviderPayout>> ListPendingAsync(CancellationToken ct = default) =>
        _db.ProviderPayouts.AsNoTracking()
            .Include(p => p.ProviderUser)
            .Where(p => p.Status == ProviderPayoutStatus.Pending || p.Status == ProviderPayoutStatus.Processing)
            .OrderBy(p => p.CreatedUtc)
            .ToListAsync(ct);

    public Task<List<ProviderPayout>> ListAllAsync(int take = 100, CancellationToken ct = default) =>
        _db.ProviderPayouts.AsNoTracking()
            .Include(p => p.ProviderUser)
            .OrderByDescending(p => p.CreatedUtc)
            .Take(take)
            .ToListAsync(ct);
}
