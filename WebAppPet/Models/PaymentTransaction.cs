using System.ComponentModel.DataAnnotations;

namespace WebAppPet.Models;

public enum PaymentPurpose
{
    BookingDeposit = 0,
    VetConsultation = 1,
    BehaviorSession = 2,
    CareSubscription = 3
}

public enum PaymentTransactionStatus
{
    Succeeded = 0,
    Failed = 1,
    Refunded = 2
}

/// <summary>
/// One card charge through the payment gateway. Ids are stored without foreign keys so the
/// financial record survives deletion of the user, business or booking it refers to.
/// </summary>
public class PaymentTransaction
{
    public int Id { get; set; }

    public int UserId { get; set; }

    /// <summary>Business (GroomerProfile) that earns the charge; null for Chombly's own products.</summary>
    public int? ProviderId { get; set; }

    public int? AppointmentId { get; set; }
    public int? ConsultationId { get; set; }
    public int? BehaviorCaseId { get; set; }
    public int? CareSubscriptionId { get; set; }

    public PaymentPurpose Purpose { get; set; }
    public PaymentTransactionStatus Status { get; set; }

    /// <summary>Amount charged to the card.</summary>
    public decimal Amount { get; set; }

    /// <summary>Full price of the service; commission is computed on it (deposits charge only part).</summary>
    public decimal ServiceTotal { get; set; }

    [MaxLength(8)]
    public string Currency { get; set; } = "COP";

    [MaxLength(30)]
    public string Gateway { get; set; } = string.Empty;

    [MaxLength(80)]
    public string ExternalReference { get; set; } = string.Empty;

    public int? PaymentMethodId { get; set; }

    [MaxLength(20)]
    public string? CardBrand { get; set; }

    [MaxLength(4)]
    public string? CardLast4 { get; set; }

    [MaxLength(40)]
    public string? FailureCode { get; set; }

    [MaxLength(200)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? RefundedAt { get; set; }

    [MaxLength(80)]
    public string? RefundReference { get; set; }

    [MaxLength(200)]
    public string? RefundReason { get; set; }
}
