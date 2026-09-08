using System.ComponentModel.DataAnnotations;

namespace WebAppPet.Models;

/// <summary>Service track used for commission rules and payouts.</summary>
public enum CompensationServiceType
{
    LocalVet = 0,
    InternationalVet = 1,
    Behavior = 2
}

public enum ProviderPayoutStatus
{
    Pending = 0,
    Processing = 1,
    Paid = 2,
    Failed = 3,
    Held = 4
}

/// <summary>
/// Commission rule. <see cref="ProviderUserId"/> null = platform default for the service type.
/// ProviderUserId maps to <see cref="AppUser.Id"/> (int), not ASP.NET Identity strings.
/// </summary>
public class ProviderCompensationRule
{
    public int Id { get; set; }

    /// <summary>Null = platform-wide default for ServiceType.</summary>
    public int? ProviderUserId { get; set; }
    public AppUser? ProviderUser { get; set; }

    public CompensationServiceType ServiceType { get; set; } = CompensationServiceType.LocalVet;

    /// <summary>Platform commission percent (e.g. 20 = 20%).</summary>
    public decimal CommissionPercent { get; set; }

    public decimal? FlatFeeUsd { get; set; }

    [MaxLength(8)]
    public string PayoutCurrency { get; set; } = "USD";

    public bool IsActive { get; set; } = true;

    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;

    public DateTime? EffectiveTo { get; set; }

    [MaxLength(400)]
    public string? Notes { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public class ProviderPayout
{
    public int Id { get; set; }

    public int ProviderUserId { get; set; }
    public AppUser ProviderUser { get; set; } = null!;

    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    public decimal GrossAmountUsd { get; set; }
    public decimal CommissionAmountUsd { get; set; }
    public decimal NetAmountUsd { get; set; }

    public ProviderPayoutStatus Status { get; set; } = ProviderPayoutStatus.Pending;

    public int ConsultationCount { get; set; }

    public int? CompensationRuleId { get; set; }
    public ProviderCompensationRule? CompensationRule { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime? PaidUtc { get; set; }

    /// <summary>Simulated Stripe Connect transfer id (not a real charge).</summary>
    [MaxLength(80)]
    public string? ExternalReference { get; set; }

    [MaxLength(400)]
    public string? Notes { get; set; }
}
