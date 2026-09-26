using WebAppPet.Application.Consultations.GetIntlSchedule;

namespace WebAppPet.Application.Consultations.ScheduleIntlConsultation;

/// <param name="When">"hoy" or "mañana".</param>
/// <param name="AcceptScope">The client accepted that international guidance is not an emergency or a local vet visit.</param>
public sealed record ScheduleIntlConsultationCommand(
    int ClientId,
    int ConsultationId,
    string? When,
    string? Slot,
    bool AcceptScope,
    string? IpAddress,
    string? UserAgent);

public enum ScheduleIntlConsultationOutcome
{
    NotFound,
    /// <summary>Scope not accepted, or the day or slot cannot be booked.</summary>
    Invalid,
    Scheduled
}

/// <param name="Schedule">The form to show again; null when not found.</param>
public sealed record ScheduleIntlConsultationResult(ScheduleIntlConsultationOutcome Outcome, IntlSchedule? Schedule);
