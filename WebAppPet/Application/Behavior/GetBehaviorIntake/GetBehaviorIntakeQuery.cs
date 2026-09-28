using WebAppPet.Domain;


namespace WebAppPet.Application.Behavior.GetBehaviorIntake;

public sealed record GetBehaviorIntakeQuery(int ClientId, int CaseId);

/// <param name="Dogs">The client's dogs to choose from.</param>
public sealed record BehaviorIntakeForm(BehaviorCase Case, List<Pet> Dogs);
