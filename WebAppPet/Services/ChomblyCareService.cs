using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;

namespace WebAppPet.Services;

public class ChomblyCareService
{
    private readonly AppDbContext _db;

    public ChomblyCareService(AppDbContext db) => _db = db;

    public async Task<CareSubscription?> GetActiveAsync(int userId, CancellationToken ct = default)
    {
        var sub = await _db.CareSubscriptions
            .Include(s => s.BenefitUses)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == CareSubscriptionStatus.Active, ct);

        if (sub is null) return null;

        // Roll period forward if expired (simulated renewal)
        if (sub.CurrentPeriodEnd < DateTime.UtcNow && !sub.CancelAtPeriodEnd)
        {
            while (sub.CurrentPeriodEnd < DateTime.UtcNow)
            {
                sub.CurrentPeriodStart = sub.CurrentPeriodEnd;
                sub.CurrentPeriodEnd = sub.CurrentPeriodEnd.AddMonths(1);
            }
            await _db.SaveChangesAsync(ct);
        }
        else if (sub.CurrentPeriodEnd < DateTime.UtcNow && sub.CancelAtPeriodEnd)
        {
            sub.Status = CareSubscriptionStatus.Cancelled;
            await _db.SaveChangesAsync(ct);
            return null;
        }

        return sub;
    }

    public int RemainingQuickConsults(CareSubscription sub)
    {
        var used = sub.BenefitUses.Count(u => u.PeriodStart == sub.CurrentPeriodStart);
        return Math.Max(0, sub.QuickConsultsPerCycle - used);
    }

    public async Task<bool> HasQuickConsultAvailableAsync(int userId, CancellationToken ct = default)
    {
        var sub = await GetActiveAsync(userId, ct);
        return sub != null && RemainingQuickConsults(sub) > 0;
    }

    public async Task<CareSubscription> ActivateAsync(int userId, decimal price, CancellationToken ct = default)
    {
        var existing = await _db.CareSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == CareSubscriptionStatus.Active, ct);
        if (existing != null) return existing;

        var now = DateTime.UtcNow;
        var sub = new CareSubscription
        {
            UserId = userId,
            Status = CareSubscriptionStatus.Active,
            PricePerMonth = price,
            StartedAt = now,
            CurrentPeriodStart = now,
            CurrentPeriodEnd = now.AddMonths(1),
            QuickConsultsPerCycle = 1
        };
        _db.CareSubscriptions.Add(sub);
        await _db.SaveChangesAsync(ct);
        return sub;
    }

    public async Task CancelAtPeriodEndAsync(int userId, CancellationToken ct = default)
    {
        var sub = await GetActiveAsync(userId, ct);
        if (sub is null) return;
        sub.CancelAtPeriodEnd = true;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> TryConsumeQuickConsultAsync(int userId, int consultationId, CancellationToken ct = default)
    {
        var sub = await GetActiveAsync(userId, ct);
        if (sub is null || RemainingQuickConsults(sub) <= 0) return false;

        _db.CareBenefitUses.Add(new CareBenefitUse
        {
            SubscriptionId = sub.Id,
            ConsultationId = consultationId,
            PeriodStart = sub.CurrentPeriodStart,
            UsedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
