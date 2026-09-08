namespace WebAppPet.Models;

public enum VetModality
{
    Virtual = 0,
    Clinic = 1,
    Emergency = 2
}

public enum VetProviderKind
{
    None = 0,
    LocalVet = 1,
    InternationalAdvisor = 2,
    BehaviorSpecialist = 3
}

public enum BehaviorSpecialistRole
{
    Trainer = 0,
    Consultant = 1,
    VeterinaryBehaviorist = 2
}

public enum BehaviorCaseStatus
{
    Draft = 0,
    IntakeComplete = 1,
    ProviderSelected = 2,
    Scheduled = 3,
    PlanActive = 4,
    Closed = 5,
    ReferredToVet = 6
}

public enum ConsultationStatus
{
    Draft = 0,
    SafetyScreened = 1,
    EligibilityVerified = 2,
    ProviderSelected = 3,
    PaymentAuthorized = 4,
    Scheduled = 5,
    InProgress = 6,
    Completed = 7,
    FollowUpOpen = 8,
    Closed = 9,
    EscalatedToEmergency = 10
}

public static class ServiceCatalogCodes
{
    public const string VetLocal30 = "vet_local_30";
    public const string VetIntl30 = "vet_intl_30";
    public const string ChomblyCare = "chombly_care";
    public const string BehaviorSession = "behavior_session";
}
