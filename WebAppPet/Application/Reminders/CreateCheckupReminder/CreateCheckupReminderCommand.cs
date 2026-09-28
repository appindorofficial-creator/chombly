namespace WebAppPet.Application.Reminders.CreateCheckupReminder;

public sealed record CreateCheckupReminderCommand(int UserId, int PetId);
