using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Business.Explore;

public class AppointmentsModel : ExplorePageModel
{
    private readonly IStringLocalizer<SharedResource> _L;

    public AppointmentsModel(IStringLocalizer<SharedResource> L) => _L = L;

    public IReadOnlyList<BusinessExploreDemo.DemoAppointment> Items { get; private set; } = Array.Empty<BusinessExploreDemo.DemoAppointment>();

    public IActionResult OnGet(string tab = "requests")
    {
        PrepareExplore("appointments");
        Tab = string.IsNullOrWhiteSpace(tab) ? "requests" : tab;
        Items = BusinessExploreDemo.Appointments(Tab);
        return Page();
    }

    public string StatusLabel(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Confirmed => _L["Appt_Status_Confirmed"].Value,
        AppointmentStatus.Completed => _L["Appt_Status_Completed"].Value,
        AppointmentStatus.Cancelled => _L["Appt_Status_Cancelled"].Value,
        _ => _L["Appt_Status_Pending"].Value
    };

    public string StatusBadge(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Confirmed => "badge-green",
        AppointmentStatus.Completed => "badge-purple",
        AppointmentStatus.Cancelled => "badge-gray",
        _ => "badge-orange"
    };
}
