using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;

namespace WebAppPet.Services;

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

        _db.ProviderCompensationRules.Add(new ProviderCompensationRule
        {
            ProviderUserId = providerUserId,
            ServiceType = serviceType,
            CommissionPercent = pct,
            FlatFeeUsd = defaults?.FlatFeeUsd,
            PayoutCurrency = "USD",
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

        var serviceType = MapFromVetKind(groomer?.VetProviderKind ?? VetProviderKind.LocalVet);
        var rule = await GetEffectiveRuleAsync(providerUserId, serviceType, periodEnd, ct);

        decimal gross = 0;
        var count = 0;

        if (groomer != null)
        {
            var consults = await _db.Consultations.AsNoTracking()
                .Where(c => c.ProviderId == groomer.Id &&
                            c.Status == ConsultationStatus.Completed &&
                            c.UpdatedAt >= periodStart &&
                            c.UpdatedAt < periodEnd)
                .Select(c => c.PriceCharged)
                .ToListAsync(ct);

            gross += consults.Sum();
            count += consults.Count;

            if (serviceType == CompensationServiceType.Behavior || groomer.VetProviderKind == VetProviderKind.BehaviorSpecialist)
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

    public async Task<ProviderPayout> CreatePendingPayoutAsync(
        int providerUserId,
        DateTime periodStart,
        DateTime periodEnd,
        int? actorUserId = null,
        CancellationToken ct = default)
    {
        var overlap = await _db.ProviderPayouts.AnyAsync(
            p => p.ProviderUserId == providerUserId &&
                 p.Status != ProviderPayoutStatus.Failed &&
                 p.PeriodStart < periodEnd &&
                 p.PeriodEnd > periodStart, ct);
        if (overlap)
            throw new InvalidOperationException("A payout already exists for an overlapping period.");

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
        var payout = await _db.ProviderPayouts.FirstOrDefaultAsync(p => p.Id == payoutId, ct);
        if (payout is null) return null;
        if (payout.Status == ProviderPayoutStatus.Paid) return payout;

        payout.Status = ProviderPayoutStatus.Paid;
        payout.PaidUtc = DateTime.UtcNow;
        payout.ExternalReference ??= "sim_connect_" + Guid.NewGuid().ToString("N")[..16];
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("payout_marked_paid", actorUserId, "ProviderPayout", payout.Id,
            new { payout.ExternalReference, payout.NetAmountUsd }, ct);

        return payout;
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
