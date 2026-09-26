using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.Consultations.EscalateToEmergency;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Vet;

public class EmergencyModel : PageModel
{
    private readonly AuthService _auth;
    private readonly EscalateToEmergencyHandler _escalate;

    public EmergencyModel(AuthService auth, EscalateToEmergencyHandler escalate)
    {
        _auth = auth;
        _escalate = escalate;
    }

    [BindProperty(SupportsGet = true)]
    public int? ConsultationId { get; set; }

    public List<GroomerProfile> Clinics { get; set; } = new();
    public bool HasCareMembership { get; set; }

    public async Task OnGetAsync()
    {
        var options = await _escalate.HandleAsync(
            new EscalateToEmergencyCommand(_auth.CurrentUserId, ConsultationId, AppTimeZones.CurrentCountryCode));
        Clinics = options.Clinics;
        HasCareMembership = options.HasCareMembership;
    }
}
