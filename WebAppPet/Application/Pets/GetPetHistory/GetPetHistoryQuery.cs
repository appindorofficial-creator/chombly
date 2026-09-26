using WebAppPet.Models;

namespace WebAppPet.Application.Pets.GetPetHistory;

public sealed record GetPetHistoryQuery(int OwnerId, int PetId);

/// <param name="Appointments">Completed appointments, newest first.</param>
public sealed record PetHistory(Pet Pet, List<Appointment> Appointments, List<Consultation> Consultations);
