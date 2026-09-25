using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Bookings.CreateBooking;
using WebAppPet.Application.Bookings.Shared;
using WebAppPet.Application.Businesses.SearchBusinesses;
using WebAppPet.Application.Promotions.ApplyPromoCode;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Pages.Shared;
using WebAppPet.Services;

namespace WebAppPet.Pages.Trainers;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly SearchBusinessesHandler _search;
    private readonly ApplyPromoCodeHandler _promo;
    private readonly CreateBookingHandler _createBooking;

    public IndexModel(
        AppDbContext db,
        AuthService auth,
        SearchBusinessesHandler search,
        ApplyPromoCodeHandler promo,
        CreateBookingHandler createBooking)
    {
        _db = db;
        _auth = auth;
        _search = search;
        _promo = promo;
        _createBooking = createBooking;
    }

    public static readonly (string Key, string Label)[] TrainingTypes =
    {
        ("obediencia", "Obediencia básica"),
        ("cachorros", "Cachorros"),
        ("modificacion", "Modificación de conducta"),
        ("correccion", "Corrección de comportamiento"),
        ("avanzado", "Entrenamiento avanzado"),
        ("otro", "Otro")
    };

    public static readonly (string Key, string Label)[] PlaceOptions =
    {
        ("domicilio", "A domicilio"),
        ("centro", "En mi lugar / centro"),
        ("virtual", "Virtual")
    };

    public static readonly (int Sessions, string Label, string? Badge)[] PackageOptions =
    {
        (1, "1 sesión", null),
        (4, "Paquete 4 sesiones", "Recomendado"),
        (8, "Paquete 8 sesiones", null)
    };

    public static readonly (string Key, string Label)[] PrefOptions =
    {
        ("certificado", "Entrenador con certificación"),
        ("raza", "Experiencia con mi raza"),
        ("parque", "Clases en el parque"),
        ("flexible", "Flexible en horario")
    };

    [BindProperty(SupportsGet = true)]
    public string Need { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string Place { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public int Sessions { get; set; }

    [BindProperty(SupportsGet = true)]
    public string When { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string Slot { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string? Date { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PetId { get; set; }

    [BindProperty(SupportsGet = true)]
    public List<int> PetIds { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public List<string> Prefs { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? GroomerId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Notes { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool More { get; set; }

    public static readonly string[] TimeSlots = BookingTime.DefaultSlots;

    public HashSet<string> OccupiedSlots { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> PastSlots { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [BindProperty(SupportsGet = true)]
    public bool Pay { get; set; }

    [BindProperty]
    public bool AcceptTerms { get; set; }

    [BindProperty]
    public int? PaymentMethodId { get; set; }

    [BindProperty]
    public string? PromoCode { get; set; }

    public ServiceCategory? Category { get; set; }
    public List<Pet> Pets { get; set; } = new();
    public List<TrainerCardVm> Results { get; set; } = new();
    public GroomerProfile? SelectedTrainer { get; set; }
    public GroomerService? SelectedService { get; set; }
    public Pet? SelectedPet { get; set; }
    public List<Pet> SelectedPets { get; set; } = new();
    public PaymentMethod? DefaultPayment { get; set; }
    public List<PaymentMethod> Payments { get; set; } = new();
    public decimal UnitPrice { get; set; }
    public decimal Estimate { get; set; }
    public decimal PromoSubtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? PromoError { get; set; }
    public string? DateLabel { get; set; }
    public string? ErrorMessage { get; set; }
    public bool HasMore { get; set; }

    public bool HasNeed => TrainingTypes.Any(t => string.Equals(t.Key, Need, StringComparison.OrdinalIgnoreCase));
    public bool HasPlace => PlaceOptions.Any(p => string.Equals(p.Key, Place, StringComparison.OrdinalIgnoreCase));
    public bool HasSessions => Sessions is 1 or 4 or 8;
    public bool HasSlot =>
        TimeSlots.Contains(Slot, StringComparer.OrdinalIgnoreCase)
        && !PastSlots.Contains(Slot)
        && !OccupiedSlots.Contains(Slot);
    public bool HasDate => BookingDate.TryParseSelected(Date, out _);

    /// <summary>Steps 1–6 complete: need, date, time, place, sessions, and pet(s).</summary>
    public bool HasBookingBasics =>
        HasNeed
        && HasDate
        && HasSlot
        && HasPlace
        && HasSessions
        && SelectedPets.Count > 0;

    public bool CanSelectTrainer => HasBookingBasics;

    public string NeedLabel => CatalogLocalizer.Text(TrainingTypes.FirstOrDefault(t => t.Key == Need).Label ?? "Entrenamiento");
    public string PlaceLabel => CatalogLocalizer.Text(PlaceOptions.FirstOrDefault(p => p.Key == Place).Label ?? Place);
    public string PackageLabel => CatalogLocalizer.Text(Sessions switch
    {
        4 => "Paquete 4 sesiones",
        8 => "Paquete 8 sesiones",
        _ => "1 sesión"
    });

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAsync();
        if (Pay && !(GroomerId.HasValue && SelectedPet != null && HasDate && HasSlot))
            Pay = false;
        return Page();
    }

    public async Task<IActionResult> OnPostBookAsync()
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login");

        await LoadAsync();

        if (!AcceptTerms)
        {
            var msg = CatalogLocalizer.Loc(
                "Debes aceptar los Términos y Condiciones.",
                "You must accept the Terms & Conditions.");
            ModelState.AddModelError(nameof(AcceptTerms), msg);
            ErrorMessage = msg;
            Pay = true;
            return Page();
        }

        if (SelectedTrainer == null || SelectedService == null || SelectedPets.Count == 0)
        {
            ErrorMessage = CatalogLocalizer.Loc(
                "Elige entrenador y mascota para continuar.",
                "Choose a trainer and pet to continue.");
            Pay = true;
            return Page();
        }

        if (Sessions is not (1 or 4 or 8)) Sessions = 1;

        if (!TryResolveSchedule(SelectedTrainer.Id, out var start, out var scheduleError))
        {
            ErrorMessage = scheduleError;
            Pay = true;
            return Page();
        }

        var subtotal = SelectedPets.Sum(p => SelectedService.PriceFor(p.Size)) * Sessions;

        var petNames = BookingPetSelection.NamesSummary(SelectedPets);
        var noteParts = new List<string>
        {
            $"{CatalogLocalizer.Loc("Tipo:", "Type:")} {NeedLabel}",
            $"{CatalogLocalizer.Loc("Lugar:", "Place:")} {PlaceLabel}",
            PackageLabel,
            CatalogLocalizer.Loc("1 sesión / semana", "1 session / week")
        };
        if (SelectedPets.Count > 1)
            noteParts.Insert(0, CatalogLocalizer.Loc($"Mascotas: {petNames}", $"Pets: {petNames}"));
        if (!string.IsNullOrWhiteSpace(Notes)) noteParts.Add(Notes.Trim());
        if (PaymentMethodId.HasValue || DefaultPayment != null)
        {
            var pm = Payments.FirstOrDefault(p => p.Id == PaymentMethodId) ?? DefaultPayment;
            if (pm != null)
                noteParts.Add($"{CatalogLocalizer.Loc("Pago:", "Payment:")} {pm.Brand} •••• {pm.Last4}");
        }

        var result = await _createBooking.HandleAsync(new CreateBookingCommand
        {
            ClientId = userId,
            BusinessId = SelectedTrainer.Id,
            ServiceId = SelectedService.Id,
            PetIds = SelectedPets.Select(p => p.Id).ToList(),
            StartUtc = start,
            EndUtc = start.AddMinutes(SelectedService.DurationMinutes > 0
                ? SelectedService.DurationMinutes
                : BookingPricing.DefaultServiceMinutes),
            Subtotal = subtotal,
            PromoCode = PromoCode,
            NoteParts = noteParts,
            ClientNotice = new("Reserva de training enviada", $"{SelectedTrainer.BusinessName} · {PackageLabel} · pendiente de confirmación."),
            BusinessNotice = new("Nueva reserva de training", $"{petNames} · {NeedLabel} · {PackageLabel}.")
        });

        if (!result.Success)
        {
            ErrorMessage = result.Error switch
            {
                CreateBookingError.SpeciesNotAccepted => CatalogLocalizer.Loc(
                    $"Este entrenador no atiende {PetSpecies.Label(result.RejectedSpecies)}.",
                    $"This trainer does not serve {PetSpecies.Label(result.RejectedSpecies)}."),
                CreateBookingError.InvalidPromo => result.PromoError,
                _ => CatalogLocalizer.Loc("Elige entrenador y mascota para continuar.", "Choose a trainer and pet to continue.")
            };
            if (result.Error == CreateBookingError.InvalidPromo)
            {
                PromoError = result.PromoError;
                Estimate = subtotal;
                PromoSubtotal = subtotal;
                DiscountAmount = 0;
            }
            Pay = true;
            return Page();
        }

        return RedirectToPage("/Booking/Confirm", new { id = result.AppointmentId });
    }

    public async Task<IActionResult> OnPostApplyPromoAsync()
    {
        await LoadAsync();
        Pay = true;
        return Page();
    }

    private async Task LoadAsync()
    {
        if (Sessions is not (0 or 1 or 4 or 8)) Sessions = 0;

        Category = await _db.Categories.FirstOrDefaultAsync(c => c.Slug == "trainers" && c.IsActive);
        (When, Date) = BookingDate.NormalizeFromLegacy(When, Date);
        if (!string.IsNullOrWhiteSpace(Slot) && !TimeSlots.Contains(Slot, StringComparer.OrdinalIgnoreCase))
            Slot = "";

        var day = ResolveDay();
        MarkPastSlots(day);
        if (GroomerId is int bookedGroomerId && HasDate)
            await LoadOccupiedSlotsAsync(bookedGroomerId, day);

        if (!string.IsNullOrWhiteSpace(Slot) && (OccupiedSlots.Contains(Slot) || PastSlots.Contains(Slot)))
        {
            var free = TimeSlots.FirstOrDefault(t => !OccupiedSlots.Contains(t) && !PastSlots.Contains(t));
            Slot = free ?? "";
        }

        if (HasSlot && HasDate && TryResolveSchedule(GroomerId, out var start, out _))
            DateLabel = $"{BookingDate.FormatLabel(day)} · {start:h:mm tt}";
        else
            DateLabel = HasDate ? BookingDate.FormatLabel(day) : null;

        double? userLat = null, userLng = null;
        if (_auth.CurrentUserId is int userId)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            userLat = user?.Latitude;
            userLng = user?.Longitude;

            Pets = await _db.Pets.Where(p => p.OwnerId == userId).OrderBy(p => p.Name).ToListAsync();
            PetIds ??= new();
            var pid = PetId;
            BookingPetSelection.Normalize(Pets, PetIds, ref pid, out var selected);
            PetId = pid;
            SelectedPets = selected;
            SelectedPet = selected.FirstOrDefault();

            Payments = await _db.PaymentMethods.Where(p => p.UserId == userId)
                .OrderByDescending(p => p.IsDefault).ToListAsync();
            DefaultPayment = Payments.FirstOrDefault(p => p.IsDefault) ?? Payments.FirstOrDefault();
            if (PaymentMethodId == null && DefaultPayment != null)
                PaymentMethodId = DefaultPayment.Id;
        }
        else
        {
            PetIds = new List<int>();
            PetId = 0;
            SelectedPet = null;
            SelectedPets = new List<Pet>();
        }

        var matches = await _search.HandleAsync(new SearchBusinessesQuery(
            "trainers",
            AppTimeZones.CurrentCountryCode,
            SelectedPets.Select(p => p.Species).ToList(),
            UserLatitude: userLat,
            UserLongitude: userLng));

        // Filtrar por modalidad (domicilio / centro / virtual)
        matches = matches.Where(m => MatchesPlace(m.Business)).ToList();

        if (Prefs.Contains("certificado"))
            matches = matches.Where(m => BusinessListing.AmenityMatch(m.Business, "certific", "certificado", "certified")).ToList();

        if (Prefs.Contains("raza"))
            matches = matches.Where(m => BusinessListing.AmenityMatch(m.Business, "raza", "breed", "experiencia")).ToList();

        if (Prefs.Contains("parque"))
            matches = matches.Where(m => BusinessListing.AmenityMatch(m.Business, "parque", "park")).ToList();

        if (Prefs.Contains("flexible"))
            matches = matches.Where(m => BusinessListing.AmenityMatch(m.Business, "flexible", "horario")).ToList();

        Results = matches.Select(m =>
        {
            var t = m.Business;
            var svc = PickService(t.Services);
            var unit = svc != null
                ? (SelectedPets.Count > 0
                    ? SelectedPets.Sum(p => svc.PriceFor(p.Size))
                    : svc.PriceSmall)
                : t.StartingPrice;

            return new TrainerCardVm
            {
                Trainer = t,
                DistanceLabel = m.DistanceLabel,
                Miles = m.Miles,
                UnitPrice = unit,
                AvailableToday = m.OpenNow,
                AvailabilityLabel = m.OpenNow ? "Abierto ahora" : "Cerrado ahora"
            };
        }).ToList();

        Results = BusinessListing.FirstPage(Results, More, out var hasMore);
        HasMore = hasMore;

        if (GroomerId.HasValue && !HasBookingBasics)
            GroomerId = null;

        if (GroomerId.HasValue)
        {
            SelectedTrainer = await _db.Groomers
                .Include(g => g.Services)
                .Include(g => g.Amenities)
                .FirstOrDefaultAsync(g => g.Id == GroomerId && g.IsActive);

            if (SelectedTrainer != null)
            {
                SelectedService = PickService(SelectedTrainer.Services);
                if (SelectedService != null)
                {
                    UnitPrice = SelectedPets.Count > 0
                        ? SelectedPets.Sum(p => SelectedService.PriceFor(p.Size))
                        : SelectedService.PriceSmall;
                    Estimate = UnitPrice * (HasSessions ? Sessions : 1);
                    await ApplyPromoAsync();
                }
            }
        }
    }

    private async Task ApplyPromoAsync()
    {
        PromoSubtotal = Estimate;
        DiscountAmount = 0;
        PromoError = null;
        if (string.IsNullOrWhiteSpace(PromoCode))
            return;

        var promo = await _promo.HandleAsync(new ApplyPromoCodeCommand(_auth.CurrentUserId, PromoCode, Estimate));
        if (promo.IsValid)
        {
            DiscountAmount = promo.DiscountAmount;
            Estimate = promo.FinalTotal;
            PromoCode = promo.NormalizedCode;
        }
        else
        {
            PromoError = promo.ErrorMessage;
        }
    }

    private bool MatchesPlace(GroomerProfile t)
    {
        return Place switch
        {
            "domicilio" => BusinessListing.AmenityMatch(t, "domicilio", "home", "casa")
                           || t.Type == GroomerType.InHome || t.Type == GroomerType.Mobile
                           || t.Amenities.Count == 0,
            "centro" => BusinessListing.AmenityMatch(t, "centro", "lugar", "salon", "salón")
                        || t.Type == GroomerType.Salon
                        || t.Amenities.Count == 0,
            "virtual" => BusinessListing.AmenityMatch(t, "virtual", "online", "zoom", "remoto")
                         || (t.About?.Contains("virtual", StringComparison.OrdinalIgnoreCase) ?? false)
                         || t.Amenities.Count == 0,
            _ => true
        };
    }

    private GroomerService? PickService(IEnumerable<GroomerService> services)
    {
        var list = services.ToList();
        if (list.Count == 0) return null;

        string[] keys = Need switch
        {
            "cachorros" => new[] { "cachorr", "puppy" },
            "modificacion" => new[] { "modific", "conducta", "comport" },
            "correccion" => new[] { "correc", "comport" },
            "avanzado" => new[] { "avanz" },
            "obediencia" => new[] { "obedien", "básica", "basica" },
            _ => new[] { "entren", "sesión", "sesion" }
        };

        foreach (var k in keys)
        {
            var hit = list.FirstOrDefault(s =>
                s.Name.Contains(k, StringComparison.OrdinalIgnoreCase)
                || s.Description.Contains(k, StringComparison.OrdinalIgnoreCase));
            if (hit != null) return hit;
        }

        return list.OrderBy(s => s.PriceSmall).First();
    }

    private DateTime ResolveDay()
    {
        if (BookingDate.TryParseSelected(Date, out var day))
            return day;
        return AppTimeZones.TodayLocalDate();
    }

    /// <summary>Disable wall-clock slots that are already past for the selected (or today) day.</summary>
    private void MarkPastSlots(DateTime day)
    {
        PastSlots = BookingTime.MarkPastSlots(TimeSlots, day);
    }

    private async Task LoadOccupiedSlotsAsync(int groomerId, DateTime day)
    {
        OccupiedSlots.Clear();
        var from = AppTimeZones.LocalDateAndTimeToUtc(day, TimeSpan.Zero);
        var to = AppTimeZones.LocalDateAndTimeToUtc(day.AddDays(1), TimeSpan.Zero);
        var taken = await _db.Appointments.AsNoTracking()
            .Where(a => a.GroomerId == groomerId
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

    private bool TryResolveSchedule(int? groomerId, out DateTime startUtc, out string? error)
    {
        error = null;
        var day = ResolveDay();
        if (!AppTimeZones.TryParseSlotToTimeSpan(Slot, out var tod))
        {
            error = CatalogLocalizer.Loc("Elige un horario.", "Choose a time slot.");
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

        if (groomerId is not int gid)
            return true;

        var preferredIndex = Array.FindIndex(TimeSlots, t => string.Equals(t, Slot, StringComparison.OrdinalIgnoreCase));
        if (preferredIndex < 0) preferredIndex = 0;
        var nowUtc = DateTime.UtcNow;

        for (var dayOffset = 0; dayOffset < 14; dayOffset++)
        {
            var tryDay = day.AddDays(dayOffset);
            if (tryDay.DayOfWeek == DayOfWeek.Sunday) continue;

            var ordered = dayOffset == 0
                ? TimeSlots.Skip(preferredIndex).ToArray()
                : TimeSlots;

            foreach (var t in ordered)
            {
                if (!AppTimeZones.TryParseSlotToTimeSpan(t, out var slotTod)) continue;
                var candidate = AppTimeZones.LocalDateAndTimeToUtc(tryDay, slotTod);
                if (candidate <= nowUtc) continue;
                var busy = _db.Appointments.AsNoTracking().Any(a =>
                    a.GroomerId == gid
                    && a.Status != AppointmentStatus.Cancelled
                    && a.ScheduledAt == candidate);
                if (busy) continue;

                startUtc = candidate;
                Slot = t;
                Date = tryDay.ToString("yyyy-MM-dd");
                When = "";
                return true;
            }
        }

        error = CatalogLocalizer.Loc(
            "Ese horario ya no está disponible. Elige otro día u hora.",
            "That time is no longer available. Choose another day or time.");
        return false;
    }

    public class TrainerCardVm
    {
        public GroomerProfile Trainer { get; set; } = null!;
        public string? DistanceLabel { get; set; }
        public double? Miles { get; set; }
        public decimal UnitPrice { get; set; }
        public bool AvailableToday { get; set; }
        public string AvailabilityLabel { get; set; } = "";
    }
}
