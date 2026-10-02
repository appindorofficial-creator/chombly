using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.Bookings.CreateBooking;
using WebAppPet.Application.Bookings.FindNextFreeStart;
using WebAppPet.Application.Bookings.GetBookableProvider;
using WebAppPet.Application.Bookings.GetCategoryBookingContext;
using WebAppPet.Application.Bookings.GetOccupiedSlots;
using WebAppPet.Application.Bookings.Shared;
using WebAppPet.Application.Businesses.SearchBusinesses;
using WebAppPet.Application.Promotions.ApplyPromoCode;
using WebAppPet.Domain;
using WebAppPet.Domain.Markets;
using WebAppPet.Infrastructure.Identity;
using WebAppPet.Localization;
using WebAppPet.Pages.Shared;

namespace WebAppPet.Pages.Trainers;

public class IndexModel : PageModel
{
    private readonly AuthService _auth;
    private readonly SearchBusinessesHandler _search;
    private readonly ApplyPromoCodeHandler _promo;
    private readonly CreateBookingHandler _createBooking;
    private readonly GetCategoryBookingContextHandler _context;
    private readonly GetBookableProviderHandler _provider;
    private readonly GetOccupiedSlotsHandler _occupiedSlots;
    private readonly FindNextFreeStartHandler _nextFreeStart;

    public IndexModel(
        AuthService auth,
        SearchBusinessesHandler search,
        ApplyPromoCodeHandler promo,
        CreateBookingHandler createBooking,
        GetCategoryBookingContextHandler context,
        GetBookableProviderHandler provider,
        GetOccupiedSlotsHandler occupiedSlots,
        FindNextFreeStartHandler nextFreeStart)
    {
        _auth = auth;
        _search = search;
        _promo = promo;
        _createBooking = createBooking;
        _context = context;
        _provider = provider;
        _occupiedSlots = occupiedSlots;
        _nextFreeStart = nextFreeStart;
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

        var (scheduled, start, scheduleError) = await ResolveScheduleAsync(SelectedTrainer.Id);
        if (!scheduled)
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
            PaymentMethodId = PaymentMethodId,
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
                CreateBookingError.NoPaymentMethod or CreateBookingError.PaymentDeclined => result.PaymentError,
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

        var context = await _context.HandleAsync(new GetCategoryBookingContextQuery("trainers", _auth.CurrentUserId));
        Category = context.Category;
        (When, Date) = BookingDate.NormalizeFromLegacy(When, Date);
        if (!string.IsNullOrWhiteSpace(Slot) && !TimeSlots.Contains(Slot, StringComparer.OrdinalIgnoreCase))
            Slot = "";

        var day = ResolveDay();
        MarkPastSlots(day);
        if (GroomerId is int bookedGroomerId && HasDate)
            OccupiedSlots = await _occupiedSlots.HandleAsync(new GetOccupiedSlotsQuery(bookedGroomerId, day, TimeSlots));

        if (!string.IsNullOrWhiteSpace(Slot) && (OccupiedSlots.Contains(Slot) || PastSlots.Contains(Slot)))
        {
            var free = TimeSlots.FirstOrDefault(t => !OccupiedSlots.Contains(t) && !PastSlots.Contains(t));
            Slot = free ?? "";
        }

        var (scheduled, start, _) = HasSlot && HasDate
            ? await ResolveScheduleAsync(GroomerId)
            : (false, default, null);
        DateLabel = scheduled
            ? $"{BookingDate.FormatLabel(day)} · {start:h:mm tt}"
            : HasDate ? BookingDate.FormatLabel(day) : null;

        var userLat = context.ClientLatitude;
        var userLng = context.ClientLongitude;
        if (_auth.CurrentUserId is int)
        {
            Pets = context.Pets;
            PetIds ??= new();
            var pid = PetId;
            BookingPetSelection.Normalize(Pets, PetIds, ref pid, out var selected);
            PetId = pid;
            SelectedPets = selected;
            SelectedPet = selected.FirstOrDefault();

            Payments = context.Payments;
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
            SelectedTrainer = await _provider.HandleAsync(new GetBookableProviderQuery(GroomerId.Value));

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

    /// <summary>
    /// Start of the session for the chosen day and slot. With a trainer, moves to the next free slot
    /// (updating <see cref="Slot"/> and <see cref="Date"/>) when the chosen one is taken.
    /// </summary>
    private async Task<(bool Ok, DateTime StartUtc, string? Error)> ResolveScheduleAsync(int? groomerId)
    {
        var day = ResolveDay();
        if (!AppTimeZones.TryParseSlotToTimeSpan(Slot, out var tod))
            return (false, default, CatalogLocalizer.Loc("Elige un horario.", "Choose a time slot."));

        var startUtc = AppTimeZones.LocalDateAndTimeToUtc(day, tod);
        if (startUtc <= DateTime.UtcNow)
        {
            return (false, startUtc, CatalogLocalizer.Loc(
                "No puedes elegir una fecha u hora en el pasado.",
                "You can't select a past date or time."));
        }

        if (groomerId is not int gid)
            return (true, startUtc, null);

        var free = await _nextFreeStart.HandleAsync(
            new FindNextFreeStartQuery(gid, day, Slot, TimeSlots, DateTime.UtcNow));
        if (free == null)
        {
            return (false, startUtc, CatalogLocalizer.Loc(
                "Ese horario ya no está disponible. Elige otro día u hora.",
                "That time is no longer available. Choose another day or time."));
        }

        Slot = free.Slot;
        Date = free.Day.ToString("yyyy-MM-dd");
        When = "";
        return (true, free.StartUtc, null);
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
