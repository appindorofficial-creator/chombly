namespace WebAppPet.Application.Reminders.DeactivateReminder;

public sealed record DeactivateReminderCommand(int UserId, int ScheduleId);
