using WebAppPet.Models;

namespace WebAppPet.Application.Consultations.EscalateToEmergency;

/// <param name="UserId">Null for visitors who are not signed in.</param>
/// <param name="ConsultationId">Consultation to mark as escalated, when coming from the flow.</param>
/// <param name="HomeCountry">Market whose clinics are listed.</param>
public sealed record EscalateToEmergencyCommand(int? UserId, int? ConsultationId, string HomeCountry);

/// <param name="Clinics">24/7 vet clinics, or any vet clinic when none offers emergencies.</param>
public sealed record EmergencyOptions(List<GroomerProfile> Clinics, bool HasCareMembership);
