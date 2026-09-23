using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Pages.Shared;
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

    [BindProperty(SupportsGet = true)]
    public int ProviderId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Slot { get; set; }

    /// <summary>Legacy When=hoy|manana; normalized into <see cref="Date"/>.</summary>
    [BindProperty(SupportsGet = true)]
    public string? When { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Date { get; set; }

    [BindProperty] public bool AcceptTerms { get; set; }
    [BindProperty] public bool AcceptScope { get; set; } = true;
    [BindProperty] public int? PaymentMethodId { get; set; }

    public BehaviorCase? Case { get; set; }
    public List<Pet> SelectedPets { get; set; } = new();
    public ServiceCatalogItem? CatalogItem { get; set; }
    public List<GroomerProfile> Providers { get; set; } = new();
    public List<PaymentMethod> Payments { get; set; } = new();
    public Dictionary<int, string> DistanceLabels { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public HashSet<string> OccupiedSlots { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> PastSlots { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> TimeSlots { get; } = new()
    {
        "9:00 AM", "10:00 AM", "10:30 AM", "1:00 PM", "2:00 PM", "3:00 PM", "5:00 PM"
    };

    public bool HasDate => BookingDate.TryParseSelected(Date, out _);
    public bool HasSlot => BookingTime.IsSlotAvailable(Slot, TimeSlots, PastSlots, OccupiedSlots);
    public bool HasProvider => ProviderId > 0 && Providers.Any(p => p.Id == ProviderId);

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Behavior/Providers?caseId={CaseId}" });

        Case = await _flow.GetOwnedAsync(CaseId);
        if (Case is null) return RedirectToPage("/Care/Services");
        if (Case.Status == BehaviorCaseStatus.ReferredToVet)
            return RedirectToPage("/Vet/Index");
        if (Case.Status < BehaviorCaseStatus.IntakeComplete)
            return RedirectToPage("/Behavior/Intake", new { caseId = CaseId });
        if (Case.Status >= BehaviorCaseStatus.Scheduled)
            return RedirectToPage("/Behavior/Summary", new { id = CaseId });

        if (!await LoadCatalogAsync()) return RedirectToPage("/Care/Services");
        await LoadSelectedPetsAsync();
        if (ProviderId > 0 && !Providers.Any(p => p.Id == ProviderId))
            ProviderId = 0;

        await RefreshScheduleStateAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (_auth.CurrentUserId is null) return RedirectToPage("/Account/Login");

        Case = await _flow.GetOwnedAsync(CaseId);
        if (Case is null) return RedirectToPage("/Care/Services");
        if (Case.Status == BehaviorCaseStatus.ReferredToVet)
            return RedirectToPage("/Vet/Index");
        if (Case.Status < BehaviorCaseStatus.IntakeComplete)
            return RedirectToPage("/Behavior/Intake", new { caseId = CaseId });
        if (Case.Status >= BehaviorCaseStatus.Scheduled)
            return RedirectToPage("/Behavior/Summary", new { id = CaseId });

        if (!await LoadCatalogAsync()) return RedirectToPage("/Care/Services");
        await LoadSelectedPetsAsync();
        await RefreshScheduleStateAsync();

        if (!AcceptTerms)
        {
            ErrorMessage = CatalogLocalizer.Loc(
                "Debes aceptar los términos para reservar.",
                "You must accept the terms to book.");
            return Page();
        }

        AcceptScope = true;

        if (!HasDate)
        {
            ErrorMessage = CatalogLocalizer.Loc("Elige el día.", "Choose the day.");
            return Page();
        }

        if (!HasSlot)
        {
            ErrorMessage = CatalogLocalizer.Loc("Elige un horario.", "Choose a time.");
            return Page();
        }

        if (!HasProvider)
        {
            ErrorMessage = CatalogLocalizer.Loc("Selecciona un especialista.", "Select a specialist.");
            return Page();
        }

        if (!TryResolveSchedule(ProviderId, out var scheduledAt, out var scheduleError))
        {
            ErrorMessage = scheduleError;
            await RefreshScheduleStateAsync();
            return Page();
        }

        if (SelectedPets.Count == 0 || CatalogItem is null)
        {
            ErrorMessage = CatalogLocalizer.Loc("Faltan datos de la evaluación.", "Evaluation details are incomplete.");
            return Page();
        }

        Case.ProviderId = ProviderId;
        Case.ScheduledAt = scheduledAt;

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

        var notes = $"Behavior case #{Case.Id}: {Case.ProblemType}";
        if (!string.IsNullOrWhiteSpace(Case.Frequency))
            notes += $" · {Case.Frequency}";
        if (!string.IsNullOrWhiteSpace(Case.ContextNotes))
            notes += $". {Case.ContextNotes}";
        if (SelectedPets.Count > 1)
            notes += $" · pets: {string.Join(", ", SelectedPets.Select(p => p.Name))}";

        Appointment? primaryAppt = null;
        foreach (var pet in SelectedPets)
        {
            var appt = new Appointment
            {
                ClientId = _auth.CurrentUserId.Value,
                PetId = pet.Id,
                GroomerId = Case.ProviderId.Value,
                ServiceId = service.Id,
                ScheduledAt = Case.ScheduledAt.Value,
                Status = AppointmentStatus.Pending,
                TotalPrice = CatalogItem.Price,
                DepositPaid = CatalogItem.Price,
                Notes = notes,
                CreatedAt = DateTime.UtcNow
            };
            _db.Appointments.Add(appt);
            primaryAppt ??= appt;
        }
        await _db.SaveChangesAsync();

        await _consent.SaveAsync(_auth.CurrentUserId.Value, null, new[]
        {
            (ConsentService.DocTerms, true),
            ("behavior_scope", true)
        }, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());

        Case.AppointmentId = primaryAppt!.Id;
        Case.PriceCharged = CatalogItem.Price * SelectedPets.Count;
        Case.Status = BehaviorCaseStatus.Scheduled;
        Case.Goals = CatalogLocalizer.Loc(
            $"Reducir: {Case.ProblemType}",
            $"Reduce: {Case.ProblemType}");
        Case.Exercises = CatalogLocalizer.Loc(
            "El especialista definirá los ejercicios en la sesión.",
            "The specialist will define exercises in the session.");
        Case.FollowUpNotes = Case.ContextNotes;
        await _flow.TouchAsync(Case);

        await _audit.LogAsync("behavior_booked", _auth.CurrentUserId, "BehaviorCase", Case.Id,
            new
            {
                price = Case.PriceCharged,
                providerId = Case.ProviderId,
                appointmentId = primaryAppt.Id,
                petIds = SelectedPets.Select(p => p.Id).ToArray(),
                scheduledAt
            });

        return RedirectToPage("/Behavior/Summary", new { id = Case.Id });
    }

    private async Task LoadSelectedPetsAsync()
    {
        SelectedPets.Clear();
        if (Case is null || _auth.CurrentUserId is null) return;

        var ids = BehaviorFlowService.GetSelectedPetIds(Case);
        if (ids.Count == 0) return;

        var pets = await _db.Pets.AsNoTracking()
            .Where(p => p.OwnerId == _auth.CurrentUserId
                        && ids.Contains(p.Id)
                        && p.Species == PetSpecies.Dog)
            .ToListAsync();
        SelectedPets = ids
            .Select(id => pets.FirstOrDefault(p => p.Id == id))
            .Where(p => p is not null)
            .Cast<Pet>()
            .ToList();
    }

    private async Task<bool> LoadCatalogAsync()
    {
        CatalogItem = await _catalog.GetAsync(ServiceCatalogCodes.BehaviorSession);
        Providers = BusinessMarketResolver.FilterHomeMarket(
                await _db.Groomers.AsNoTracking()
                    .Where(g => g.IsActive && g.PublishStatus == BusinessPublishStatus.Approved && g.VetProviderKind == VetProviderKind.BehaviorSpecialist)
                    .OrderByDescending(g => g.Rating)
                    .ToListAsync(),
                AppTimeZones.CurrentCountryCode)
            .Take(30)
            .ToList();

        Payments = await _db.PaymentMethods.AsNoTracking()
            .Where(p => p.UserId == _auth.CurrentUserId)
            .OrderByDescending(p => p.IsDefault)
            .ToListAsync();

        await FillDistanceLabelsAsync();

        return true;
    }

    private async Task FillDistanceLabelsAsync()
    {
        DistanceLabels.Clear();
        if (_auth.CurrentUserId is null || Providers.Count == 0) return;

        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == _auth.CurrentUserId.Value);
        if (user?.Latitude is not double ulat || user.Longitude is not double ulng)
            return;

        foreach (var p in Providers)
        {
            if (p.Latitude == 0 && p.Longitude == 0) continue;
            var km = GeoHelper.KmBetween(ulat, ulng, p.Latitude, p.Longitude);
            var label = GeoHelper.FormatDistanceOrPlace(km, p.City);
            if (!string.IsNullOrEmpty(label))
                DistanceLabels[p.Id] = label;
        }
    }

    private async Task RefreshScheduleStateAsync()
    {
        (When, Date) = BookingDate.NormalizeFromLegacy(When, Date);
        if (!string.IsNullOrWhiteSpace(Slot) &&
            !TimeSlots.Contains(Slot, StringComparer.OrdinalIgnoreCase))
            Slot = null;

        var day = ResolveDay();
        PastSlots = BookingTime.MarkPastSlots(TimeSlots, day);
        OccupiedSlots.Clear();

        if (HasDate && ProviderId > 0)
            await LoadOccupiedSlotsAsync(ProviderId, day);

        if (!string.IsNullOrWhiteSpace(Slot) &&
            (PastSlots.Contains(Slot) || OccupiedSlots.Contains(Slot)))
            Slot = null;
    }

    private async Task LoadOccupiedSlotsAsync(int providerId, DateTime day)
    {
        OccupiedSlots.Clear();
        var from = AppTimeZones.LocalDateAndTimeToUtc(day, TimeSpan.Zero);
        var to = AppTimeZones.LocalDateAndTimeToUtc(day.AddDays(1), TimeSpan.Zero);
        var taken = await _db.Appointments.AsNoTracking()
            .Where(a => a.GroomerId == providerId
                        && a.Status != AppointmentStatus.Cancelled
                        && a.ScheduledAt >= from
                        && a.ScheduledAt < to)
            .Select(a => a.ScheduledAt)
            .ToListAsync();

        foreach (var utc in taken)
        {
            var local = AppTimeZones.ToAppLocal(utc);
            foreach (var label in TimeSlots)
            {
                if (!AppTimeZones.TryParseSlotToTimeSpan(label, out var slotTod)) continue;
                if (slotTod == local.TimeOfDay)
                    OccupiedSlots.Add(label);
            }
        }
    }

    private DateTime ResolveDay()
    {
        if (BookingDate.TryParseSelected(Date, out var day))
            return day;
        return AppTimeZones.TodayLocalDate();
    }

    private bool TryResolveSchedule(int providerId, out DateTime startUtc, out string? error)
    {
        error = null;
        if (!BookingDate.TryParseSelected(Date, out var day))
        {
            error = CatalogLocalizer.Loc("Elige el día.", "Choose the day.");
            startUtc = default;
            return false;
        }

        if (string.IsNullOrWhiteSpace(Slot) || !AppTimeZones.TryParseSlotToTimeSpan(Slot, out var tod))
        {
            error = CatalogLocalizer.Loc("Elige un horario.", "Choose a time.");
            startUtc = default;
            return false;
        }

        startUtc = AppTimeZones.LocalDateAndTimeToUtc(day, tod);
        if (startUtc <= DateTime.UtcNow)
        {
            error = CatalogLocalizer.Loc(
                "No puedes elegir una fecha u hora en el pasado.",
                "You can't select a past date or time.");
            return false;
        }

        var candidateUtc = startUtc;
        var busy = _db.Appointments.AsNoTracking().Any(a =>
            a.GroomerId == providerId
            && a.Status != AppointmentStatus.Cancelled
            && a.ScheduledAt == candidateUtc);
        if (busy)
        {
            error = CatalogLocalizer.Loc(
                "Ese horario ya no está disponible. Elige otro día u hora.",
                "That time is no longer available. Choose another day or time.");
            return false;
        }

        return true;
    }

    public static string RoleLabel(BehaviorSpecialistRole? role) => role switch
    {
        BehaviorSpecialistRole.Trainer => CatalogLocalizer.Loc("Entrenador", "Trainer"),
        BehaviorSpecialistRole.VeterinaryBehaviorist => CatalogLocalizer.Loc("Veterinario conductista", "Veterinary behaviorist"),
        _ => CatalogLocalizer.Loc("Consultor de conducta", "Behavior consultant")
    };
}
