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

public static class VetDisplay
{
    public static string ConsultationTitle(Consultation c)
    {
        if (!string.IsNullOrWhiteSpace(c.Provider?.BusinessName))
            return c.Provider.BusinessName!;
        if (!string.IsNullOrWhiteSpace(c.ResponsibleName))
            return c.ResponsibleName!;

        return c.Modality switch
        {
            VetModality.Emergency => Loc("Emergencia", "Emergency"),
            VetModality.Clinic => Loc("Consulta en clínica", "Clinic consult"),
            _ => Loc("Consulta virtual", "Virtual consult")
        };
    }

    public static string StatusLabel(ConsultationStatus s) => s switch
    {
        ConsultationStatus.Draft => Loc("Borrador", "Draft"),
        ConsultationStatus.SafetyScreened => Loc("Seguridad revisada", "Safety screened"),
        ConsultationStatus.EligibilityVerified => Loc("Elegibilidad verificada", "Eligibility verified"),
        ConsultationStatus.ProviderSelected => Loc("Profesional elegido", "Provider selected"),
        ConsultationStatus.PaymentAuthorized => Loc("Pago autorizado", "Payment authorized"),
        ConsultationStatus.Scheduled => Loc("Agendada", "Scheduled"),
        ConsultationStatus.InProgress => Loc("En curso", "In progress"),
        ConsultationStatus.Completed => Loc("Completada", "Completed"),
        ConsultationStatus.FollowUpOpen => Loc("Seguimiento abierto", "Follow-up open"),
        ConsultationStatus.Closed => Loc("Cerrada", "Closed"),
        ConsultationStatus.EscalatedToEmergency => Loc("Derivada a emergencia", "Escalated to emergency"),
        _ => s.ToString()
    };

    public static DateTime SortAt(Consultation c) =>
        c.ScheduledAt ?? c.UpdatedAt;

    private static string Loc(string spanish, string english) =>
        Localization.CatalogLocalizer.Loc(spanish, english);
}
