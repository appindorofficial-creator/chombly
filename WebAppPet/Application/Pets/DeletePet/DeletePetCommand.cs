namespace WebAppPet.Application.Pets.DeletePet;

public sealed record DeletePetCommand(int OwnerId, int PetId);

public enum DeletePetResult
{
    Deleted,
    NotFound,
    HasAppointments
}
