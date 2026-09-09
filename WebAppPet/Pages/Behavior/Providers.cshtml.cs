using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Behavior;

public class ProvidersModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly BehaviorFlowService _flow;
    private readonly ServiceCatalogService _catalog;
    private readonly ConsentService _consent;
    private readonly VetAuditService _audit;

    public ProvidersModel(
        AppDbContext db,
        AuthService auth,
        BehaviorFlowService flow,
        ServiceCatalogService catalog,
        ConsentService consent,
        VetAuditService audit)
    {
        _db = db;
        _auth = auth;
        _flow = flow;
        _catalog = catalog;
        _consent = consent;
        _audit = audit;
    }

    [BindProperty(SupportsGet = true)]
    public int CaseId { get; set; }

    [BindProperty] public int ProviderId { get; set; }
    [BindProperty] public string Slot { get; set; } = "10:30 AM";
    [BindProperty] public string When { get; set; } = "hoy";
    [BindProperty] public bool AcceptTerms { get; set; }
    [BindProperty] public bool AcceptScope { get; set; }
    [BindProperty] public int? PaymentMethodId { get; set; }
    [BindProperty] public string Step { get; set; } = "pick";

    public BehaviorCase? Case { get; set; }
    public ServiceCatalogItem? CatalogItem { get; set; }
    public List<GroomerProfile> Providers { get; set; } = new();
    public List<PaymentMethod> Payments { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public List<string> TimeSlots { get; } = new()
    {
        "9:00 AM", "10:00 AM", "10:30 AM", "1:00 PM", "2:00 PM", "3:00 PM", "5:00 PM"
    };

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Behavior/Providers?caseId={CaseId}" });

        if (!await LoadAsync()) return RedirectToPage("/Care/Services");
        Step = "pick";
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (_auth.CurrentUserId is null) return RedirectToPage("/Account/Login");
        if (!await LoadAsync()) return RedirectToPage("/Care/Services");

        if (Step == "pick")
        {
            if (ProviderId <= 0 || !Providers.Any(p => p.Id == ProviderId))
            {
                ErrorMessage = CatalogLocalizer.Loc("Selecciona un especialista.", "Select a specialist.");
                return Page();
            }
            if (!AppTimeZones.TryParseSlotToTimeSpan(Slot, out var tod))
            {
                ErrorMessage = CatalogLocalizer.Loc("Elige un horario.", "Choose a time.");
                return Page();
            }

            var day = AppTimeZones.TodayLocalDate();
            if (string.Equals(When, "mañana", StringComparison.OrdinalIgnoreCase))
                day = day.AddDays(1);

            Case!.ProviderId = ProviderId;
            Case.ScheduledAt = AppTimeZones.LocalDateAndTimeToUtc(day, tod);
            Case.Status = BehaviorCaseStatus.ProviderSelected;
            await _flow.TouchAsync(Case);
            Step = "pay";
            return Page();
        }

        // pay
        if (!AcceptTerms || !AcceptScope)
        {
            ErrorMessage = CatalogLocalizer.Loc("Debes aceptar los términos y el alcance.", "You must accept terms and scope.");
            Step = "pay";
            return Page();
        }

        if (Case!.ProviderId is null || Case.PetId is null || Case.ScheduledAt is null || CatalogItem is null)
        {
            ErrorMessage = CatalogLocalizer.Loc("Faltan datos de la reserva.", "Booking details are incomplete.");
            Step = "pick";
            return Page();
        }

        var service = await _db.Services.FirstOrDefaultAsync(s => s.GroomerId == Case.ProviderId)
            ?? new GroomerService
            {
                GroomerId = Case.ProviderId.Value,
                Name = CatalogItem.NameEs,
                Description = CatalogItem.ScopeEs,
                DurationMinutes = CatalogItem.DurationMinutes,
                PriceSmall = CatalogItem.Price,
                PriceMedium = CatalogItem.Price,
                PriceLarge = CatalogItem.Price,
                PriceGiant = CatalogItem.Price
            };
        if (service.Id == 0)
        {
            _db.Services.Add(service);
            await _db.SaveChangesAsync();
        }

        var appt = new Appointment
        {
            ClientId = _auth.CurrentUserId.Value,
            PetId = Case.PetId.Value,
            GroomerId = Case.ProviderId.Value,
            ServiceId = service.Id,
            ScheduledAt = Case.ScheduledAt.Value,
            Status = AppointmentStatus.Pending,
            TotalPrice = CatalogItem.Price,
            DepositPaid = CatalogItem.Price,
            Notes = $"Behavior case #{Case.Id}: {Case.ProblemType}. {Case.ContextNotes}",
            CreatedAt = DateTime.UtcNow
        };
        _db.Appointments.Add(appt);
        await _db.SaveChangesAsync();

        await _consent.SaveAsync(_auth.CurrentUserId.Value, null, new[]
        {
            (ConsentService.DocTerms, AcceptTerms),
            ("behavior_scope", AcceptScope)
        }, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());

        Case.AppointmentId = appt.Id;
        Case.PriceCharged = CatalogItem.Price;
        Case.Status = BehaviorCaseStatus.Scheduled;
        Case.Goals = CatalogLocalizer.Loc(
            $"Reducir: {Case.ProblemType}",
            $"Reduce: {Case.ProblemType}");
        Case.Exercises = CatalogLocalizer.Loc(
            "Pendiente de definir con el especialista en la sesión.",
            "To be defined with the specialist in the session.");
        await _flow.TouchAsync(Case);

        await _audit.LogAsync("behavior_booked", _auth.CurrentUserId, "BehaviorCase", Case.Id,
            new { price = CatalogItem.Price, providerId = Case.ProviderId });

        return RedirectToPage("/Behavior/Summary", new { id = Case.Id });
    }

    private async Task<bool> LoadAsync()
    {
        Case = await _flow.GetOwnedAsync(CaseId);
        if (Case is null || Case.Status < BehaviorCaseStatus.IntakeComplete)
            return false;
        if (Case.Status == BehaviorCaseStatus.ReferredToVet)
            return false;

        CatalogItem = await _catalog.GetAsync(ServiceCatalogCodes.BehaviorSession);
        Providers = await _db.Groomers.AsNoTracking()
            .Where(g => g.IsActive && g.PublishStatus == BusinessPublishStatus.Approved && g.VetProviderKind == VetProviderKind.BehaviorSpecialist)
            .OrderByDescending(g => g.Rating)
            .Take(30)
            .ToListAsync();

        Payments = await _db.PaymentMethods.AsNoTracking()
            .Where(p => p.UserId == _auth.CurrentUserId)
            .OrderByDescending(p => p.IsDefault)
            .ToListAsync();

        if (Case.Status >= BehaviorCaseStatus.ProviderSelected && Case.ProviderId != null)
            Step = "pay";

        return true;
    }

    public static string RoleLabel(BehaviorSpecialistRole? role) => role switch
    {
        BehaviorSpecialistRole.Trainer => CatalogLocalizer.Loc("Entrenador", "Trainer"),
        BehaviorSpecialistRole.VeterinaryBehaviorist => CatalogLocalizer.Loc("Veterinario conductista", "Veterinary behaviorist"),
        _ => CatalogLocalizer.Loc("Consultor de conducta", "Behavior consultant")
    };
}
