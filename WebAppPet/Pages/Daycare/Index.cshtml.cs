using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.Bookings.CreateBooking;
using WebAppPet.Application.Bookings.GetActiveExtras;
using WebAppPet.Application.Bookings.GetBookableProvider;
using WebAppPet.Application.Bookings.GetCategoryBookingContext;
using WebAppPet.Application.Bookings.Shared;
using WebAppPet.Application.Businesses.SearchBusinesses;
using WebAppPet.Application.Promotions.ApplyPromoCode;
using WebAppPet.Domain;
using WebAppPet.Domain.Markets;
using WebAppPet.Infrastructure.Identity;
using WebAppPet.Localization;
using WebAppPet.Pages.Shared;

namespace WebAppPet.Pages.Daycare;

public class IndexModel : PageModel
{
    private readonly AuthService _auth;
    private readonly SearchBusinessesHandler _search;
    private readonly ApplyPromoCodeHandler _promo;
    private readonly CreateBookingHandler _createBooking;
    private readonly GetCategoryBookingContextHandler _context;
    private readonly GetBookableProviderHandler _provider;
    private readonly GetActiveExtrasHandler _extras;

    public IndexModel(
        AuthService auth,
        SearchBusinessesHandler search,
        ApplyPromoCodeHandler promo,
        CreateBookingHandler createBooking,
        GetCategoryBookingContextHandler context,
        GetBookableProviderHandler provider,
        GetActiveExtrasHandler extras)
    {
        _auth = auth;
        _search = search;
        _promo = promo;
        _createBooking = createBooking;
        _context = context;
        _provider = provider;
        _extras = extras;
    }

    public static readonly (string Key, string Label, string Hint)[] ScheduleOptions =
    {
        ("medio", "Medio día", "hasta 5 horas"),
        ("completo", "Día completo", "7 AM – 7 PM"),
        ("personalizado", "Personalizado", "elige horario")
    };

    public static readonly (string Key, string Label)[] PrefOptions =
    {
        ("grandes", "Acepta perros grandes"),
        ("juego", "Áreas de juego"),
        ("siesta", "Siesta / descanso"),
        ("camaras", "Cámaras en vivo")
    };

