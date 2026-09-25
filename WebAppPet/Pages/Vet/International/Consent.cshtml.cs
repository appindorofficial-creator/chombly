using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Businesses.Shared;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Pages.Shared;
using WebAppPet.Services;

namespace WebAppPet.Pages.Vet.International;

public class ConsentModel : PageModel
{
    private readonly AuthService _auth;
    private readonly ConsultationFlowService _flow;
    private readonly ServiceCatalogService _catalog;
    private readonly ConsentService _consent;
    private readonly ChomblyCareService _care;
    private readonly AvailabilityService _availability;
    private readonly AppDbContext _db;
    private readonly VetAuditService _audit;

    public ConsentModel(
        AuthService auth,
        ConsultationFlowService flow,
        ServiceCatalogService catalog,
        ConsentService consent,
        ChomblyCareService care,
        AvailabilityService availability,
        AppDbContext db,
        VetAuditService audit)
    {
        _auth = auth;
        _flow = flow;
        _catalog = catalog;
        _consent = consent;
        _care = care;
        _availability = availability;
        _db = db;
        _audit = audit;
    }

    [BindProperty(SupportsGet = true)]
    public int ConsultationId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? When { get; set; }

    [BindProperty] public bool AcceptScope { get; set; }
    [BindProperty] public string? Slot { get; set; }

    public Consultation? Consultation { get; set; }
    public GroomerProfile? Provider { get; set; }
    public ServiceCatalogItem? CatalogItem { get; set; }
    public bool UsingCare { get; set; }
    public decimal ConsultPrice { get; set; } = 30m;
    public string? ErrorMessage { get; set; }
    public List<string> TimeSlots { get; set; } = new();
    public HashSet<string> PastSlots { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool DayOpen { get; set; } = true;
    /// <summary>Home market of the signed-in client (CO/US) — drives US-only disclaimer copy.</summary>
    public string HomeCountryCode { get; set; } = MarketCountry.DefaultIso;
    public bool HomeIsUnitedStates => MarketCountry.IsUnitedStates(HomeCountryCode);

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Vet/International/Consent?consultationId={ConsultationId}" });

        if (!await LoadAsync()) return RedirectToPage("/Vet/International/Matches", new { consultationId = ConsultationId });
        When ??= "hoy";
        await LoadSlotsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await LoadAsync()) return RedirectToPage("/Vet/International/Matches", new { consultationId = ConsultationId });
        await LoadSlotsAsync();

        if (!AcceptScope)
            return Page();

        if (string.IsNullOrWhiteSpace(When) ||
            (!string.Equals(When, "hoy", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(When, "mañana", StringComparison.OrdinalIgnoreCase)))
            return Page();

        var market = AppTimeZones.MarketFromCountry(Consultation?.ContextCountry);
        using var _tz = AppTimeZones.UseMarket(market);

        var day = AppTimeZones.TodayLocalDate(market);
        if (string.Equals(When, "mañana", StringComparison.OrdinalIgnoreCase))
            day = day.AddDays(1);

        if (!DayOpen || TimeSlots.Count == 0)
            return Page();

        if (!BookingTime.IsSlotAvailable(Slot, TimeSlots, PastSlots, new HashSet<string>()))
            return Page();

        if (!AppTimeZones.TryParseSlotToTimeSpan(Slot, out var tod))
            return Page();

        Consultation!.ScheduledAt = AppTimeZones.LocalDateAndTimeToUtc(day, tod, market);
        Consultation.UsesCareBenefit = UsingCare;
        Consultation.ServiceCatalogCode = ServiceCatalogCodes.VetIntl30;
        Consultation.Status = ConsultationStatus.ProviderSelected;
        await _flow.TouchAsync(Consultation);

        await _consent.SaveAsync(_auth.CurrentUserId!.Value, Consultation.Id, new[]
        {
            (ConsentService.DocIntlOrientation, AcceptScope)
        }, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());

        await _audit.LogAsync("schedule_selected", _auth.CurrentUserId, "Consultation", ConsultationId,
            new { when = When, slot = Slot, care = UsingCare });

        return RedirectToPage("/Vet/Virtual/Checkout", new { consultationId = ConsultationId });
    }

