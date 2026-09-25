using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Bookings.CreateBooking;
using WebAppPet.Application.Bookings.Shared;
using WebAppPet.Application.Promotions.ApplyPromoCode;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Pages.Shared;
using WebAppPet.Services;

namespace WebAppPet.Pages.Walkers;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly AvailabilityService _availability;
    private readonly ApplyPromoCodeHandler _promo;
    private readonly CreateBookingHandler _createBooking;

    public IndexModel(
        AppDbContext db,
        AuthService auth,
        AvailabilityService availability,
        ApplyPromoCodeHandler promo,
        CreateBookingHandler createBooking)
    {
        _db = db;
        _auth = auth;
        _availability = availability;
        _promo = promo;
        _createBooking = createBooking;
    }

    public static readonly string[] TimeSlots = BookingTime.DefaultSlots;

    public static readonly int[] DurationOptions = { 30, 60, 90, 120 };

    public static readonly (string Key, string Label)[] PrefOptions =
    {
        ("individual", "Paseo individual"),
        ("grandes", "Acepta perros grandes"),
        ("escaleras", "Sin escaleras"),
        ("foto", "Foto del paseo")
    };

    [BindProperty(SupportsGet = true)]
    public string When { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string? Date { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Slot { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public int Duration { get; set; }

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
    public List<WalkerCardVm> Results { get; set; } = new();
    public GroomerProfile? SelectedWalker { get; set; }
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
    public string? TimeLabel { get; set; }
    public string? ErrorMessage { get; set; }
    public bool HasMore { get; set; }
    public HashSet<string> OccupiedSlots { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> PastSlots { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>True when the user has explicitly chosen a duration chip.</summary>
    public bool HasDuration => DurationOptions.Contains(Duration);

    /// <summary>True when the user picked a valid booking date (today or later).</summary>
    public bool HasDate => BookingDate.TryParseSelected(Date, out _);

    public bool HasSlot => BookingTime.IsSlotAvailable(Slot, TimeSlots, PastSlots, OccupiedSlots);

    /// <summary>Minutes shown on cards/estimates; 60 until the user picks a duration.</summary>
    public int DisplayDuration => HasDuration ? Duration : 60;

    /// <summary>Steps 1–4 complete: date, time, duration, and pet(s).</summary>
    public bool HasBookingBasics =>
        HasDate
        && HasSlot
        && HasDuration
        && SelectedPets.Count > 0;

    /// <summary>Walker cards can be chosen only after steps 1–4.</summary>
    public bool CanSelectWalker => HasBookingBasics;

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAsync();
        if (Pay && !(GroomerId.HasValue && SelectedPet != null && HasDate && HasSlot && HasDuration))
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

        if (!HasDate || !HasSlot || !DurationOptions.Contains(Duration))
        {
            ErrorMessage = CatalogLocalizer.Loc(
                "Elige fecha, a qué hora y cuánto tiempo dura el paseo.",
                "Choose a date, what time, and how long the walk should be.");
            Pay = true;
            return Page();
        }

        if (SelectedWalker == null || SelectedService == null || SelectedPets.Count == 0)
        {
            ErrorMessage = CatalogLocalizer.Loc("Elige paseador y mascota para continuar.", "Choose a walker and pet to continue.");
            Pay = true;
            return Page();
        }

        ResolveDate(out var day);
        if (!BookingTime.TryResolveStartUtc(Slot, day, out var startUtc, out var scheduleError))
        {
            ErrorMessage = scheduleError;
            Pay = true;
            return Page();
        }

        var endUtc = startUtc.AddMinutes(Duration);

        var subtotal = SelectedPets.Sum(p => PriceForDuration(SelectedService, p));

        var petNames = BookingPetSelection.NamesSummary(SelectedPets);
        var noteParts = new List<string>
        {
            CatalogLocalizer.Loc($"Paseo {Duration} min", $"Walk {Duration} min")
        };
        if (SelectedPets.Count > 1)
            noteParts.Insert(0, CatalogLocalizer.Loc($"Mascotas: {petNames}", $"Pets: {petNames}"));
        if (Prefs.Count > 0)
            noteParts.Add("Prefs: " + string.Join(", ", Prefs.Select(CatalogLocalizer.Text)));
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
            BusinessId = SelectedWalker.Id,
            ServiceId = SelectedService.Id,
            PetIds = SelectedPets.Select(p => p.Id).ToList(),
            StartUtc = startUtc,
            EndUtc = endUtc,
            Subtotal = subtotal,
            PromoCode = PromoCode,
            MinimumDeposit = BookingPricing.WalkMinimumDeposit,
            NoteParts = noteParts,
            AllowedSpecies = new[] { PetSpecies.Dog },
            ClientNotice = new("Paseo solicitado", $"{SelectedWalker.BusinessName} · {Duration} min · pendiente de confirmación."),
            BusinessNotice = new("Nueva solicitud de paseo", $"{petNames} · {AppTimeZones.FormatShort(startUtc)} · {Duration} min.")
        });

        if (!result.Success)
        {
            ErrorMessage = result.Error switch
            {
                CreateBookingError.SpeciesNotAccepted => CatalogLocalizer.Loc(
                    $"Este paseador no atiende {result.RejectedSpecies}.",
                    $"This walker does not accept {result.RejectedSpecies}."),
                CreateBookingError.InvalidPromo => result.PromoError,
                _ => CatalogLocalizer.Loc("Elige paseador y mascota para continuar.", "Choose a walker and pet to continue.")
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
        if (Duration != 0 && !DurationOptions.Contains(Duration)) Duration = 0;

        Category = await _db.Categories.FirstOrDefaultAsync(c => c.Slug == "walkers" && c.IsActive);
        (When, Date) = BookingDate.NormalizeFromLegacy(When, Date);
        Slot = BookingTime.MapLegacySlot(Slot) ?? "";
        ResolveDate(out var day);
        PastSlots = BookingTime.MarkPastSlots(TimeSlots, day);
        OccupiedSlots.Clear();
        if (GroomerId is int walkerId && HasDate)
            await LoadOccupiedSlotsAsync(walkerId, day);

        if (!string.IsNullOrWhiteSpace(Slot) &&
            (PastSlots.Contains(Slot) || OccupiedSlots.Contains(Slot) ||
             !TimeSlots.Contains(Slot, StringComparer.OrdinalIgnoreCase)))
            Slot = "";

        DateLabel = HasDate ? BookingDate.FormatLabel(day) : null;
        TimeLabel = HasSlot ? Slot : null;

        double? userLat = null, userLng = null;
        if (_auth.CurrentUserId is int userId)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            userLat = user?.Latitude;
            userLng = user?.Longitude;

            Pets = await _db.Pets.Where(p => p.OwnerId == userId).OrderBy(p => p.Name).ToListAsync();
            PetIds ??= new();
            var pid = PetId;
            BookingPetSelection.Normalize(Pets, PetIds, ref pid, out var selected, allowedSpecies: new[] { PetSpecies.Dog });
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

        var walkers = await _db.Groomers
            .Include(g => g.Category)
            .Include(g => g.Amenities)
            .Include(g => g.Services)
            .Where(g => g.IsActive && g.PublishStatus == BusinessPublishStatus.Approved && g.Category != null && g.Category.Slug == "walkers")
            .OrderByDescending(g => g.IsFeatured)
            .ThenByDescending(g => g.Rating)
            .ToListAsync();

        walkers = BusinessMarketResolver.FilterHomeMarket(walkers, AppTimeZones.CurrentCountryCode).ToList();

        walkers = walkers.Where(w => w.AcceptsSpecies(PetSpecies.Dog)).ToList();

        if (SelectedPets.Count > 0)
            walkers = walkers.Where(w => SelectedPets.All(p => w.AcceptsSpecies(p.Species))).ToList();
        else
            walkers = new List<GroomerProfile>();

        var todayMap = await _availability.TodayMapAsync(walkers.Select(w => w.Id));

        // Calendar-day availability (open that weekday), not "open right now".
        if (HasDate && day.Date == AppTimeZones.TodayLocalDate())
        {
            var openToday = new List<GroomerProfile>();
            foreach (var w in walkers)
            {
                if (await _availability.IsAvailableOnAsync(w.Id, day.Date))
                    openToday.Add(w);
            }
            walkers = openToday;
        }

        if (Prefs.Contains("individual"))
            walkers = walkers.Where(w => AmenityMatch(w, "individual", "privado", "1 a 1", "uno")).ToList();

        if (Prefs.Contains("grandes"))
            walkers = walkers.Where(w =>
                AmenityMatch(w, "grande") || w.AcceptsSpecies(PetSpecies.Dog)).ToList();

        if (Prefs.Contains("escaleras"))
            walkers = walkers.Where(w => AmenityMatch(w, "escalera", "elevator", "ascensor", "sin escaleras")).ToList();

        if (Prefs.Contains("foto"))
            walkers = walkers.Where(w => AmenityMatch(w, "foto", "photo", "gps", "actualiz")).ToList();

        Results = walkers.Select(w =>
        {
            double? miles = null;
            string? dist = null;
            if (userLat != null && userLng != null && (w.Latitude != 0 || w.Longitude != 0))
            {
                miles = GeoHelper.MilesBetween(userLat.Value, userLng.Value, w.Latitude, w.Longitude);
                var km = miles is null ? null : miles * 1.609344;
                dist = GeoHelper.FormatDistanceOrPlace(km, w.City, w.Address);
            }

            var svc = PickService(w.Services);
            var mins = PricingMinutes;
            var price = svc != null
                ? (SelectedPets.Count > 0
                    ? SelectedPets.Sum(p => PriceForDuration(svc, p))
                    : PriceForDuration(svc, null))
                : BookingPricing.ScaleByMinutes(w.StartingPrice, BookingPricing.DefaultServiceMinutes, mins);

            return new WalkerCardVm
            {
                Walker = w,
                DistanceLabel = dist,
                Miles = miles,
                Price = price,
                AvailableToday = todayMap.GetValueOrDefault(w.Id, false)
            };
        }).ToList();

        if (userLat != null)
            Results = Results.OrderBy(r => r.Miles ?? double.MaxValue)
                .ThenByDescending(r => r.Walker.Rating).ToList();

        HasMore = !More && Results.Count > 3;
        if (HasMore)
            Results = Results.Take(3).ToList();

        // Don't keep a walker selection (or confirm sheet) until when/time/duration/pet are set.
        if (GroomerId.HasValue && !HasBookingBasics)
            GroomerId = null;

        if (GroomerId.HasValue)
        {
            SelectedWalker = await _db.Groomers
                .Include(g => g.Services)
                .Include(g => g.Amenities)
                .FirstOrDefaultAsync(g => g.Id == GroomerId && g.IsActive);

            if (SelectedWalker != null)
            {
                SelectedService = PickService(SelectedWalker.Services);
                if (SelectedService != null && SelectedPets.Count > 0)
                {
                    Estimate = SelectedPets.Sum(p => PriceForDuration(SelectedService, p));
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

    private static bool AmenityMatch(GroomerProfile w, params string[] keys)
    {
        var blob = string.Join(" ", w.Amenities.Select(a => a.Label)) + " " + (w.About ?? "");
        return keys.Any(k => blob.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Minutes used for estimates when the user has not picked a duration yet.</summary>
    private int PricingMinutes => DisplayDuration;

    private GroomerService? PickService(IEnumerable<GroomerService> services)
    {
        var list = services.ToList();
        if (list.Count == 0) return null;

        var mins = PricingMinutes;
        var byDuration = list.FirstOrDefault(s => s.DurationMinutes == mins);
        if (byDuration != null) return byDuration;

        var walk = list.FirstOrDefault(s =>
            s.Name.Contains("paseo", StringComparison.OrdinalIgnoreCase)
            || s.Name.Contains("walk", StringComparison.OrdinalIgnoreCase));
        return walk ?? list.OrderBy(s => s.PriceSmall).First();
    }

    private decimal PriceForDuration(GroomerService svc, Pet? pet) =>
        BookingPricing.WalkPrice(svc, pet?.Size, PricingMinutes);

    private void ResolveDate(out DateTime day)
    {
        if (BookingDate.TryParseSelected(Date, out day))
            return;
        day = AppTimeZones.TodayLocalDate();
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

    public class WalkerCardVm
    {
        public GroomerProfile Walker { get; set; } = null!;
        public string? DistanceLabel { get; set; }
        public double? Miles { get; set; }
        public decimal Price { get; set; }
        public bool AvailableToday { get; set; }
    }
}
