using Microsoft.AspNetCore.Mvc;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Business.Explore;

public class AvailabilityModel : ExplorePageModel
{
    public IReadOnlyList<WeekDayInput> WeekEdit { get; private set; } = Array.Empty<WeekDayInput>();
    public IReadOnlyList<BusinessExploreDemo.DemoDaySlot> Days { get; private set; } = Array.Empty<BusinessExploreDemo.DemoDaySlot>();

    public IActionResult OnGet()
    {
        PrepareExplore("availability");
        WeekEdit = BusinessExploreDemo.Week;
        Days = BusinessExploreDemo.CalendarDays();
        return Page();
    }
}