    [BindProperty(SupportsGet = true)]
    public string When { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string? Date { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Schedule { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string CustomStart { get; set; } = "09:00";

    [BindProperty(SupportsGet = true)]
    public string CustomEnd { get; set; } = "17:00";

    [BindProperty(SupportsGet = true)]
    public int PetId { get; set; }

    [BindProperty(SupportsGet = true)]
    public List<int> PetIds { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public List<string> Prefs { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? GroomerId { get; set; }

    [BindProperty(SupportsGet = true)]
    public List<int> ExtraIds { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Notes { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool More { get; set; }

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
    public List<DaycareCardVm> Results { get; set; } = new();
    public List<ServiceExtra> Extras { get; set; } = new();
    public GroomerProfile? SelectedDaycare { get; set; }
    public GroomerService? SelectedService { get; set; }
    public Pet? SelectedPet { get; set; }
    public List<Pet> SelectedPets { get; set; } = new();
    public PaymentMethod? DefaultPayment { get; set; }
    public List<PaymentMethod> Payments { get; set; } = new();
    public decimal Estimate { get; set; }
    public decimal PromoSubtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? PromoError { get; set; }
    public string? DateLabel { get; set; }
    public string? ScheduleLabel { get; set; }
    public string? ErrorMessage { get; set; }
    public bool HasMore { get; set; }

    public bool HasDate => BookingDate.TryParseSelected(Date, out _);

    public bool HasSchedule =>
        Schedule is "medio" or "completo"
        || (Schedule == "personalizado"
            && TimeSpan.TryParse(CustomStart, out _)
            && TimeSpan.TryParse(CustomEnd, out _));

    /// <summary>Steps 1–3 complete: date, schedule, and pet(s).</summary>
    public bool HasBookingBasics =>
        HasDate
        && HasSchedule
        && SelectedPets.Count > 0;

    public bool CanSelectDaycare => HasBookingBasics;

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAsync();
        if (Pay && !(GroomerId.HasValue && SelectedPet != null && HasDate))
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

        if (SelectedDaycare == null || SelectedService == null || SelectedPets.Count == 0)
        {
            ErrorMessage = CatalogLocalizer.Loc("Elige guardería y mascota para continuar.", "Choose a daycare and pet to continue.");
            Pay = true;
            return Page();
        }

        ResolveDate(out var day);
        ResolveWindow(day, out var start, out var end);
        if (end <= start)
        {
            ErrorMessage = CatalogLocalizer.Loc("Revisa el horario personalizado.", "Check the custom schedule.");
            Pay = true;
            return Page();
        }

        var selectedExtras = Extras.Where(e => ExtraIds.Contains(e.Id)).ToList();
        var basePrice = SelectedPets.Sum(p => PriceForSchedule(SelectedService, p));
        var subtotal = basePrice + selectedExtras.Sum(e => e.Price);

        var petNames = BookingPetSelection.NamesSummary(SelectedPets);
        var noteParts = new List<string>
        {
            $"{CatalogLocalizer.Loc("Horario:", "Schedule:")} {ScheduleLabel}"
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
            BusinessId = SelectedDaycare.Id,
            ServiceId = SelectedService.Id,
            PetIds = SelectedPets.Select(p => p.Id).ToList(),
            StartUtc = AppTimeZones.LocalDateAndTimeToUtc(start.Date, start.TimeOfDay),
            EndUtc = AppTimeZones.LocalDateAndTimeToUtc(end.Date, end.TimeOfDay),
            Subtotal = subtotal,
            PromoCode = PromoCode,
            Extras = selectedExtras.Select(e => new BookingExtraLine(e.Id, e.Name, e.Price)).ToList(),
            NoteParts = noteParts,
            ClientNotice = new("Reserva de daycare enviada", $"{SelectedDaycare.BusinessName} · {ScheduleLabel} · pendiente de confirmación."),
            BusinessNotice = new("Nueva reserva de daycare", $"{petNames} · {day:d} · {ScheduleLabel}.")
        });

        if (!result.Success)
        {
            ErrorMessage = result.Error switch
            {
                CreateBookingError.SpeciesNotAccepted => CatalogLocalizer.Loc(
                    $"Esta guardería no atiende {result.RejectedSpecies}.",
                    $"This daycare does not accept {result.RejectedSpecies}."),
                CreateBookingError.InvalidPromo => result.PromoError,
                CreateBookingError.NoPaymentMethod or CreateBookingError.PaymentDeclined => result.PaymentError,
                _ => CatalogLocalizer.Loc("Elige guardería y mascota para continuar.", "Choose a daycare and pet to continue.")
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
        var context = await _context.HandleAsync(new GetCategoryBookingContextQuery("daycare", _auth.CurrentUserId));
        Category = context.Category;
        (When, Date) = BookingDate.NormalizeFromLegacy(When, Date);
        ResolveDate(out var day);
        DateLabel = HasDate ? BookingDate.FormatLabel(day) : null;

        ScheduleLabel = Schedule switch
        {
            "medio" => CatalogLocalizer.Loc("Medio día (hasta 5 h)", "Half day (up to 5 h)"),
            "completo" => CatalogLocalizer.Loc("Día completo (7 AM – 7 PM)", "Full day (7 AM – 7 PM)"),
            "personalizado" => CatalogLocalizer.Loc($"Personalizado {CustomStart} – {CustomEnd}", $"Custom {CustomStart} – {CustomEnd}"),
            _ => null
        };

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

        // Calendar-day availability (open that weekday), not "open right now".
        var matches = await _search.HandleAsync(new SearchBusinessesQuery(
            "daycare",
            AppTimeZones.CurrentCountryCode,
            SelectedPets.Select(p => p.Species).ToList(),
            OpenOn: HasDate && day.Date == AppTimeZones.TodayLocalDate() ? day.Date : null,
            UserLatitude: userLat,
            UserLongitude: userLng));

        if (Prefs.Contains("grandes"))
            matches = matches.Where(m =>
                m.Business.Amenities.Any(a => a.Label.Contains("grande", StringComparison.OrdinalIgnoreCase))
                || m.Business.AcceptsSpecies(PetSpecies.Dog)).ToList();

        if (Prefs.Contains("juego"))
            matches = matches.Where(m => BusinessListing.AmenityMatch(m.Business, "juego", "play", "patio", "área")).ToList();

        if (Prefs.Contains("siesta"))
            matches = matches.Where(m => BusinessListing.AmenityMatch(m.Business, "siesta", "descanso", "nap", "quiet")).ToList();

        if (Prefs.Contains("camaras"))
            matches = matches.Where(m => BusinessListing.AmenityMatch(m.Business, "cámara", "camara", "cam", "vivo")).ToList();

        Results = matches.Select(m =>
        {
            var d = m.Business;
            var svc = PickService(d.Services);
            var price = svc != null
                ? (SelectedPets.Count > 0
                    ? SelectedPets.Sum(p => PriceForSchedule(svc, p))
                    : PriceForSchedule(svc, null))
                : d.StartingPrice;

            return new DaycareCardVm
            {
                Daycare = d,
                DistanceLabel = m.DistanceLabel,
                Miles = m.Miles,
                StartingPrice = price,
                PriceUnitLabel = CatalogLocalizer.Loc(
                    Schedule == "medio" ? "/ medio día" : "/ día completo",
                    Schedule == "medio" ? "/ half day" : "/ full day"),
                AvailableToday = m.OpenNow,
                Features = d.Amenities.OrderBy(a => a.SortOrder).Select(a => a.Label).Take(3).ToList()
            };
        }).ToList();

        Results = BusinessListing.FirstPage(Results, More, out var hasMore);
        HasMore = hasMore;

        if (GroomerId.HasValue && !HasBookingBasics)
            GroomerId = null;

        if (GroomerId.HasValue)
        {
            SelectedDaycare = await _provider.HandleAsync(new GetBookableProviderQuery(GroomerId.Value));

            if (SelectedDaycare != null)
            {
                SelectedService = PickService(SelectedDaycare.Services);
                Extras = (await _extras.HandleAsync(new GetActiveExtrasQuery(new[] { SelectedDaycare.Id })))
                    .OrderBy(e => e.Price)
                    .ToList();

                if (SelectedService != null && SelectedPets.Count > 0)
                {
                    var basePrice = SelectedPets.Sum(p => PriceForSchedule(SelectedService, p));
                    Estimate = basePrice + Extras.Where(e => ExtraIds.Contains(e.Id)).Sum(e => e.Price);
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

    private GroomerService? PickService(IEnumerable<GroomerService> services)
    {
        var list = services.ToList();
        if (list.Count == 0) return null;

        string[] keys = Schedule switch
        {
            "medio" => new[] { "medio", "half", "5 h", "5h" },
            "completo" => new[] { "completo", "full", "día", "dia" },
            _ => new[] { "completo", "día", "dia", "daycare" }
        };

        foreach (var k in keys)
        {
            var hit = list.FirstOrDefault(s =>
                s.Name.Contains(k, StringComparison.OrdinalIgnoreCase)
                || s.Description.Contains(k, StringComparison.OrdinalIgnoreCase)
                || (s.BillingUnit?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false));
            if (hit != null) return hit;
        }

        return list.OrderBy(s => s.PriceSmall).First();
    }

    private decimal PriceForSchedule(GroomerService svc, Pet? pet) =>
        BookingPricing.DaycarePrice(svc, pet?.Size, halfDay: Schedule == "medio");

    private void ResolveDate(out DateTime day)
    {
        if (BookingDate.TryParseSelected(Date, out day))
            return;
        day = AppTimeZones.TodayLocalDate();
    }

    private void ResolveWindow(DateTime day, out DateTime start, out DateTime end)
    {
        if (Schedule == "medio")
        {
            start = day.Date.AddHours(9);
            end = day.Date.AddHours(14);
        }
        else if (Schedule == "personalizado")
        {
            if (!TimeSpan.TryParse(CustomStart, out var s)) s = TimeSpan.FromHours(9);
            if (!TimeSpan.TryParse(CustomEnd, out var e)) e = TimeSpan.FromHours(17);
            start = day.Date.Add(s);
            end = day.Date.Add(e);
        }
        else
        {
            start = day.Date.AddHours(7);
            end = day.Date.AddHours(19);
        }
    }

    public class DaycareCardVm
    {
        public GroomerProfile Daycare { get; set; } = null!;
        public string? DistanceLabel { get; set; }
        public double? Miles { get; set; }
        public decimal StartingPrice { get; set; }
        public string PriceUnitLabel { get; set; } = "/ día";
        public bool AvailableToday { get; set; }
        public List<string> Features { get; set; } = new();
    }
}
