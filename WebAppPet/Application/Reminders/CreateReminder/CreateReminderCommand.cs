using WebAppPet.Models;

namespace WebAppPet.Application.Reminders.CreateReminder;

/// <param name="NextDueLocal">Date in the family's market; the reminder fires at 9:00 that day.</param>
/// <param name="QuietStart">Optional "HH:mm" start of the window where reminders are held back.</param>
public sealed record CreateReminderCommand(
    int UserId,
    int PetId,
    ReminderType? Type,
    string? Title,
    string? Notes,
    int? FrequencyDays,
    DateTime? NextDueLocal,
    string? QuietStart,
    string? QuietEnd);
