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

    public async Task<(decimal Gross, decimal Commission, decimal Net, int Count, ProviderCompensationRule? Rule)>
        CalculatePayoutForPeriodAsync(int providerUserId, DateTime periodStart, DateTime periodEnd, CancellationToken ct = default)
    {
        var groomer = await _db.Groomers.AsNoTracking()
            .FirstOrDefaultAsync(g => g.UserId == providerUserId, ct);

        var serviceType = MapFromVetKind(groomer?.VetProviderKind ?? VetProviderKind.None);
        var rule = await GetEffectiveRuleAsync(providerUserId, serviceType, periodEnd, ct)
            ?? await GetEffectiveRuleAsync(providerUserId, CompensationServiceType.LocalVet, periodEnd, ct);

        decimal gross = 0;
        var count = 0;

        if (groomer != null)
        {
            // Family marketplace bookings (simulated card charge at checkout).
            var appointmentTotals = await _db.Appointments.AsNoTracking()
                .Where(a => a.GroomerId == groomer.Id
                    && a.Status != AppointmentStatus.Cancelled
                    && a.TotalPrice > 0
                    && a.CreatedAt >= periodStart
                    && a.CreatedAt < periodEnd)
                .Select(a => a.TotalPrice)
                .ToListAsync(ct);

            gross += appointmentTotals.Sum();
            count += appointmentTotals.Count;

            var consults = await _db.Consultations.AsNoTracking()
                .Where(c => c.ProviderId == groomer.Id &&
                            c.Status == ConsultationStatus.Completed &&
                            c.UpdatedAt >= periodStart &&
                            c.UpdatedAt < periodEnd)
                .Select(c => c.PriceCharged)
                .ToListAsync(ct);

            gross += consults.Sum();
            count += consults.Count;

            if (groomer.VetProviderKind == VetProviderKind.BehaviorSpecialist
                || serviceType == CompensationServiceType.Behavior)
            {
                var cases = await _db.BehaviorCases.AsNoTracking()
                    .Where(b => b.ProviderId == groomer.Id &&
                                b.PriceCharged > 0 &&
                                (b.Status == BehaviorCaseStatus.Closed ||
                                 b.Status == BehaviorCaseStatus.PlanActive ||
                                 b.Status == BehaviorCaseStatus.Scheduled) &&
                                b.UpdatedAt >= periodStart &&
                                b.UpdatedAt < periodEnd)
                    .Select(b => b.PriceCharged)
                    .ToListAsync(ct);

                gross += cases.Sum();
                count += cases.Count;
            }
        }

        var pct = rule?.CommissionPercent ?? 20m;
        var flat = rule?.FlatFeeUsd ?? 0m;
        var commission = Math.Round(gross * (pct / 100m) + flat, 2);
        var net = Math.Round(gross - commission, 2);
        return (gross, commission, net, count, rule);
    }

    /// <summary>Recent family bookings with simulated payment for the provider dashboard.</summary>
    public async Task<List<ProviderPaymentRow>> ListRecentFamilyPaymentsAsync(
        int providerUserId,
        int take = 30,
        CancellationToken ct = default)
    {
        var groomer = await _db.Groomers.AsNoTracking()
            .FirstOrDefaultAsync(g => g.UserId == providerUserId, ct);
        if (groomer is null) return [];

        var rule = await GetEffectiveRuleAsync(providerUserId, CompensationServiceType.LocalVet, ct: ct);
        var pct = rule?.CommissionPercent ?? 20m;

        var rows = await _db.Appointments.AsNoTracking()
            .Include(a => a.Pet)
            .Include(a => a.Service)
            .Include(a => a.Client)
            .Where(a => a.GroomerId == groomer.Id
                && a.Status != AppointmentStatus.Cancelled
                && a.TotalPrice > 0)
            .OrderByDescending(a => a.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

        return rows.Select(a =>
        {
            var commission = Math.Round(a.TotalPrice * (pct / 100m), 2);
            return new ProviderPaymentRow(
                a.Id,
                a.CreatedAt,
                a.ScheduledAt,
                a.Status,
                a.Pet?.Name ?? "—",
                a.Service?.Name ?? "—",
                a.Client?.FullName ?? "—",
                a.TotalPrice,
                commission,
                Math.Round(a.TotalPrice - commission, 2),
                a.DepositPaid,
                a.Notes);
        }).ToList();
    }

    public sealed record ProviderPaymentRow(
        int AppointmentId,
        DateTime PaidAtUtc,
        DateTime ScheduledAtUtc,
        AppointmentStatus Status,
        string PetName,
        string ServiceName,
        string ClientName,
        decimal Gross,
        decimal Commission,
        decimal Net,
        decimal DepositPaid,
        string? Notes);

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
    /// Recompute gross/commission/net/count for existing period summaries
    /// (e.g. after marketplace appointments were added to the calculator).
    /// </summary>
    public async Task<int> RefreshAllPayoutTotalsAsync(CancellationToken ct = default) =>
        await RefreshPayoutTotalsAsync(providerUserId: null, ct);

    public async Task<int> RefreshPayoutTotalsAsync(int? providerUserId, CancellationToken ct = default)
    {
        var q = _db.ProviderPayouts.AsQueryable();
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
