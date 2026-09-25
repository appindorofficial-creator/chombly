namespace WebAppPet.Application.Bookings.SaveClinicalNote;

public sealed record SaveClinicalNoteCommand(int BusinessId, int AppointmentId, string? Note);
