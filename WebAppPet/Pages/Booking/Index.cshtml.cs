using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using WebAppPet.Application.Bookings.CreateBooking;
using WebAppPet.Application.Bookings.Shared;
using WebAppPet.Application.Promotions.ApplyPromoCode;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Pages.Shared;
using WebAppPet.Services;

namespace WebAppPet.Pages.Booking;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly ApplyPromoCodeHandler _promo;
    private readonly CreateBookingHandler _createBooking;
    private readonly AvailabilityService _availability;
    private readonly IStringLocalizer<SharedResource> _L;

    public IndexModel(
        AppDbContext db,
        AuthService auth,
        ApplyPromoCodeHandler promo,
        CreateBookingHandler createBooking,
        AvailabilityService availability,
        IStringLocalizer<SharedResource> L)
    {
        _db = db;
        _auth = auth;
        _promo = promo;
        _createBooking = createBooking;
        _availability = availability;
        _L = L;
    }

    [BindProperty(SupportsGet = true)]
    public int GroomerId { get; set; }

    /// <summary>Optional popular-service name from Home → Groomers (pre-selects matching catalog service).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Service { get; set; }

    [BindProperty(SupportsGet = true)]
    public int ServiceId { get; set; }

    /// <summary>Selected catalog services (multi-select). Drives ServiceId primary for the appointment FK.</summary>
    [BindProperty(SupportsGet = true)]
    public List<int> ServiceIds { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int PetId { get; set; }

    /// <summary>Selected pets for this booking (multi-select). Drives PetId primary.</summary>
    [BindProperty(SupportsGet = true)]
    public List<int> PetIds { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string Date { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string EndDate { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string Time { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? Notes { get; set; }

    [BindProperty(SupportsGet = true)]
    public List<int> SelectedExtraIds { get; set; } = new();

    /// <summary>When true, show the full-page checkout instead of the configure form.</summary>
    [BindProperty(SupportsGet = true)]
    public bool Pay { get; set; }

    [BindProperty]
    public string? PromoCode { get; set; }

    [BindProperty]
    public bool AcceptTerms { get; set; }

    [BindProperty]
    public int? PaymentMethodId { get; set; }

    public GroomerProfile? Groomer { get; set; }
    public bool IsOvernight { get; set; }
    public List<GroomerService> Services { get; set; } = new();
    public List<ServiceExtra> Extras { get; set; } = new();
    public List<Pet> Pets { get; set; } = new();
    public List<string> TimeSlots { get; } = new()
    {
        "9:00 AM", "10:00 AM", "11:00 AM", "12:00 PM", "1:00 PM", "2:00 PM", "3:00 PM", "4:00 PM", "5:00 PM"
    };

    public HashSet<string> PastSlots { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> OccupiedSlots { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> OutsideHoursSlots { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Slots that can still be booked for the selected date.</summary>
    public List<string> BookableTimeSlots { get; private set; } = new();

    public bool DayIsOpen { get; private set; } = true;
    public string? HoursLabel { get; private set; }

    public GroomerService? SelectedService { get; set; }
    public List<GroomerService> SelectedServices { get; set; } = new();
    public Pet? SelectedPet { get; set; }
    public List<Pet> SelectedPets { get; set; } = new();
    public List<ServiceExtra> SelectedExtras { get; set; } = new();
    public int Nights { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal EstimatedTotal { get; set; }
    public decimal Deposit { get; set; }
    public string? PromoError { get; set; }
    public string? ErrorMessage { get; set; }

    public PaymentMethod? DefaultPayment { get; set; }
    public List<PaymentMethod> Payments { get; set; } = new();

    /// <summary>Service + pet + valid schedule — enough to show the summary sheet.</summary>
    public bool CanShowSummary { get; private set; }

    /// <summary>Service already chosen (from Details or single offering) — hide the picker.</summary>
    public bool ServiceLocked { get; private set; }

    public string BookingReturnPath =>
        $"/Booking/Index?groomerId={GroomerId}"
        + string.Concat(ServiceIds.Select(id => $"&ServiceIds={id}"))
        + (ServiceId > 0 && ServiceIds.Count == 0 ? $"&serviceId={ServiceId}" : "")
        + (!string.IsNullOrWhiteSpace(Service) ? $"&service={Uri.EscapeDataString(Service)}" : "")
        + string.Concat(PetIds.Select(id => $"&PetIds={id}"))
        + (PetId > 0 && PetIds.Count == 0 ? $"&petId={PetId}" : "")
        + (!string.IsNullOrWhiteSpace(Date) ? $"&date={Uri.EscapeDataString(Date)}" : "")
        + (!string.IsNullOrWhiteSpace(Time) ? $"&time={Uri.EscapeDataString(Time)}" : "")
        + (!string.IsNullOrWhiteSpace(EndDate) ? $"&endDate={Uri.EscapeDataString(EndDate)}" : "");

    public string SelectedServicesLabel =>
        string.Join(", ", SelectedServices.Select(s => CatalogLocalizer.Text(s.Name)));

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = BookingReturnPath });

        await LoadAsync();
        if (Groomer == null) return RedirectToPage("/Groomers/Index");

        ApplyServicePreselect();
        NormalizeSelectedServices();
        if (ServiceId == 0 && Services.Count == 1)
        {
            ServiceId = Services[0].Id;
            NormalizeSelectedServices();
        }
        // Only lock the picker when the business offers a single service.
        ServiceLocked = Services.Count == 1 && SelectedServices.Count == 1;

        NormalizeSelectedPets();

        EnsureDateDefaults();
        await LoadDayAvailabilityAsync();
        await PrepareConfirmAsync(applyPromo: false);
        await LoadPaymentsAsync();
        EvaluateCanShowSummary();
        if (Pay && !CanShowSummary)
            Pay = false;
        return Page();
    }

    public async Task<IActionResult> OnPostBookAsync()
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login", new { returnUrl = BookingReturnPath });

        await LoadAsync();
        if (Groomer == null) return RedirectToPage("/Groomers/Index");

        ApplyServicePreselect();
        NormalizeSelectedServices();
        ServiceLocked = Services.Count == 1 && SelectedServices.Count == 1;
        NormalizeSelectedPets();
        EnsureDateDefaults();
        await LoadDayAvailabilityAsync();
        await PrepareConfirmAsync(applyPromo: true);
        await LoadPaymentsAsync();

        if (!AcceptTerms)
        {
            var msg = CatalogLocalizer.Loc(
                "Debes aceptar los términos para continuar.",
                "You must accept the terms to continue.");
            ModelState.AddModelError(nameof(AcceptTerms), msg);
            ErrorMessage = msg;
            EvaluateCanShowSummary();
            Pay = true;
            return Page();
        }

        if (SelectedServices.Count == 0 || SelectedPets.Count == 0)
        {
            ErrorMessage = SelectedServices.Count == 0 && Services.Count > 0
                ? CatalogLocalizer.Loc(
                    "Elige al menos un servicio para continuar.",
                    "Choose at least one service to continue.")
                : SelectedPets.Count == 0 && Pets.Count > 0
                ? CatalogLocalizer.Loc(
                    "Elige al menos una mascota para continuar.",
                    "Choose at least one pet to continue.")
                : _L["Booking_MissingData"].Value;
            EvaluateCanShowSummary();
            Pay = true;
            return Page();
        }

        DateTime scheduled;
        DateTime? endAt = null;
        var nights = 0;

        if (IsOvernight)
        {
            if (!DateTime.TryParse(Date, out var cin) || !DateTime.TryParse(EndDate, out var cout) || cout <= cin)
            {
                ErrorMessage = _L["Booking_InvalidDates"].Value;
                EvaluateCanShowSummary();
                Pay = true;
                return Page();
            }
            if (IsOvernightCheckInInPast(cin.Date))
            {
                ErrorMessage = _L["Booking_DateNotPast"].Value;
                EvaluateCanShowSummary();
                Pay = true;
                return Page();
            }
            scheduled = AppTimeZones.LocalDateAndTimeToUtc(cin.Date, TimeSpan.FromHours(14));
            endAt = AppTimeZones.LocalDateAndTimeToUtc(cout.Date, TimeSpan.FromHours(11));
            nights = Math.Max(1, (int)(cout.Date - cin.Date).TotalDays);
        }
        else
        {
            if (!TryResolveDayServiceStartUtc(out scheduled, out var scheduleError))
            {
                ErrorMessage = scheduleError ?? _L["Booking_InvalidDateTime"].Value;
                EvaluateCanShowSummary();
                Pay = true;
                return Page();
            }
            if (scheduled <= DateTime.UtcNow)
            {
                ErrorMessage = _L["Booking_DateNotPast"].Value;
                EvaluateCanShowSummary();
                Pay = true;
                return Page();
            }
        }

        if (PaymentMethodId.HasValue || DefaultPayment != null)
        {
            var pm = Payments.FirstOrDefault(p => p.Id == PaymentMethodId) ?? DefaultPayment;
            if (pm != null)
                PaymentMethodId = pm.Id;
        }

        var petNames = string.Join(", ", SelectedPets.Select(p => $"{PetSpecies.Emoji(p.Species)} {p.Name}"));
        var serviceNames = SelectedServicesLabel;
        var noteParts = new List<string>();
        if (SelectedServices.Count > 1)
            noteParts.Add(CatalogLocalizer.Loc($"Servicios: {serviceNames}", $"Services: {serviceNames}"));
        // Multi-pet only: primary pet is already on Appointment.PetId / Confirm "Mascota".
        if (SelectedPets.Count > 1)
            noteParts.Add(CatalogLocalizer.Loc($"Mascotas: {petNames}", $"Pets: {petNames}"));
        if (!string.IsNullOrWhiteSpace(Notes)) noteParts.Add(Notes.Trim());

        var result = await _createBooking.HandleAsync(new CreateBookingCommand
        {
            ClientId = userId,
            BusinessId = GroomerId,
            ServiceId = ServiceId,
            PetIds = SelectedPets.Select(p => p.Id).ToList(),
            StartUtc = scheduled,
            EndUtc = endAt,
            Nights = nights,
            Subtotal = Subtotal,
            PromoCode = PromoCode,
            Extras = SelectedExtras.Select(e => new BookingExtraLine(e.Id, e.Name, e.Price)).ToList(),
            NoteParts = noteParts,
            ClientNotice = new("Reserva enviada", $"Tu solicitud en {Groomer.BusinessName} está pendiente de confirmación."),
            BusinessNotice = new("Nueva solicitud de reserva", $"{petNames} · {serviceNames}.")
        });

        if (!result.Success)
        {
            if (result.Error == CreateBookingError.InvalidPromo)
                PromoError = result.PromoError;
            else
                ErrorMessage = result.Error == CreateBookingError.SpeciesNotAccepted
                    ? string.Format(_L["Booking_SpeciesNotAccepted"].Value, result.RejectedSpecies)
                    : _L["Booking_MissingData"].Value;
            EvaluateCanShowSummary();
            Pay = true;
            return Page();
        }

        return RedirectToPage("./Confirm", new { id = result.AppointmentId });
    }

    public async Task<IActionResult> OnPostApplyPromoAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = BookingReturnPath });

        await LoadAsync();
        ApplyServicePreselect();
        NormalizeSelectedServices();
        ServiceLocked = Services.Count == 1 && SelectedServices.Count == 1;
        NormalizeSelectedPets();
        EnsureDateDefaults();
        await LoadDayAvailabilityAsync();
        await PrepareConfirmAsync(applyPromo: true);
        await LoadPaymentsAsync();
        EvaluateCanShowSummary();
        Pay = true;
        return Page();
    }

    private void ApplyServicePreselect()
    {
        if (ServiceId != 0)
        {
            if (!Services.Any(s => s.Id == ServiceId))
                ServiceId = 0;
            return;
        }

        if (string.IsNullOrWhiteSpace(Service) || Services.Count == 0)
            return;

        var exact = Services.FirstOrDefault(s =>
            s.Name.Equals(Service, StringComparison.OrdinalIgnoreCase));
        if (exact != null)
        {
            ServiceId = exact.Id;
            return;
        }

        var partial = Services.FirstOrDefault(s =>
            s.Name.Contains(Service, StringComparison.OrdinalIgnoreCase)
            || Service.Contains(s.Name, StringComparison.OrdinalIgnoreCase));
        if (partial != null)
            ServiceId = partial.Id;
    }

    private void NormalizeSelectedServices()
    {
        ServiceIds ??= new List<int>();
        var offered = Services.Select(s => s.Id).ToHashSet();

        if (ServiceIds.Count == 0 && ServiceId > 0 && offered.Contains(ServiceId))
            ServiceIds.Add(ServiceId);

        ServiceIds = ServiceIds.Where(offered.Contains).Distinct().ToList();

        if (ServiceIds.Count == 0 && Services.Count == 1)
            ServiceIds.Add(Services[0].Id);

        SelectedServices = Services.Where(s => ServiceIds.Contains(s.Id)).ToList();
        ServiceId = SelectedServices.FirstOrDefault()?.Id ?? 0;
        SelectedService = SelectedServices.FirstOrDefault();
    }

    private async Task LoadAsync()
    {
        Groomer = await _db.Groomers
            .Include(g => g.Category)
            .FirstOrDefaultAsync(g => g.Id == GroomerId && g.IsActive);

        IsOvernight = Groomer?.Category?.IsOvernight == true
            || string.Equals(Groomer?.Category?.Slug, "hotel", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Groomer?.Category?.Slug, "daycare", StringComparison.OrdinalIgnoreCase);

        Services = await _db.Services.Where(s => s.GroomerId == GroomerId).ToListAsync();
        Extras = await _db.ServiceExtras.Where(e => e.GroomerId == GroomerId && e.IsActive).ToListAsync();

        if (_auth.CurrentUserId is int userId)
            Pets = await _db.Pets.Where(p => p.OwnerId == userId).ToListAsync();

        ServiceLocked = ServiceId > 0 && Services.Any(s => s.Id == ServiceId);
    }

    private async Task LoadPaymentsAsync()
    {
        if (_auth.CurrentUserId is not int userId) return;
        Payments = await _db.PaymentMethods.Where(p => p.UserId == userId)
            .OrderByDescending(p => p.IsDefault).ThenBy(p => p.Id).ToListAsync();
        DefaultPayment = Payments.FirstOrDefault(p => p.IsDefault) ?? Payments.FirstOrDefault();
        if (PaymentMethodId == null && DefaultPayment != null)
            PaymentMethodId = DefaultPayment.Id;
    }

    private async Task PrepareConfirmAsync(bool applyPromo)
    {
        SelectedService = SelectedServices.FirstOrDefault()
            ?? await _db.Services.FirstOrDefaultAsync(s => s.Id == ServiceId);
        // SelectedPets already normalized from owned pets; keep SelectedPet as primary.
        SelectedPet = SelectedPets.FirstOrDefault();
        SelectedExtras = Extras.Where(e => SelectedExtraIds.Contains(e.Id)).ToList();

        Nights = 0;
        Subtotal = 0;
        DiscountAmount = 0;
        EstimatedTotal = 0;
        Deposit = 0;
        PromoError = null;

        if (SelectedServices.Count == 0 || SelectedPets.Count == 0)
            return;

        var nights = 1;
        if (IsOvernight && DateTime.TryParse(Date, out var cin) && DateTime.TryParse(EndDate, out var cout) && cout > cin)
        {
            nights = Math.Max(1, (int)(cout.Date - cin.Date).TotalDays);
            Nights = nights;
        }

        Subtotal = SelectedPets.Sum(p =>
            SelectedServices.Sum(s => s.PriceFor(p.Size) * nights));
        Subtotal += SelectedExtras.Sum(e => e.Price);
        EstimatedTotal = Subtotal;

        if (applyPromo)
        {
            var promo = await _promo.HandleAsync(new ApplyPromoCodeCommand(_auth.CurrentUserId, PromoCode, Subtotal));
            if (!string.IsNullOrWhiteSpace(PromoCode))
            {
                if (promo.IsValid)
                {
                    DiscountAmount = promo.DiscountAmount;
                    EstimatedTotal = promo.FinalTotal;
                    PromoCode = promo.NormalizedCode;
                }
                else if (promo.ErrorMessage != null)
                {
                    PromoError = promo.ErrorMessage;
                    DiscountAmount = 0;
                    EstimatedTotal = Subtotal;
                }
            }
        }

        Deposit = BookingPricing.Deposit(EstimatedTotal);
    }

    private void NormalizeSelectedPets()
    {
        PetIds ??= new List<int>();
        var owned = Pets.Select(p => p.Id).ToHashSet();

        if (PetIds.Count == 0 && PetId > 0 && owned.Contains(PetId))
            PetIds.Add(PetId);

        PetIds = PetIds.Where(owned.Contains).Distinct().Take(6).ToList();

        if (PetIds.Count == 0 && Pets.Count == 1
            && (Groomer == null || Groomer.AcceptsSpecies(Pets[0].Species)))
            PetIds.Add(Pets[0].Id);

        SelectedPets = Pets.Where(p => PetIds.Contains(p.Id)).ToList();
        PetId = SelectedPets.FirstOrDefault()?.Id ?? 0;
        SelectedPet = SelectedPets.FirstOrDefault();
    }

    private void EvaluateCanShowSummary()
    {
        CanShowSummary = SelectedServices.Count > 0 && SelectedPets.Count > 0;
        if (!CanShowSummary) return;

        if (IsOvernight)
        {
            CanShowSummary = DateTime.TryParse(Date, out var cin)
                && DateTime.TryParse(EndDate, out var cout)
                && cout > cin
                && !IsOvernightCheckInInPast(cin.Date);
            return;
        }

        CanShowSummary = DayIsOpen
            && BookableTimeSlots.Count > 0
            && TryResolveDayServiceStartUtc(out var start, out _)
            && start > DateTime.UtcNow;
    }

    private void EnsureDateDefaults()
    {
        if (string.IsNullOrWhiteSpace(Date))
            Date = AppTimeZones.TodayLocalDate().ToString("yyyy-MM-dd");

        if (IsOvernight && string.IsNullOrWhiteSpace(EndDate) && DateTime.TryParse(Date, out var cin))
            EndDate = cin.Date.AddDays(1).ToString("yyyy-MM-dd");
    }

    private async Task LoadDayAvailabilityAsync()
    {
        PastSlots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        OccupiedSlots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        OutsideHoursSlots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        BookableTimeSlots = new List<string>();
        DayIsOpen = true;
        HoursLabel = null;

        if (IsOvernight || Groomer == null)
            return;

        if (!DateTime.TryParse(Date, out var dayParsed))
            dayParsed = AppTimeZones.TodayLocalDate();
        var day = dayParsed.Date;

        if (Groomer.OffersEmergency24x7)
        {
            DayIsOpen = true;
            HoursLabel = CatalogLocalizer.Loc("24 horas", "24 hours");
        }
        else
        {
            DayIsOpen = await _availability.IsAvailableOnAsync(GroomerId, day);
        }

        BusinessWeeklyHour? week = null;
        if (!Groomer.OffersEmergency24x7)
        {
            week = await _db.WeeklyHours.AsNoTracking()
                .FirstOrDefaultAsync(h => h.GroomerId == GroomerId && h.DayOfWeek == (int)day.DayOfWeek);
            if (week != null && week.IsOpen)
                HoursLabel = $"{week.OpenLabel}–{week.CloseLabel}";
        }

        if (!DayIsOpen)
        {
            Time = "";
            return;
        }

        PastSlots = BookingTime.MarkPastSlots(TimeSlots, day);
        await LoadOccupiedSlotsAsync(day);

        foreach (var label in TimeSlots)
        {
            if (!AppTimeZones.TryParseSlotToTimeSpan(label, out var tod)) continue;
            var minutes = (int)tod.TotalMinutes;

            if (!Groomer.OffersEmergency24x7
                && week != null
                && week.IsOpen
                && !AvailabilityService.IsWithinOpenWindow(minutes, week.OpenMinutes, week.CloseMinutes))
            {
                OutsideHoursSlots.Add(label);
                continue;
            }

            if (PastSlots.Contains(label) || OccupiedSlots.Contains(label))
                continue;

            BookableTimeSlots.Add(label);
        }

        if (BookableTimeSlots.Count == 0)
        {
            Time = "";
            return;
        }

        if (string.IsNullOrWhiteSpace(Time)
            || !BookableTimeSlots.Contains(Time, StringComparer.OrdinalIgnoreCase))
            Time = BookableTimeSlots[0];
    }

    private async Task LoadOccupiedSlotsAsync(DateTime day)
    {
        OccupiedSlots.Clear();
        var from = AppTimeZones.LocalDateAndTimeToUtc(day, TimeSpan.Zero);
        var to = AppTimeZones.LocalDateAndTimeToUtc(day.AddDays(1), TimeSpan.Zero);
        var taken = await _db.Appointments.AsNoTracking()
            .Where(a => a.GroomerId == GroomerId
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

    private bool TryResolveDayServiceStartUtc(out DateTime startUtc, out string? error)
    {
        startUtc = default;
        error = null;

        if (!DayIsOpen)
        {
            error = CatalogLocalizer.Loc(
                "El negocio no atiende ese día. Elige otra fecha.",
                "This business is closed that day. Choose another date.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(Date) || string.IsNullOrWhiteSpace(Time))
        {
            error = _L["Booking_SelectDateTime"].Value;
            return false;
        }

        if (!DateTime.TryParse(Date, out var day))
        {
            error = _L["Booking_InvalidDateTime"].Value;
            return false;
        }

        if (day.Date < AppTimeZones.TodayLocalDate())
        {
            error = _L["Booking_DateNotPast"].Value;
            return false;
        }

        if (BookableTimeSlots.Count == 0)
        {
            error = _L["Booking_NoFutureSlots"].Value;
            return false;
        }

        if (!BookableTimeSlots.Contains(Time, StringComparer.OrdinalIgnoreCase))
        {
            error = CatalogLocalizer.Loc(
                "Ese horario no está disponible. Elige otro.",
                "That time isn't available. Choose another.");
            return false;
        }

        if (!AppTimeZones.TryParseSlotToTimeSpan(Time, out var tod))
        {
            error = _L["Booking_InvalidDateTime"].Value;
            return false;
        }

        startUtc = AppTimeZones.LocalDateAndTimeToUtc(day.Date, tod);
        return true;
    }

    private static bool IsOvernightCheckInInPast(DateTime checkInDate)
    {
        var today = AppTimeZones.TodayLocalDate();
        if (checkInDate.Date < today) return true;
        if (checkInDate.Date > today) return false;
        var checkInUtc = AppTimeZones.LocalDateAndTimeToUtc(checkInDate.Date, TimeSpan.FromHours(14));
        return checkInUtc <= DateTime.UtcNow;
    }
}
