using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Care.Shared;
using WebAppPet.Application.Common;
using WebAppPet.Application.Payments.Shared;
using WebAppPet.Domain;
using WebAppPet.Domain.Markets;
using WebAppPet.Infrastructure.Persistence;
using WebAppPet.Localization;

namespace WebAppPet.Application.Care.RenewCare;

/// <summary>
/// Charges the next month of every active membership whose cycle has ended, at the current price of the
/// currency it was taken in. A membership set to cancel ends instead; without a card or with a declined
/// charge it becomes past due. The family is told either way.
/// </summary>
public class RenewDueCareHandler
{
    private readonly AppDbContext _db;
    private readonly ServiceCatalogService _catalog;
    private readonly PaymentService _payments;
    private readonly VetAuditService _audit;

    public RenewDueCareHandler(AppDbContext db, ServiceCatalogService catalog, PaymentService payments, VetAuditService audit)
    {
        _db = db;
        _catalog = catalog;
        _payments = payments;
        _audit = audit;
    }

    /// <returns>How many memberships were renewed, ended or marked past due.</returns>
    public async Task<int> HandleAsync(RenewDueCareCommand command, CancellationToken ct = default)
    {
        var due = await _db.CareSubscriptions
            .Where(s => s.Status == CareSubscriptionStatus.Active && s.CurrentPeriodEnd <= command.NowUtc)
            .OrderBy(s => s.CurrentPeriodEnd)
            .ToListAsync(ct);
        if (due.Count == 0) return 0;

        var catalogUsd = (await _catalog.GetAsync(ServiceCatalogCodes.ChomblyCare, ct))?.Price ?? CarePlan.FallbackMonthlyPrice;
        foreach (var subscription in due)
        {
            if (subscription.CancelAtPeriodEnd)
            {
                subscription.Status = CareSubscriptionStatus.Cancelled;
                Notify(subscription.UserId,
                    "Chombly Care terminó",
                    "Tu membresía terminó como lo pediste. Puedes volver a activarla cuando quieras.");
            }
            else
            {
                var market = subscription.Currency == "USD" ? BusinessMarket.UnitedStates : BusinessMarket.Colombia;
                using (AppTimeZones.UseMarket(market))
                    await RenewAsync(subscription, catalogUsd, command.NowUtc, ct);
            }
            await _db.SaveChangesAsync(ct);
        }
        return due.Count;
    }

    private async Task RenewAsync(CareSubscription subscription, decimal catalogUsd, DateTime now, CancellationToken ct)
    {
        var country = AppTimeZones.CurrentCountryCode;
        var price = CarePlan.MonthlyPrice(catalogUsd, country);
        var card = await _payments.FindCardAsync(subscription.UserId, null, ct);
        if (card is null)
        {
            MarkPastDue(subscription,
                "No encontramos una tarjeta para renovar tu membresía. Agrega una y actívala de nuevo.");
            await _audit.LogAsync("care_renewal_failed", subscription.UserId, "CareSubscription", subscription.Id,
                new { reason = "no_card" }, ct);
            return;
        }

        var charge = await _payments.ChargeAsync(new ChargeRequest
        {
            UserId = subscription.UserId,
            Card = card,
            Amount = price,
            Currency = AppMoney.Code(country),
            Purpose = PaymentPurpose.CareSubscription,
            Description = "Chombly Care · renewal",
            CareSubscriptionId = subscription.Id
        }, ct);

        if (charge.Status != PaymentTransactionStatus.Succeeded)
        {
            MarkPastDue(subscription,
                $"No pudimos cobrar la renovación a tu tarjeta •••• {card.Last4}. {PaymentFailureCodes.SpanishMessage(charge.FailureCode)} Luego activa la membresía de nuevo.");
            await _audit.LogAsync("care_renewal_failed", subscription.UserId, "CareSubscription", subscription.Id,
                new { charge.FailureCode }, ct);
            return;
        }

        var start = subscription.CurrentPeriodEnd.AddMonths(1) > now ? subscription.CurrentPeriodEnd : now;
        subscription.CurrentPeriodStart = start;
        subscription.CurrentPeriodEnd = start.AddMonths(1);
        subscription.PricePerMonth = price;
        subscription.Currency = charge.Currency;
        var amount = AppMoney.FormatCurrency(price, charge.Currency);
        var next = AppTimeZones.FormatDate(subscription.CurrentPeriodEnd);
        Notify(subscription.UserId,
            "Chombly Care renovado",
            $"Cobramos {amount} a tu tarjeta •••• {card.Last4}. Tu próximo cobro es el {next}.");
        await _audit.LogAsync("care_renewed", subscription.UserId, "CareSubscription", subscription.Id,
            new { price, charge.Currency, chargeId = charge.Id }, ct);
    }

    private void MarkPastDue(CareSubscription subscription, string message)
    {
        subscription.Status = CareSubscriptionStatus.PastDue;
        Notify(subscription.UserId, "Chombly Care pausado", message);
    }

    private void Notify(int userId, string title, string message) =>
        _db.Notifications.Add(new AppNotification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = "care",
            CreatedAt = DateTime.UtcNow
        });
}
