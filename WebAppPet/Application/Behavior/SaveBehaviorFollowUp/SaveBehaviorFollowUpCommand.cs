namespace WebAppPet.Application.Behavior.SaveBehaviorFollowUp;

public sealed record SaveBehaviorFollowUpCommand(int ClientId, int CaseId, string? FollowUpNotes);
