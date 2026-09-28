using WebAppPet.Application.Behavior.GetBehaviorIntake;

namespace WebAppPet.Application.Behavior.SubmitBehaviorIntake;

/// <param name="HasClinicalConcern">The client suspects a medical cause; the case is referred to a vet instead.</param>
public sealed record SubmitBehaviorIntakeCommand(
    int ClientId,
    int CaseId,
    IReadOnlyCollection<int> SelectedPetIds,
    string? ProblemType,
    string? Frequency,
    string? ContextNotes,
    bool HasClinicalConcern);

public enum SubmitBehaviorIntakeOutcome
{
    NotFound,
    NoDogSelected,
    NoProblem,
    NoFrequency,
    ReferredToVet,
    Completed
}

/// <param name="Form">The form to show again; null when not found.</param>
/// <param name="SelectedPetIds">The submitted ids that are the client's dogs.</param>
public sealed record SubmitBehaviorIntakeResult(
    SubmitBehaviorIntakeOutcome Outcome,
    BehaviorIntakeForm? Form,
    List<int> SelectedPetIds);
