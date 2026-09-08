using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Walkers;

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

    public static readonly (string Key, string Label)[] TimeOptions =
    {
        ("ahora", "Ahora"),
        ("manana9", "Mañana 9:00 AM"),
        ("tarde", "Tarde 1:00 PM"),
        ("noche", "Noche 6:00 PM")
    };

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
    public List<string> Prefs { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? GroomerId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Notes { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool More { get; set; }

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
    public PaymentMethod? DefaultPayment { get; set; }
    public List<PaymentMethod> Payments { get; set; } = new();
    public decimal Estimate { get; set; }
    public decimal PromoSubtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? PromoError { get; set; }
    public string? DateLabel { get; set; }
    public string? TimeLabel { get; set; }
    public string? SuggestedTime { get; set; }
    public string? ErrorMessage { get; set; }
    public bool HasMore { get; set; }

    /// <summary>True when the user has explicitly chosen a duration chip.</summary>
    public bool HasDuration => DurationOptions.Contains(Duration);

    /// <summary>Minutes shown on cards/estimates; 60 until the user picks a duration.</summary>
    public int DisplayDuration => HasDuration ? Duration : 60;

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

        if (string.IsNullOrWhiteSpace(When) || string.IsNullOrWhiteSpace(Slot) || !DurationOptions.Contains(Duration))
        {
            ErrorMessage = CatalogLocalizer.Loc(
                "Elige cuándo, a qué hora y cuánto tiempo dura el paseo.",
                "Choose when, what time, and how long the walk should be.");
            return Page();
        }

        if (SelectedWalker == null || SelectedService == null || SelectedPet == null)
        {
            ErrorMessage = CatalogLocalizer.Loc("Elige paseador y mascota para continuar.", "Choose a walker and pet to continue.");
            return Page();
        }

        if (!SelectedWalker.AcceptsSpecies(SelectedPet.Species))
        {
            ErrorMessage = CatalogLocalizer.Loc(
                $"Este paseador no atiende {SelectedPet.Species}.",
                $"This walker does not accept {SelectedPet.Species}.");
            return Page();
        }

        ResolveDate(out var day);
        ResolveStart(day, out var start);
        var end = start.AddMinutes(Duration);

        var subtotal = PriceForDuration(SelectedService, SelectedPet);
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
        if (deposit < 10) deposit = Math.Min(10, total);

        var noteParts = new List<string> { $"Paseo {Duration} min" };
        if (Prefs.Count > 0)
            noteParts.Add("Prefs: " + string.Join(", ", Prefs));
        if (!string.IsNullOrWhiteSpace(Notes)) noteParts.Add(Notes.Trim());
        if (PaymentMethodId.HasValue || DefaultPayment != null)
        {
            var pm = Payments.FirstOrDefault(p => p.Id == PaymentMethodId) ?? DefaultPayment;
            if (pm != null) noteParts.Add($"Pago: {pm.Brand} •••• {pm.Last4}");
        }

        var appt = new Appointment
        {
            ClientId = userId,
            PetId = SelectedPet.Id,
            GroomerId = SelectedWalker.Id,
            ServiceId = SelectedService.Id,
            ScheduledAt = start,
            EndAt = end,
            Nights = 0,
            Status = AppointmentStatus.Pending,
            TotalPrice = total,
            DepositPaid = deposit,
            PromoCode = discount > 0 ? promo.NormalizedCode : null,
            DiscountAmount = discount,
            Notes = string.Join(" · ", noteParts)
        };

        _db.Appointments.Add(appt);
        _db.Notifications.Add(new AppNotification
        {
            UserId = userId,
            Title = "Paseo solicitado",
            Message = $"{SelectedWalker.BusinessName} · {Duration} min · pendiente de confirmación.",
            Type = "appointment"
        });
        _db.Notifications.Add(new AppNotification
        {
            UserId = SelectedWalker.UserId,
            Title = "Nueva solicitud de paseo",
            Message = $"{SelectedPet.Name} · {start:g} · {Duration} min.",
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
        if (Duration != 0 && !DurationOptions.Contains(Duration)) Duration = 0;

        Category = await _db.Categories.FirstOrDefaultAsync(c => c.Slug == "walkers" && c.IsActive);
        ResolveDate(out var day);
        if (!string.IsNullOrWhiteSpace(When) || !string.IsNullOrWhiteSpace(Date))
            Date = day.ToString("yyyy-MM-dd");
        DateLabel = When switch
        {
            "hoy" => $"Hoy, {day:d MMM yyyy}",
            "manana" => $"Mañana, {day:d MMM yyyy}",
            "fecha" => day.ToString("ddd d MMM yyyy"),
            _ => null
        };

        ResolveStart(day, out var start);
        SuggestedTime = string.IsNullOrWhiteSpace(Slot) ? null : start.ToString("h:mm tt");
        TimeLabel = Slot switch
        {
            "ahora" => $"Ahora (~{SuggestedTime})",
            "manana9" => "9:00 AM",
            "tarde" => "1:00 PM",
            "noche" => "6:00 PM",
            _ => null
        };

        double? userLat = null, userLng = null;
        if (_auth.CurrentUserId is int userId)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            userLat = user?.Latitude;
            userLng = user?.Longitude;

            Pets = await _db.Pets.Where(p => p.OwnerId == userId).OrderBy(p => p.Name).ToListAsync();
            SelectedPet = PetId > 0 ? Pets.FirstOrDefault(p => p.Id == PetId) : null;

            Payments = await _db.PaymentMethods.Where(p => p.UserId == userId)
                .OrderByDescending(p => p.IsDefault).ToListAsync();
            DefaultPayment = Payments.FirstOrDefault(p => p.IsDefault) ?? Payments.FirstOrDefault();
            if (PaymentMethodId == null && DefaultPayment != null)
                PaymentMethodId = DefaultPayment.Id;
        }

        var walkers = await _db.Groomers
            .Include(g => g.Category)
            .Include(g => g.Amenities)
            .Include(g => g.Services)
            .Where(g => g.IsActive && g.PublishStatus == BusinessPublishStatus.Approved && g.Category != null && g.Category.Slug == "walkers")
            .OrderByDescending(g => g.IsFeatured)
            .ThenByDescending(g => g.Rating)
            .ToListAsync();

        var todayMap = await _availability.TodayMapAsync(walkers.Select(w => w.Id));

        if (When.Equals("hoy", StringComparison.OrdinalIgnoreCase))
            walkers = walkers.Where(w => todayMap.GetValueOrDefault(w.Id, true)).ToList();

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
                dist = GeoHelper.FormatMilesAway(miles);
            }

            var svc = PickService(w.Services);
            var mins = PricingMinutes;
            var price = svc != null ? PriceForDuration(svc, SelectedPet) : ScalePrice(w.StartingPrice, 60, mins);

            return new WalkerCardVm
            {
                Walker = w,
                DistanceLabel = dist,
                Miles = miles,
                Price = price,
                AvailableToday = todayMap.GetValueOrDefault(w.Id, true)
            };
        }).ToList();

        if (userLat != null)
            Results = Results.OrderBy(r => r.Miles ?? double.MaxValue)
                .ThenByDescending(r => r.Walker.Rating).ToList();

        HasMore = !More && Results.Count > 3;
        if (HasMore)
            Results = Results.Take(3).ToList();

        if (GroomerId.HasValue)
        {
            SelectedWalker = await _db.Groomers
                .Include(g => g.Services)
                .Include(g => g.Amenities)
                .FirstOrDefaultAsync(g => g.Id == GroomerId && g.IsActive);

            if (SelectedWalker != null)
            {
                SelectedService = PickService(SelectedWalker.Services);
                if (SelectedService != null)
                {
                    Estimate = PriceForDuration(SelectedService, SelectedPet);
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

    private decimal PriceForDuration(GroomerService svc, Pet? pet)
    {
        var mins = PricingMinutes;
        var basePrice = pet != null ? svc.PriceFor(pet.Size) : svc.PriceSmall;
        var baseMinutes = svc.DurationMinutes > 0 ? svc.DurationMinutes : 60;
        if (svc.DurationMinutes == mins) return basePrice;
        return ScalePrice(basePrice, baseMinutes, mins);
    }

    private static decimal ScalePrice(decimal basePrice, int baseMinutes, int minutes)
    {
        if (baseMinutes <= 0) baseMinutes = 60;
        return Math.Round(basePrice * minutes / (decimal)baseMinutes, 0);
    }

    private void ResolveDate(out DateTime day)
    {
        var today = DateTime.Today;
        if (When.Equals("hoy", StringComparison.OrdinalIgnoreCase))
            day = today;
        else if (When.Equals("manana", StringComparison.OrdinalIgnoreCase))
            day = today.AddDays(1);
        else if (When.Equals("fecha", StringComparison.OrdinalIgnoreCase) && DateTime.TryParse(Date, out day))
            day = day.Date;
        else if (!string.IsNullOrWhiteSpace(When) && DateTime.TryParse(Date, out day))
            day = day.Date;
        else
            day = today;
    }

    private void ResolveStart(DateTime day, out DateTime start)
    {
        var now = DateTime.Now;
        if (Slot == "ahora")
        {
            if (day.Date == DateTime.Today)
            {
                start = now.AddMinutes(15);
                start = new DateTime(start.Year, start.Month, start.Day, start.Hour, (start.Minute / 5) * 5, 0);
            }
            else
                start = day.Date.AddHours(9);
        }
        else if (Slot == "manana9")
            start = day.Date.AddHours(9);
        else if (Slot == "noche")
            start = day.Date.AddHours(18);
        else if (Slot == "tarde")
            start = day.Date.AddHours(13);
        else
            start = day.Date.AddHours(13); // listing fallback only; booking requires Slot
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