    private async Task<bool> LoadAsync()
    {
        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation?.ProviderId is null) return false;
        Provider = await _db.Groomers.AsNoTracking().FirstOrDefaultAsync(g => g.Id == Consultation.ProviderId);
        CatalogItem = await _catalog.GetAsync(ServiceCatalogCodes.VetIntl30);
        var catalogPrice = CatalogItem?.Price ?? 30m;
        ConsultPrice = Provider?.StartingPrice > 0 ? Provider.StartingPrice : catalogPrice;
        UsingCare = Consultation.UsesCareBenefit;
        if (!UsingCare && _auth.CurrentUserId is int uid && await _care.HasQuickConsultAvailableAsync(uid))
        {
            UsingCare = true;
            Consultation.UsesCareBenefit = true;
            await _flow.TouchAsync(Consultation);
        }

        if (_auth.CurrentUserId is int userId)
        {
            var user = await _db.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.CountryCode, u.City, u.Latitude, u.Longitude })
                .FirstOrDefaultAsync();
            HomeCountryCode = MarketCountry.ResolveForUser(user?.CountryCode, user?.City, user?.Latitude, user?.Longitude);
        }

        return CatalogItem != null && Provider != null;
    }

    private async Task LoadSlotsAsync()
    {
        var market = AppTimeZones.MarketFromCountry(Consultation!.ContextCountry);
        using var _tz = AppTimeZones.UseMarket(market);

        var day = AppTimeZones.TodayLocalDate(market);
        if (string.Equals(When, "mañana", StringComparison.OrdinalIgnoreCase))
            day = day.AddDays(1);

        var providerId = Consultation.ProviderId!.Value;
        DayOpen = await _availability.IsAvailableOnAsync(providerId, day);

        var weekly = await _db.WeeklyHours.AsNoTracking()
            .FirstOrDefaultAsync(h => h.GroomerId == providerId && h.DayOfWeek == (int)day.DayOfWeek);

        if (weekly is { IsOpen: true })
        {
            TimeSlots = BuildSlotsFromWindow(weekly.OpenMinutes, weekly.CloseMinutes);
            DayOpen = true;
        }
        else if (DayOpen)
        {
            TimeSlots = BookingTime.DefaultSlots.ToList();
        }
        else
        {
            var hasAnyWeekly = await _db.WeeklyHours.AsNoTracking().AnyAsync(h => h.GroomerId == providerId);
            if (!hasAnyWeekly)
            {
                DayOpen = true;
                TimeSlots = BookingTime.DefaultSlots.ToList();
            }
            else
            {
                TimeSlots = new List<string>();
            }
        }

        PastSlots = BookingTime.MarkPastSlots(TimeSlots, day, market);
        // Do not auto-select a slot — user must choose before Continue enables.
        if (!string.IsNullOrWhiteSpace(Slot) &&
            (!TimeSlots.Contains(Slot, StringComparer.OrdinalIgnoreCase) || PastSlots.Contains(Slot)))
            Slot = null;
    }

    private static List<string> BuildSlotsFromWindow(int openMinutes, int closeMinutes)
    {
        if (openMinutes == closeMinutes)
            return BookingTime.DefaultSlots.ToList();

        var slots = new List<string>();
        var end = closeMinutes > openMinutes ? closeMinutes : openMinutes + 8 * 60;
        for (var m = openMinutes; m + 30 <= end; m += 30)
        {
            var ts = TimeSpan.FromMinutes(m % (24 * 60));
            var dt = DateTime.Today.Add(ts);
            slots.Add(dt.ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture));
        }

        return slots.Count > 0 ? slots : BookingTime.DefaultSlots.ToList();
    }
}
