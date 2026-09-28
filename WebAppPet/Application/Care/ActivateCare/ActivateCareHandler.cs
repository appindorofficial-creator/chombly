using WebAppPet.Application.Care.Shared;
using WebAppPet.Application.Common;
using WebAppPet.Application.Payments.Shared;
using WebAppPet.Domain;
using WebAppPet.Infrastructure.Persistence;

namespace WebAppPet.Application.Care.ActivateCare;

public enum ActivateCareOutcome
{
    TermsNotAccepted,
    NoPaymentMethod,
    PaymentDeclined,
    Activated
}

public sealed record ActivateCareResult(ActivateCareOutcome Outcome, string? PaymentError = null);

/// <summary>
/// Starts the monthly membership, charging the first month to the family's card. When a membership
/// is already active nothing is charged and the existing one is kept.
/// </summary>
public class ActivateCareHandler
{
    private readonly AppDbContext _db;
    private readonly ServiceCatalogService _catalog;
    private readonly ChomblyCareService _care;
    private readonly PaymentService _payments;
    private readonly ConsentService _consent;
    private readonly VetAuditService _audit;

    public ActivateCareHandler(
        AppDbContext db,
        ServiceCatalogService catalog,
        ChomblyCareService care,
        PaymentService payments,
        ConsentService consent,
        VetAuditService audit)
    {
        _db = db;
        _catalog = catalog;
        _care = care;
        _payments = payments;
        _consent = consent;
        _audit = audit;
    }

    public async Task<ActivateCareResult> HandleAsync(ActivateCareCommand command, CancellationToken ct = default)
    {
        if (!command.AcceptTerms || !command.AcceptRenewal)
            return new ActivateCareResult(ActivateCareOutcome.TermsNotAccepted);

        var (_, price) = await _catalog.CatalogPriceAsync(ct);
        var subscription = await _care.GetActiveAsync(command.UserId, ct);
        if (subscription is null)
        {
            var card = await _payments.FindCardAsync(command.UserId, null, ct);
            if (card is null)
                return new ActivateCareResult(ActivateCareOutcome.NoPaymentMethod);

            var charge = await _payments.ChargeAsync(new ChargeRequest
            {
                UserId = command.UserId,
                Card = card,
                Amount = price,
                Purpose = PaymentPurpose.CareSubscription,
                Description = "Chombly Care · first month"
            }, ct);
            if (charge.Status != PaymentTransactionStatus.Succeeded)
                return new ActivateCareResult(ActivateCareOutcome.PaymentDeclined, PaymentFailureCodes.Message(charge.FailureCode));

            var now = DateTime.UtcNow;
            subscription = new CareSubscription
            {
                UserId = command.UserId,
                Status = CareSubscriptionStatus.Active,
                PricePerMonth = price,
                Currency = charge.Currency,
                StartedAt = now,
                CurrentPeriodStart = now,
                CurrentPeriodEnd = now.AddMonths(1),
                QuickConsultsPerCycle = 1
            };
            _db.CareSubscriptions.Add(subscription);
            await _db.SaveChangesAsync(ct);
            await _payments.AttachCareSubscriptionAsync(charge, subscription.Id, ct);
        }

        await _consent.SaveAsync(command.UserId, command.ConsultationId, new[]
        {
            (ConsentService.DocTerms, true),
            ("care_auto_renewal", true)
        }, command.IpAddress, command.UserAgent, ct);

        await _audit.LogAsync("care_activated", command.UserId, "CareSubscription", subscription.Id, new { price }, ct);

        return new ActivateCareResult(ActivateCareOutcome.Activated);
    }
}
