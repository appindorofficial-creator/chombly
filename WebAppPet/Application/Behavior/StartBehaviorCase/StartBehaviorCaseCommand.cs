namespace WebAppPet.Application.Behavior.StartBehaviorCase;

/// <param name="PetId">Pet to preselect; ignored when it is not the client's.</param>
public sealed record StartBehaviorCaseCommand(int ClientId, int? PetId = null);
