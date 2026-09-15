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
    [BindProperty] public bool AcceptScope { get; set; } = true;
    [BindProperty] public int? PaymentMethodId { get; set; }

    public BehaviorCase? Case { get; set; }
    public List<Pet> SelectedPets { get; set; } = new();
    public ServiceCatalogItem? CatalogItem { get; set; }
    public List<GroomerProfile> Providers { get; set; } = new();
    public List<PaymentMethod> Payments { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public HashSet<string> OccupiedSlots { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> TimeSlots { get; } = new()
    {
        "9:00 AM", "10:00 AM", "10:30 AM", "1:00 PM", "2:00 PM", "3:00 PM", "5:00 PM"
    };

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
        if (ProviderId <= 0 && Providers.Count > 0)
            ProviderId = Providers[0].Id;

        await RefreshSlotAvailabilityAsync();
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

        if (!AcceptTerms)
        {
            ErrorMessage = CatalogLocalizer.Loc(
                "Debes aceptar los términos para reservar.",
                "You must accept the terms to book.");
            await RefreshSlotAvailabilityAsync();
            return Page();
        }

        AcceptScope = true;

        if (ProviderId <= 0 || !Providers.Any(p => p.Id == ProviderId))
        {
            ErrorMessage = CatalogLocalizer.Loc("Selecciona un especialista.", "Select a specialist.");
            await RefreshSlotAvailabilityAsync();
            return Page();
        }

        if (!TryResolveSchedule(ProviderId, out var scheduledAt, out var scheduleError))
        {
            ErrorMessage = scheduleError;
            await RefreshSlotAvailabilityAsync();
            return Page();
        }

        if (SelectedPets.Count == 0 || CatalogItem is null)
        {
            ErrorMessage = CatalogLocalizer.Loc("Faltan datos de la evaluación.", "Evaluation details are incomplete.");
            await RefreshSlotAvailabilityAsync();
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
        Providers = await _db.Groomers.AsNoTracking()
            .Where(g => g.IsActive && g.PublishStatus == BusinessPublishStatus.Approved && g.VetProviderKind == VetProviderKind.BehaviorSpecialist)
            .OrderByDescending(g => g.Rating)
            .Take(30)
            .ToListAsync();

        Payments = await _db.PaymentMethods.AsNoTracking()
            .Where(p => p.UserId == _auth.CurrentUserId)
            .OrderByDescending(p => p.IsDefault)
            .ToListAsync();

        if (Case?.ProviderId is int pid && Providers.Any(p => p.Id == pid))
            ProviderId = pid;

        return true;
    }

    private async Task RefreshSlotAvailabilityAsync()
    {
        NormalizeWhen();
        if (!TimeSlots.Contains(Slot, StringComparer.OrdinalIgnoreCase))
            Slot = TimeSlots[0];

        if (ProviderId <= 0) return;

        var day = ResolveDay();
        OccupiedSlots.Clear();
        var from = AppTimeZones.LocalDateAndTimeToUtc(day, TimeSpan.Zero);
        var to = AppTimeZones.LocalDateAndTimeToUtc(day.AddDays(1), TimeSpan.Zero);
        var taken = await _db.Appointments.AsNoTracking()
            .Where(a => a.GroomerId == ProviderId
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

        if (OccupiedSlots.Contains(Slot))
        {
            var free = TimeSlots.FirstOrDefault(t => !OccupiedSlots.Contains(t));
            if (free != null) Slot = free;
        }
    }

    private void NormalizeWhen()
    {
        When = When?.Trim().ToLowerInvariant() switch
        {
            "hoy" or "today" => "hoy",
            "manana" or "mañana" or "tomorrow" => "manana",
            _ => "hoy"
        };
    }

    private DateTime ResolveDay()
    {
        var today = AppTimeZones.TodayLocalDate();
        return When == "manana" ? today.AddDays(1) : today;
    }

    private bool TryResolveSchedule(int providerId, out DateTime startUtc, out string? error)
    {
        error = null;
        NormalizeWhen();
        var day = ResolveDay();
        if (!AppTimeZones.TryParseSlotToTimeSpan(Slot, out _))
        {
            error = CatalogLocalizer.Loc("Elige un horario.", "Choose a time.");
            startUtc = default;
            return false;
        }

        var preferredIndex = TimeSlots.FindIndex(t => string.Equals(t, Slot, StringComparison.OrdinalIgnoreCase));
        if (preferredIndex < 0) preferredIndex = 0;

        for (var dayOffset = 0; dayOffset < 7; dayOffset++)
        {
            var tryDay = day.AddDays(dayOffset);
            var ordered = dayOffset == 0
                ? TimeSlots.Skip(preferredIndex).ToList()
                : TimeSlots;

            foreach (var t in ordered)
            {
                if (!AppTimeZones.TryParseSlotToTimeSpan(t, out var slotTod)) continue;
                var candidate = AppTimeZones.LocalDateAndTimeToUtc(tryDay, slotTod);
                if (candidate <= DateTime.UtcNow) continue;
                var busy = _db.Appointments.AsNoTracking().Any(a =>
                    a.GroomerId == providerId
                    && a.Status != AppointmentStatus.Cancelled
                    && a.ScheduledAt == candidate);
                if (busy) continue;

                startUtc = candidate;
                Slot = t;
                When = tryDay.Date == AppTimeZones.TodayLocalDate() ? "hoy" : "manana";
                return true;
            }
        }

        error = CatalogLocalizer.Loc(
            "Ese horario ya no está disponible. Elige otro día u hora.",
            "That time is no longer available. Choose another day or time.");
        startUtc = default;
        return false;
    }

    public static string RoleLabel(BehaviorSpecialistRole? role) => role switch
    {
        BehaviorSpecialistRole.Trainer => CatalogLocalizer.Loc("Entrenador", "Trainer"),
        BehaviorSpecialistRole.VeterinaryBehaviorist => CatalogLocalizer.Loc("Veterinario conductista", "Veterinary behaviorist"),
        _ => CatalogLocalizer.Loc("Consultor de conducta", "Behavior consultant")
    };
}
