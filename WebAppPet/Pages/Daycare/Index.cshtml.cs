using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Daycare;

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
    public string When { get; set; } = "hoy";

    [BindProperty(SupportsGet = true)]
    public string? Date { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Schedule { get; set; } = "completo";

    [BindProperty(SupportsGet = true)]
    public string CustomStart { get; set; } = "09:00";

    [BindProperty(SupportsGet = true)]
    public string CustomEnd { get; set; } = "17:00";

    [BindProperty(SupportsGet = true)]
    public int PetId { get; set; }

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

        if (SelectedDaycare == null || SelectedService == null || SelectedPet == null)
        {
            ErrorMessage = "Elige daycare y mascota para continuar.";
            return Page();
        }

        if (!SelectedDaycare.AcceptsSpecies(SelectedPet.Species))
        {
            ErrorMessage = $"Este daycare no atiende {SelectedPet.Species}.";
            return Page();
        }

        ResolveDate(out var day);
        ResolveWindow(day, out var start, out var end);
        if (end <= start)
        {
            ErrorMessage = "Revisa el horario personalizado.";
            return Page();
        }

        var selectedExtras = Extras.Where(e => ExtraIds.Contains(e.Id)).ToList();
        var basePrice = PriceForSchedule(SelectedService, SelectedPet);
        var subtotal = basePrice + selectedExtras.Sum(e => e.Price);
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

        var noteParts = new List<string> { $"Horario: {ScheduleLabel}" };
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
            GroomerId = SelectedDaycare.Id,
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
            Title = "Reserva de daycare enviada",
            Message = $"{SelectedDaycare.BusinessName} · {ScheduleLabel} · pendiente de confirmación.",
            Type = "appointment"
        });
        _db.Notifications.Add(new AppNotification
        {
            UserId = SelectedDaycare.UserId,
            Title = "Nueva reserva de daycare",
            Message = $"{SelectedPet.Name} · {day:d} · {ScheduleLabel}.",
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
        Category = await _db.Categories.FirstOrDefaultAsync(c => c.Slug == "daycare" && c.IsActive);
        ResolveDate(out var day);
        Date = day.ToString("yyyy-MM-dd");
        DateLabel = When switch
        {
            "hoy" => $"Hoy, {day:d MMM yyyy}",
            "manana" => $"Mañana, {day:d MMM yyyy}",
            _ => day.ToString("ddd d MMM yyyy")
        };

        var sched = ScheduleOptions.FirstOrDefault(s => s.Key == Schedule);
        ScheduleLabel = Schedule switch
        {
            "medio" => CatalogLocalizer.Loc("Medio día (hasta 5 h)", "Half day (up to 5 h)"),
            "completo" => CatalogLocalizer.Loc("Día completo (7 AM – 7 PM)", "Full day (7 AM – 7 PM)"),
            "personalizado" => CatalogLocalizer.Loc($"Personalizado {CustomStart} – {CustomEnd}", $"Custom {CustomStart} – {CustomEnd}"),
            _ => CatalogLocalizer.Text(sched.Label ?? "Día completo")
        };

        double? userLat = null, userLng = null;
        if (_auth.CurrentUserId is int userId)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            userLat = user?.Latitude;
            userLng = user?.Longitude;

            Pets = await _db.Pets.Where(p => p.OwnerId == userId).OrderBy(p => p.Name).ToListAsync();
            if (PetId == 0 && Pets.Count > 0) PetId = Pets[0].Id;
            SelectedPet = Pets.FirstOrDefault(p => p.Id == PetId);

            Payments = await _db.PaymentMethods.Where(p => p.UserId == userId)
                .OrderByDescending(p => p.IsDefault).ToListAsync();
            DefaultPayment = Payments.FirstOrDefault(p => p.IsDefault) ?? Payments.FirstOrDefault();
            if (PaymentMethodId == null && DefaultPayment != null)
                PaymentMethodId = DefaultPayment.Id;
        }

        var daycares = await _db.Groomers
            .Include(g => g.Category)
            .Include(g => g.Amenities)
            .Include(g => g.Services)
            .Where(g => g.IsActive && g.PublishStatus == BusinessPublishStatus.Approved && g.Category != null && g.Category.Slug == "daycare")
            .OrderByDescending(g => g.IsFeatured)
            .ThenByDescending(g => g.Rating)
            .ToListAsync();

        var todayMap = await _availability.TodayMapAsync(daycares.Select(d => d.Id));

        if (When.Equals("hoy", StringComparison.OrdinalIgnoreCase))
            daycares = daycares.Where(d => todayMap.GetValueOrDefault(d.Id, true)).ToList();

        if (Prefs.Contains("grandes"))
            daycares = daycares.Where(d =>
                d.Amenities.Any(a => a.Label.Contains("grande", StringComparison.OrdinalIgnoreCase))
                || d.AcceptsSpecies(PetSpecies.Dog)).ToList();

        if (Prefs.Contains("juego"))
            daycares = daycares.Where(d => AmenityMatch(d, "juego", "play", "patio", "área")).ToList();

        if (Prefs.Contains("siesta"))
            daycares = daycares.Where(d => AmenityMatch(d, "siesta", "descanso", "nap", "quiet")).ToList();

        if (Prefs.Contains("camaras"))
            daycares = daycares.Where(d => AmenityMatch(d, "cámara", "camara", "cam", "vivo")).ToList();

        Results = daycares.Select(d =>
        {
            double? miles = null;
            string? dist = null;
            if (userLat != null && userLng != null && (d.Latitude != 0 || d.Longitude != 0))
            {
                miles = GeoHelper.MilesBetween(userLat.Value, userLng.Value, d.Latitude, d.Longitude);
                dist = GeoHelper.FormatMilesAway(miles);
            }

            var svc = PickService(d.Services);
            var price = svc != null
                ? (SelectedPet != null ? PriceForSchedule(svc, SelectedPet) : PriceForSchedule(svc, null))
                : d.StartingPrice;

            return new DaycareCardVm
            {
                Daycare = d,
                DistanceLabel = dist,
                Miles = miles,
                StartingPrice = price,
                PriceUnitLabel = CatalogLocalizer.Loc(
                    Schedule == "medio" ? "/ medio día" : "/ día completo",
                    Schedule == "medio" ? "/ half day" : "/ full day"),
                AvailableToday = todayMap.GetValueOrDefault(d.Id, true),
                Features = d.Amenities.OrderBy(a => a.SortOrder).Select(a => a.Label).Take(3).ToList()
            };
        }).ToList();

        if (userLat != null)
            Results = Results.OrderBy(r => r.Miles ?? double.MaxValue)
                .ThenByDescending(r => r.Daycare.Rating).ToList();

        HasMore = !More && Results.Count > 3;
        if (HasMore)
            Results = Results.Take(3).ToList();

        if (GroomerId.HasValue)
        {
            SelectedDaycare = await _db.Groomers
                .Include(g => g.Services)
                .Include(g => g.Amenities)
                .FirstOrDefaultAsync(g => g.Id == GroomerId && g.IsActive);

            if (SelectedDaycare != null)
            {
                SelectedService = PickService(SelectedDaycare.Services);
                Extras = await _db.ServiceExtras
                    .Where(e => e.GroomerId == SelectedDaycare.Id && e.IsActive)
                    .OrderBy(e => e.Price)
                    .ToListAsync();

                if (SelectedService != null)
                {
                    var basePrice = PriceForSchedule(SelectedService, SelectedPet);
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

    private static bool AmenityMatch(GroomerProfile d, params string[] keys)
    {
        var blob = string.Join(" ", d.Amenities.Select(a => a.Label))
                   + " " + (d.About ?? "");
        return keys.Any(k => blob.Contains(k, StringComparison.OrdinalIgnoreCase));
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

    private decimal PriceForSchedule(GroomerService svc, Pet? pet)
    {
        var full = pet != null ? svc.PriceFor(pet.Size) : svc.PriceSmall;
        if (Schedule == "medio")
        {
            // Si hay servicio "medio" real, PickService ya lo eligió; si no, ~70% del día completo
            var looksHalf = svc.Name.Contains("medio", StringComparison.OrdinalIgnoreCase)
                            || svc.Name.Contains("half", StringComparison.OrdinalIgnoreCase);
            return looksHalf ? full : Math.Round(full * 0.7m, 0);
        }
        return full;
    }

    private void ResolveDate(out DateTime day)
    {
        var today = DateTime.Today;
        if (When.Equals("hoy", StringComparison.OrdinalIgnoreCase))
            day = today;
        else if (When.Equals("manana", StringComparison.OrdinalIgnoreCase))
            day = today.AddDays(1);
        else if (!DateTime.TryParse(Date, out day))
            day = today.AddDays(1);
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
