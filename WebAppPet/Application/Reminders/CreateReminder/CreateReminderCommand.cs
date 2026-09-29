using WebAppPet.Domain;


namespace WebAppPet.Application.Reminders.CreateReminder;

/// <param name="NextDueLocal">Date in the family's market.</param>
/// <param name="QuietStart">Optional "HH:mm" start of the window where reminders are held back.</param>
/// <param name="DueTime">"HH:mm" in the family's market when the reminder fires; 9:00 when empty.</param>
public sealed record CreateReminderCommand(
    int UserId,
    int PetId,
    ReminderType? Type,
    string? Title,
    string? Notes,
    int? FrequencyDays,
    DateTime? NextDueLocal,
    string? QuietStart,
    string? QuietEnd,
    string? DueTime = null);
