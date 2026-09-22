using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Hotel;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly AvailabilityService _availability;
    private readonly PromoCodeService _promo;

    public IndexModel(AppDbContext db, AuthService auth, AvailabilityService availability, PromoCodeService promo)
    {
        _db = db;
        _auth = auth;
        _availability = availability;
        _promo = promo;
    }

    [BindProperty(SupportsGet = true)]
    public string When { get; set; } = "hoy"; // hoy | manana | fechas

    [BindProperty(SupportsGet = true)]
    public string? CheckIn { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? CheckOut { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PetCount { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PetId { get; set; }

    /// <summary>Selected pets for the stay (multi-select). Drives PetCount / PetId.</summary>
    [BindProperty(SupportsGet = true)]
    public List<int> PetIds { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public List<string> Filters { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? GroomerId { get; set; }

    [BindProperty(SupportsGet = true)]
    public List<int> ExtraIds { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Notes { get; set; }

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
    public List<HotelCardVm> Results { get; set; } = new();
    public List<string> AllFilterLabels { get; set; } = new();
    public List<ServiceExtra> Extras { get; set; } = new();
    public GroomerProfile? SelectedHotel { get; set; }
    public GroomerService? SelectedService { get; set; }
    public Pet? SelectedPet { get; set; }
    public List<Pet> SelectedPets { get; set; } = new();

    /// <summary>Hotel cards can be chosen only after at least one pet is selected.</summary>
    public bool CanSelectHotel => SelectedPets.Count > 0;
    public PaymentMethod? DefaultPayment { get; set; }
    public List<PaymentMethod> Payments { get; set; } = new();
    public int Nights { get; set; } = 1;
    public decimal Estimate { get; set; }
    public decimal PromoSubtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? PromoError { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessRedirect { get; set; }
    public bool ShowAll { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool More { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAsync();
        if (Pay && !(SelectedHotel != null && SelectedPet != null))
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

        if (SelectedHotel == null || SelectedService == null)
        {
            ErrorMessage = CatalogLocalizer.Loc(
                "Elige un hotel para continuar.",
                "Choose a hotel to continue.");
            Pay = true;
            return Page();
        }

        if (SelectedPets.Count == 0)
        {
            ErrorMessage = Pets.Count == 0
                ? CatalogLocalizer.Loc(
                    "Agrega una mascota para continuar.",
                    "Add a pet to continue.")
                : CatalogLocalizer.Loc(
                    "Elige al menos una mascota para continuar.",
                    "Choose at least one pet to continue.");
            Pay = true;
            return Page();
        }

        var rejected = SelectedPets.FirstOrDefault(p => !SelectedHotel.AcceptsSpecies(p.Species));
        if (rejected != null)
        {
            ErrorMessage = CatalogLocalizer.Loc(
                $"Este hotel no atiende {rejected.Species}.",
                $"This hotel does not accept {rejected.Species}.");
            Pay = true;
            return Page();
        }

        ResolveDates(out var cin, out var cout);
        if (cout <= cin)
        {
            ErrorMessage = "Revisa las fechas de check-in y check-out.";
            Pay = true;
            return Page();
        }

        var nights = Math.Max(1, (int)(cout.Date - cin.Date).TotalDays);
        var selectedExtras = Extras.Where(e => ExtraIds.Contains(e.Id)).ToList();
        var subtotal = SelectedPets.Sum(p => SelectedService.PriceFor(p.Size) * nights)
            + selectedExtras.Sum(e => e.Price);
        var promo = await _promo.TryApplyAsync(userId, PromoCode, subtotal);
        if (!string.IsNullOrWhiteSpace(PromoCode) && !promo.IsValid)
        {
            PromoError = promo.ErrorMessage;
            ErrorMessage = promo.ErrorMessage;
            Estimate = subtotal;
            PromoSubtotal = subtotal;
            Pay = true;
            return Page();
        }

        var total = promo.IsValid ? promo.FinalTotal : subtotal;
        var discount = promo.IsValid ? promo.DiscountAmount : 0m;
        var deposit = Math.Round(total * 0.35m, 2);
        if (deposit < 15) deposit = Math.Min(15, total);

        var petNames = string.Join(", ", SelectedPets.Select(p => $"{PetSpecies.Emoji(p.Species)} {p.Name}"));
        var noteParts = new List<string>();
        if (SelectedPets.Count > 1)
            noteParts.Add(CatalogLocalizer.Loc($"Mascotas: {petNames}", $"Pets: {petNames}"));
        if (!string.IsNullOrWhiteSpace(Notes)) noteParts.Add(Notes.Trim());
        if (PaymentMethodId.HasValue)
        {
            var pm = Payments.FirstOrDefault(p => p.Id == PaymentMethodId) ?? DefaultPayment;
            if (pm != null)
                noteParts.Add($"{CatalogLocalizer.Loc("Pago:", "Payment:")} {pm.Brand} •••• {pm.Last4}");
        }

        var appt = new Appointment
        {
            ClientId = userId,
            PetId = SelectedPet!.Id,
            GroomerId = SelectedHotel.Id,
            ServiceId = SelectedService.Id,
            ScheduledAt = cin.Date.AddHours(14),
            EndAt = cout.Date.AddHours(11),
            Nights = nights,
            Status = AppointmentStatus.Pending,
            TotalPrice = total,
            DepositPaid = deposit,
            PromoCode = discount > 0 ? promo.NormalizedCode : null,
            DiscountAmount = discount,
            Notes = noteParts.Count > 0 ? string.Join(" · ", noteParts) : null
        };

        foreach (var ex in selectedExtras)
        {
            appt.Extras.Add(new AppointmentExtra
            {
                ServiceExtraId = ex.Id,
                Name = ex.Name,
                Price = ex.Price
            });
        }

        _db.Appointments.Add(appt);
        _db.Notifications.Add(new AppNotification
        {
            UserId = userId,
            Title = "Reserva de hotel enviada",
            Message = $"{SelectedHotel.BusinessName} · {nights} noche(s) · pendiente de confirmación.",
            Type = "appointment"
        });
        _db.Notifications.Add(new AppNotification
        {
            UserId = SelectedHotel.UserId,
            Title = "Nueva reserva de hotel",
            Message = $"{petNames} · {nights} noche(s).",
            Type = "appointment"
        });
        await _db.SaveChangesAsync();

        return RedirectToPage("/Booking/Confirm", new { id = appt.Id });
    }

    public async Task<IActionResult> OnPostApplyPromoAsync()
    {
        await LoadAsync();
        Pay = true;
        return Page();
    }

    private async Task LoadAsync()
    {
        When ??= "hoy";
        Filters ??= new List<string>();
        ExtraIds ??= new List<int>();

        Category = await _db.Categories.FirstOrDefaultAsync(c => c.Slug == "hotel" && c.IsActive);

        ResolveDates(out var cin, out var cout);
        CheckIn = cin.ToString("yyyy-MM-dd");
        CheckOut = cout.ToString("yyyy-MM-dd");
        Nights = Math.Max(1, (int)(cout.Date - cin.Date).TotalDays);

        double? userLat = null, userLng = null;
        if (_auth.CurrentUserId is int userId)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            userLat = user?.Latitude;
            userLng = user?.Longitude;

            Pets = await _db.Pets.Where(p => p.OwnerId == userId).OrderBy(p => p.Name).ToListAsync();
            NormalizeSelectedPets();

            Payments = await _db.PaymentMethods.Where(p => p.UserId == userId).OrderByDescending(p => p.IsDefault).ToListAsync();
            DefaultPayment = Payments.FirstOrDefault(p => p.IsDefault) ?? Payments.FirstOrDefault();
            if (PaymentMethodId == null && DefaultPayment != null)
                PaymentMethodId = DefaultPayment.Id;
        }
        else
        {
            PetIds = new List<int>();
            PetCount = 0;
            PetId = 0;
            SelectedPet = null;
            SelectedPets = new List<Pet>();
        }

        // Don't keep a hotel selection (or confirm sheet) until a pet is chosen.
        if (GroomerId.HasValue && !CanSelectHotel)
            GroomerId = null;

        var hotelsQuery = _db.Groomers
            .Include(g => g.Category)
            .Include(g => g.Amenities)
            .Include(g => g.Services)
            .Where(g => g.IsActive && g.PublishStatus == BusinessPublishStatus.Approved && g.Category != null && g.Category.Slug == "hotel");

        var hotels = await hotelsQuery
            .OrderByDescending(g => g.IsFeatured)
            .ThenByDescending(g => g.Rating)
            .ToListAsync();

        // Results must accept every selected pet species.
        if (SelectedPets.Count > 0)
            hotels = hotels.Where(h => SelectedPets.All(p => h.AcceptsSpecies(p.Species))).ToList();
        else
            hotels = new List<GroomerProfile>();

        AllFilterLabels = hotels
            .SelectMany(h => h.Amenities.Select(a => a.Label))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        if (Filters.Count > 0)
        {
            hotels = hotels.Where(h =>
                Filters.All(f => h.Amenities.Any(a =>
                    !string.IsNullOrEmpty(a.Label)
                    && a.Label.Equals(f, StringComparison.OrdinalIgnoreCase)))).ToList();
        }

        // Drop a prior hotel pick if it no longer matches the selected pet.
        if (GroomerId.HasValue && (SelectedPet == null || hotels.All(h => h.Id != GroomerId)))
            GroomerId = null;

        var todayMap = await _availability.TodayMapAsync(hotels.Select(h => h.Id));
        // Para "hoy" filtrar disponibles hoy
        if (string.Equals(When, "hoy", StringComparison.OrdinalIgnoreCase))
            hotels = hotels.Where(h => todayMap.GetValueOrDefault(h.Id, true)).ToList();

        Results = hotels.Select(h =>
        {
            string? dist = null;
            if (userLat != null && userLng != null && (h.Latitude != 0 || h.Longitude != 0))
            {
                var km = GeoHelper.KmBetween(userLat.Value, userLng.Value, h.Latitude, h.Longitude);
                dist = GeoHelper.FormatDistanceOrPlace(km, h.City, h.Address);
            }

            var svc = h.Services.OrderBy(s => s.PriceSmall).FirstOrDefault();
            return new HotelCardVm
            {
                Hotel = h,
                DistanceLabel = dist,
                StartingNight = svc?.PriceSmall ?? h.StartingPrice,
                Features = h.Amenities.OrderBy(a => a.SortOrder)
                    .Select(a => a.Label)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .Take(4)
                    .ToList(),
                Recommended = h.IsFeatured,
                AvailableToday = todayMap.GetValueOrDefault(h.Id, false)
            };
        }).ToList();

        if (userLat != null)
            Results = Results.OrderBy(r => r.DistanceLabel == null)
                .ThenBy(r => ParseMiles(r.DistanceLabel))
                .ThenByDescending(r => r.Recommended)
                .ToList();

        ShowAll = More;
        if (!More && Results.Count > 3)
            Results = Results.Take(3).ToList();

        if (GroomerId.HasValue)
        {
            SelectedHotel = await _db.Groomers
                .Include(g => g.Amenities)
                .Include(g => g.Services)
                .FirstOrDefaultAsync(g => g.Id == GroomerId && g.IsActive);
            if (SelectedHotel != null)
            {
                SelectedService = SelectedHotel.Services.OrderBy(s => s.PriceSmall).FirstOrDefault();
                Extras = await _db.ServiceExtras
                    .Where(e => e.GroomerId == SelectedHotel.Id && e.IsActive)
                    .OrderBy(e => e.Price)
                    .ToListAsync();

                if (SelectedService != null && SelectedPets.Count > 0)
                {
                    var extrasTotal = Extras.Where(e => ExtraIds.Contains(e.Id)).Sum(e => e.Price);
                    Estimate = SelectedPets.Sum(p => SelectedService.PriceFor(p.Size) * Nights) + extrasTotal;
                    await ApplyPromoAsync();
                }
                else if (SelectedService != null)
                {
                    Estimate = SelectedService.PriceSmall * Nights;
                    await ApplyPromoAsync();
                }
            }
        }
        else
        {
            // Extras genéricos: unión de extras de hoteles listados (solo lectura hasta elegir hotel).
            // Materialize first — EF cannot translate GroupBy + OrderBy + First reliably on SQL Server.
            var ids = hotels.Select(h => h.Id).ToList();
            if (ids.Count == 0)
            {
                Extras = new List<ServiceExtra>();
            }
            else
            {
                var raw = await _db.ServiceExtras
                    .AsNoTracking()
                    .Where(e => ids.Contains(e.GroomerId) && e.IsActive)
                    .ToListAsync();

                Extras = raw
                    .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                    .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.OrderBy(x => x.Price).First())
                    .OrderBy(e => e.Price)
                    .Take(8)
                    .ToList();
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

        var promo = await _promo.TryApplyAsync(_auth.CurrentUserId, PromoCode, Estimate);
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

    private void NormalizeSelectedPets()
    {
        PetIds ??= new List<int>();
        var owned = Pets.Select(p => p.Id).ToHashSet();

        // Legacy single PetId / PetCount URLs → seed PetIds.
        if (PetIds.Count == 0 && PetId > 0 && owned.Contains(PetId))
            PetIds.Add(PetId);

        PetIds = PetIds.Where(owned.Contains).Distinct().Take(6).ToList();

        // If still empty and user has exactly one pet, auto-select it.
        if (PetIds.Count == 0 && Pets.Count == 1)
            PetIds.Add(Pets[0].Id);

        SelectedPets = Pets.Where(p => PetIds.Contains(p.Id)).ToList();
        PetCount = SelectedPets.Count;
        PetId = SelectedPets.FirstOrDefault()?.Id ?? 0;
        SelectedPet = SelectedPets.FirstOrDefault();
    }

    private void ResolveDates(out DateTime cin, out DateTime cout)
    {
        var today = DateTime.Today;
        var when = When ?? "hoy";
        if (string.Equals(when, "hoy", StringComparison.OrdinalIgnoreCase))
        {
            cin = today;
            cout = today.AddDays(1);
        }
        else if (string.Equals(when, "manana", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(when, "mañana", StringComparison.OrdinalIgnoreCase))
        {
            cin = today.AddDays(1);
            cout = today.AddDays(2);
        }
        else
        {
            if (!DateTime.TryParse(CheckIn, out cin)) cin = today.AddDays(1);
            if (!DateTime.TryParse(CheckOut, out cout)) cout = cin.AddDays(2);
            if (cout <= cin) cout = cin.AddDays(1);
        }
    }

    private static double ParseMiles(string? label)
    {
        if (string.IsNullOrEmpty(label)) return double.MaxValue;
        var part = label.Replace("A ", "").Replace(" km de ti", "").Replace(" mi de ti", "").Trim();
        return double.TryParse(part, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var m) ? m : double.MaxValue;
    }

    public class HotelCardVm
    {
        public GroomerProfile Hotel { get; set; } = null!;
        public string? DistanceLabel { get; set; }
        public decimal StartingNight { get; set; }
        public List<string> Features { get; set; } = new();
        public bool Recommended { get; set; }
        public bool AvailableToday { get; set; }
    }
}
