using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Models;

namespace WebAppPet.Application.Consultations.CheckEligibility;

public sealed record CheckEligibilityQuery(int ClientId, int ConsultationId);

/// <param name="Redirect">Set when the eligibility page does not apply to this client.</param>
public sealed record EligibilityCheck(ConsultationStep? Redirect, Consultation? Consultation);
