using WebAppPet.Application.Behavior.Shared;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;

namespace WebAppPet.Application.Behavior.SaveBehaviorFollowUp;

/// <summary>
/// Saves the client's follow-up notes, activates the plan when it was not active yet and notifies
/// the client. Returns the updated case, or null when it is not theirs.
/// </summary>
public class SaveBehaviorFollowUpHandler
{
    private readonly AppDbContext _db;

    public SaveBehaviorFollowUpHandler(AppDbContext db) => _db = db;

    public async Task<BehaviorCase?> HandleAsync(SaveBehaviorFollowUpCommand command, CancellationToken ct = default)
    {
        var behaviorCase = await _db.OwnedBehaviorCaseAsync(command.ClientId, command.CaseId, ct);
        if (behaviorCase is null)
            return null;

        behaviorCase.FollowUpNotes = command.FollowUpNotes?.Trim();
        if (behaviorCase.Status < BehaviorCaseStatus.PlanActive)
            behaviorCase.Status = BehaviorCaseStatus.PlanActive;

        _db.Notifications.Add(new AppNotification
        {
            UserId = command.ClientId,
            Title = CatalogLocalizer.Loc("Plan de conducta", "Behavior plan"),
            Message = CatalogLocalizer.Loc(
                $"Nota guardada para {behaviorCase.Pet?.Name}.",
                $"Note saved for {behaviorCase.Pet?.Name}."),
            Type = "behavior-plan",
            CreatedAt = DateTime.UtcNow
        });
        await _db.TouchAsync(behaviorCase, ct);
        return behaviorCase;
    }
}
