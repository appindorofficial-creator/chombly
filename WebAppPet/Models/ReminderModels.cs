using System.ComponentModel.DataAnnotations;

namespace WebAppPet.Models;

public enum ReminderType
{
    Vaccine = 0,
    Medication = 1,
    Appointment = 2,
    Custom = 3
}

public enum ReminderDeliveryStatus
{
    Pending = 0,
    Sent = 1,
    SuppressedQuietHours = 2,
    Failed = 3
}

public enum ReminderChannel
{
    InApp = 0
}

public class ReminderSchedule
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public int? PetId { get; set; }
    public Pet? Pet { get; set; }

    public ReminderType Type { get; set; } = ReminderType.Vaccine;

    [Required, MaxLength(160)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(400)]
    public string? Notes { get; set; }

    /// <summary>Repeat every N days; null = one-shot.</summary>
    public int? FrequencyDays { get; set; }

    public DateTime NextDueUtc { get; set; }

    public DateTime? LastSentUtc { get; set; }

    /// <summary>Local wall-clock start of quiet hours (e.g. 21:00).</summary>
    public TimeSpan? QuietHoursStartLocal { get; set; }

    /// <summary>Local wall-clock end of quiet hours (e.g. 08:00).</summary>
    public TimeSpan? QuietHoursEndLocal { get; set; }

    [MaxLength(64)]
    public string TimeZoneId { get; set; } = "America/New_York";

    public bool IsActive { get; set; } = true;

    public ReminderChannel Channel { get; set; } = ReminderChannel.InApp;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public ICollection<ReminderDelivery> Deliveries { get; set; } = new List<ReminderDelivery>();
}

public class ReminderDelivery
{
    public int Id { get; set; }

    public int ReminderScheduleId { get; set; }
    public ReminderSchedule ReminderSchedule { get; set; } = null!;

    public DateTime ScheduledForUtc { get; set; }

    public DateTime? SentUtc { get; set; }

    public ReminderDeliveryStatus Status { get; set; } = ReminderDeliveryStatus.Pending;

    [MaxLength(400)]
    public string Message { get; set; } = string.Empty;

    public int? AppNotificationId { get; set; }
}
