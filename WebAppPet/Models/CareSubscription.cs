using System.ComponentModel.DataAnnotations;

namespace WebAppPet.Models;

public enum CareSubscriptionStatus
{
    Active = 0,
    Cancelled = 1,
    PastDue = 2
}

public class CareSubscription
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public CareSubscriptionStatus Status { get; set; } = CareSubscriptionStatus.Active;

    public decimal PricePerMonth { get; set; } = 14.99m;

    [MaxLength(8)]
    public string Currency { get; set; } = "USD";

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime CurrentPeriodStart { get; set; } = DateTime.UtcNow;

    public DateTime CurrentPeriodEnd { get; set; } = DateTime.UtcNow.AddMonths(1);

    public bool CancelAtPeriodEnd { get; set; }

    /// <summary>Quick international consults included per cycle (configurable).</summary>
    public int QuickConsultsPerCycle { get; set; } = 1;

    public ICollection<CareBenefitUse> BenefitUses { get; set; } = new List<CareBenefitUse>();
}

public class CareBenefitUse
{
    public int Id { get; set; }

    public int SubscriptionId { get; set; }
    public CareSubscription Subscription { get; set; } = null!;

    public int? ConsultationId { get; set; }

    public DateTime PeriodStart { get; set; }

    public DateTime UsedAt { get; set; } = DateTime.UtcNow;
}
