using WebAppPet.Domain;


namespace WebAppPet.Application.Behavior.Shared;

/// <summary>A page of the behavior flow; the page model maps it to its route.</summary>
public enum BehaviorStep
{
    CareServices,
    VetHome,
    Intake,
    Providers,
    Summary
}

public static class BehaviorFlow
{
    /// <summary>
    /// Where a case that is not ready to pick a specialist belongs: missing cases go back to Care,
    /// clinical referrals to the vet, unfinished intakes to the intake and booked cases to the summary.
    /// Null when the case can pick a specialist.
    /// </summary>
    public static BehaviorStep? RedirectBeforeBooking(BehaviorCase? behaviorCase) => behaviorCase switch
    {
        null => BehaviorStep.CareServices,
        { Status: BehaviorCaseStatus.ReferredToVet } => BehaviorStep.VetHome,
        { Status: < BehaviorCaseStatus.IntakeComplete } => BehaviorStep.Intake,
        { Status: >= BehaviorCaseStatus.Scheduled } => BehaviorStep.Summary,
        _ => null
    };
}
