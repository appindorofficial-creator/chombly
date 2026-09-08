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

    [BindProperty(SupportsGet = true)]
    public List<string> Filters { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? GroomerId { get; set; }

    [BindProperty(SupportsGet = true)]
    public List<int> ExtraIds { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Notes { get; set; }

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
            return Page();
        }

        if (SelectedHotel == null || SelectedService == null || SelectedPet == null)
        {
            ErrorMessage = "Elige hotel y mascota para continuar.";
            return Page();
        }

        if (!SelectedHotel.AcceptsSpecies(SelectedPet.Species))
        {
            ErrorMessage = $"Este hotel no atiende {SelectedPet.Species}.";
            return Page();
        }

        ResolveDates(out var cin, out var cout);
        if (cout <= cin)
        {
            ErrorMessage = "Revisa las fechas de check-in y check-out.";
            return Page();
        }

        var nights = Math.Max(1, (int)(cout.Date - cin.Date).TotalDays);
        var selectedExtras = Extras.Where(e => ExtraIds.Contains(e.Id)).ToList();
        var unit = SelectedService.PriceFor(SelectedPet.Size);
        var subtotal = unit * nights * Math.Max(1, PetCount) + selectedExtras.Sum(e => e.Price);
        var promo = await _promo.TryApplyAsync(userId, PromoCode, subtotal);
        if (!string.IsNullOrWhiteSpace(PromoCode) && !promo.IsValid)
        {
            PromoError = promo.ErrorMessage;
            ErrorMessage = promo.ErrorMessage;
            Estimate = subtotal;
            PromoSubtotal = subtotal;
            return Page();
        }

        var total = promo.IsValid ? promo.FinalTotal : subtotal;
        var discount = promo.IsValid ? promo.DiscountAmount : 0m;
        var deposit = Math.Round(total * 0.35m, 2);
        if (deposit < 15) deposit = Math.Min(15, total);

        var noteParts = new List<string>();
        if (PetCount > 1) noteParts.Add($"{PetCount} mascotas");
        if (!string.IsNullOrWhiteSpace(Notes)) noteParts.Add(Notes.Trim());
        if (PaymentMethodId.HasValue)
        {
            var pm = Payments.FirstOrDefault(p => p.Id == PaymentMethodId) ?? DefaultPayment;
            if (pm != null) noteParts.Add($"Pago: {pm.Brand} •••• {pm.Last4}");
        }

        var appt = new Appointment
        {
            ClientId = userId,
            PetId = SelectedPet.Id,
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
            Message = $"{SelectedPet.Name} · {nights} noche(s).",
            Type = "appointment"
        });
        await _db.SaveChangesAsync();

        return RedirectToPage("/Booking/Confirm", new { id = appt.Id });
    }

    public async Task<IActionResult> OnPostApplyPromoAsync()
    {
        await LoadAsync();
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
        if (PetCount < 1) PetCount = 1;
        if (PetCount > 6) PetCount = 6;

        double? userLat = null, userLng = null;
        if (_auth.CurrentUserId is int userId)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            userLat = user?.Latitude;
            userLng = user?.Longitude;

            Pets = await _db.Pets.Where(p => p.OwnerId == userId).OrderBy(p => p.Name).ToListAsync();
            if (PetId == 0 && Pets.Count > 0) PetId = Pets[0].Id;
            SelectedPet = Pets.FirstOrDefault(p => p.Id == PetId);

            Payments = await _db.PaymentMethods.Where(p => p.UserId == userId).OrderByDescending(p => p.IsDefault).ToListAsync();
            DefaultPayment = Payments.FirstOrDefault(p => p.IsDefault) ?? Payments.FirstOrDefault();
            if (PaymentMethodId == null && DefaultPayment != null)
                PaymentMethodId = DefaultPayment.Id;
        }

        var hotelsQuery = _db.Groomers
            .Include(g => g.Category)
            .Include(g => g.Amenities)
            .Include(g => g.Services)
            .Where(g => g.IsActive && g.PublishStatus == BusinessPublishStatus.Approved && g.Category != null && g.Category.Slug == "hotel");

        var hotels = await hotelsQuery
            .OrderByDescending(g => g.IsFeatured)
            .ThenByDescending(g => g.Rating)
            .ToListAsync();

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

        var todayMap = await _availability.TodayMapAsync(hotels.Select(h => h.Id));
        // Para "hoy" filtrar disponibles hoy
        if (string.Equals(When, "hoy", StringComparison.OrdinalIgnoreCase))
            hotels = hotels.Where(h => todayMap.GetValueOrDefault(h.Id, true)).ToList();

        Results = hotels.Select(h =>
        {
            string? dist = null;
            if (userLat != null && userLng != null && (h.Latitude != 0 || h.Longitude != 0))
                dist = GeoHelper.FormatMilesAway(GeoHelper.MilesBetween(userLat.Value, userLng.Value, h.Latitude, h.Longitude));

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
                AvailableToday = todayMap.GetValueOrDefault(h.Id, true)
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

                if (SelectedService != null && SelectedPet != null)
                {
                    var unit = SelectedService.PriceFor(SelectedPet.Size);
                    var extrasTotal = Extras.Where(e => ExtraIds.Contains(e.Id)).Sum(e => e.Price);
                    Estimate = unit * Nights * Math.Max(1, PetCount) + extrasTotal;
                    await ApplyPromoAsync();
                }
                else if (SelectedService != null)
                {
                    Estimate = SelectedService.PriceSmall * Nights * Math.Max(1, PetCount);
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
        var part = label.Replace("A ", "").Replace(" mi de ti", "").Trim();
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
