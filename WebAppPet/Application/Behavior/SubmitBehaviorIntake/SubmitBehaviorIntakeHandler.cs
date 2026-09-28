using WebAppPet.Application.Behavior.GetBehaviorIntake;
using WebAppPet.Application.Behavior.Shared;
using WebAppPet.Application.Common;
using WebAppPet.Domain;
using WebAppPet.Infrastructure.Persistence;

namespace WebAppPet.Application.Behavior.SubmitBehaviorIntake;

/// <summary>
/// Saves the dogs, problem, frequency and context of the case. A clinical concern refers the case
/// to a vet; otherwise the client moves on to pick a specialist.
/// </summary>
public class SubmitBehaviorIntakeHandler
{
    private readonly AppDbContext _db;
    private readonly GetBehaviorIntakeHandler _intake;
    private readonly VetAuditService _audit;

    public SubmitBehaviorIntakeHandler(AppDbContext db, GetBehaviorIntakeHandler intake, VetAuditService audit)
    {
        _db = db;
        _intake = intake;
        _audit = audit;
    }

    public async Task<SubmitBehaviorIntakeResult> HandleAsync(SubmitBehaviorIntakeCommand command, CancellationToken ct = default)
    {
        var form = await _intake.HandleAsync(new GetBehaviorIntakeQuery(command.ClientId, command.CaseId), ct);
        if (form is null)
            return new SubmitBehaviorIntakeResult(SubmitBehaviorIntakeOutcome.NotFound, null, []);

        var dogIds = form.Dogs.Select(p => p.Id).ToHashSet();
        var selected = command.SelectedPetIds.Where(dogIds.Contains).Distinct().ToList();
        SubmitBehaviorIntakeResult Stop(SubmitBehaviorIntakeOutcome outcome) => new(outcome, form, selected);

        if (selected.Count == 0)
            return Stop(SubmitBehaviorIntakeOutcome.NoDogSelected);
        if (string.IsNullOrWhiteSpace(command.ProblemType))
            return Stop(SubmitBehaviorIntakeOutcome.NoProblem);
        if (string.IsNullOrWhiteSpace(command.Frequency))
            return Stop(SubmitBehaviorIntakeOutcome.NoFrequency);

        var behaviorCase = form.Case;
        BehaviorCasePets.SetSelectedIds(behaviorCase, selected);
        behaviorCase.ProblemType = command.ProblemType.Trim();
        behaviorCase.Frequency = command.Frequency.Trim();
        behaviorCase.ContextNotes = command.ContextNotes?.Trim();
        behaviorCase.ClinicalRedFlag = command.HasClinicalConcern;
        behaviorCase.VideoUrl = null;

        if (behaviorCase.ClinicalRedFlag)
        {
            behaviorCase.Status = BehaviorCaseStatus.ReferredToVet;
            await _db.TouchAsync(behaviorCase, ct);
            await _audit.LogAsync("behavior_referred_vet", command.ClientId, "BehaviorCase", behaviorCase.Id, ct: ct);
            return Stop(SubmitBehaviorIntakeOutcome.ReferredToVet);
        }

        behaviorCase.Status = BehaviorCaseStatus.IntakeComplete;
        await _db.TouchAsync(behaviorCase, ct);
        return Stop(SubmitBehaviorIntakeOutcome.Completed);
    }
}
