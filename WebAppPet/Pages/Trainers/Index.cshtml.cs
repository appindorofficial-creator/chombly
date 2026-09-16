using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
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
    private readonly AvailabilityService _availability;
    private readonly PromoCodeService _promo;

    public IndexModel(AppDbContext db, AuthService auth, AvailabilityService availability, PromoCodeService promo)
    {
        _db = db;
        _auth = auth;
        _availability = availability;
        _promo = promo;
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
    public List<string> Prefs { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? GroomerId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Notes { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool More { get; set; }

    public static readonly string[] TimeSlots =
    {
        "9:00 AM", "10:00 AM", "11:00 AM", "1:00 PM", "2:00 PM", "3:00 PM", "5:00 PM"
    };

    public HashSet<string> OccupiedSlots { get; set; } = new(StringComparer.OrdinalIgnoreCase);

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
    public bool HasSlot => TimeSlots.Contains(Slot, StringComparer.OrdinalIgnoreCase);
    public bool HasDate => BookingDate.TryParseSelected(Date, out _);

    /// <summary>Steps 1–6 complete: need, date, time, place, sessions, and pet.</summary>
    public bool HasBookingBasics =>
        HasNeed
        && HasDate
        && HasSlot
        && HasPlace
        && HasSessions
        && PetId > 0
        && SelectedPet != null;

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

        if (SelectedTrainer == null || SelectedService == null || SelectedPet == null)
        {
            ErrorMessage = CatalogLocalizer.Loc(
                "Elige entrenador y mascota para continuar.",
                "Choose a trainer and pet to continue.");
            return Page();
        }

        if (!SelectedTrainer.AcceptsSpecies(SelectedPet.Species))
        {
            ErrorMessage = CatalogLocalizer.Loc(
                $"Este entrenador no atiende {PetSpecies.Label(SelectedPet.Species)}.",
                $"This trainer does not serve {PetSpecies.Label(SelectedPet.Species)}.");
            return Page();
        }

        if (Sessions is not (1 or 4 or 8)) Sessions = 1;

        if (!TryResolveSchedule(SelectedTrainer.Id, out var start, out var scheduleError))
        {
            ErrorMessage = scheduleError;
            return Page();
        }

        var subtotal = UnitPrice * Sessions;
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

        var noteParts = new List<string>
        {
            $"{CatalogLocalizer.Loc("Tipo:", "Type:")} {NeedLabel}",
            $"{CatalogLocalizer.Loc("Lugar:", "Place:")} {PlaceLabel}",
            PackageLabel,
            CatalogLocalizer.Loc("1 sesión / semana", "1 session / week")
        };
        if (!string.IsNullOrWhiteSpace(Notes)) noteParts.Add(Notes.Trim());
        if (PaymentMethodId.HasValue || DefaultPayment != null)
        {
            var pm = Payments.FirstOrDefault(p => p.Id == PaymentMethodId) ?? DefaultPayment;
            if (pm != null)
                noteParts.Add($"{CatalogLocalizer.Loc("Pago:", "Payment:")} {pm.Brand} •••• {pm.Last4}");
        }

        var appt = new Appointment
        {
            ClientId = userId,
            PetId = SelectedPet.Id,
            GroomerId = SelectedTrainer.Id,
            ServiceId = SelectedService.Id,
            ScheduledAt = start,
            EndAt = start.AddMinutes(SelectedService.DurationMinutes > 0 ? SelectedService.DurationMinutes : 60),
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
            Title = "Reserva de training enviada",
            Message = $"{SelectedTrainer.BusinessName} · {PackageLabel} · pendiente de confirmación.",
            Type = "appointment"
        });
        _db.Notifications.Add(new AppNotification
        {
            UserId = SelectedTrainer.UserId,
            Title = "Nueva reserva de training",
            Message = $"{SelectedPet.Name} · {NeedLabel} · {PackageLabel}.",
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
        if (Sessions is not (0 or 1 or 4 or 8)) Sessions = 0;

        Category = await _db.Categories.FirstOrDefaultAsync(c => c.Slug == "trainers" && c.IsActive);
        (When, Date) = BookingDate.NormalizeFromLegacy(When, Date);
        if (!string.IsNullOrWhiteSpace(Slot) && !TimeSlots.Contains(Slot, StringComparer.OrdinalIgnoreCase))
            Slot = "";

        var day = ResolveDay();
        if (GroomerId is int bookedGroomerId && HasDate)
            await LoadOccupiedSlotsAsync(bookedGroomerId, day);

        if (!string.IsNullOrWhiteSpace(Slot) && OccupiedSlots.Contains(Slot))
        {
            var free = TimeSlots.FirstOrDefault(t => !OccupiedSlots.Contains(t));
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
            SelectedPet = PetId > 0 ? Pets.FirstOrDefault(p => p.Id == PetId) : null;

            Payments = await _db.PaymentMethods.Where(p => p.UserId == userId)
                .OrderByDescending(p => p.IsDefault).ToListAsync();
            DefaultPayment = Payments.FirstOrDefault(p => p.IsDefault) ?? Payments.FirstOrDefault();
            if (PaymentMethodId == null && DefaultPayment != null)
                PaymentMethodId = DefaultPayment.Id;
        }

        var trainers = await _db.Groomers
            .Include(g => g.Category)
            .Include(g => g.Amenities)
            .Include(g => g.Services)
            .Where(g => g.IsActive && g.PublishStatus == BusinessPublishStatus.Approved && g.Category != null && g.Category.Slug == "trainers")
            .OrderByDescending(g => g.IsFeatured)
            .ThenByDescending(g => g.Rating)
            .ToListAsync();

        var todayMap = await _availability.TodayMapAsync(trainers.Select(t => t.Id));

        // Filtrar por modalidad (domicilio / centro / virtual)
        trainers = trainers.Where(t => MatchesPlace(t)).ToList();

        if (Prefs.Contains("certificado"))
            trainers = trainers.Where(t => AmenityMatch(t, "certific", "certificado", "certified")).ToList();

        if (Prefs.Contains("raza"))
            trainers = trainers.Where(t => AmenityMatch(t, "raza", "breed", "experiencia")).ToList();

        if (Prefs.Contains("parque"))
            trainers = trainers.Where(t => AmenityMatch(t, "parque", "park")).ToList();

        if (Prefs.Contains("flexible"))
            trainers = trainers.Where(t => AmenityMatch(t, "flexible", "horario")).ToList();

        Results = trainers.Select(t =>
        {
            double? miles = null;
            string? dist = null;
            if (userLat != null && userLng != null && (t.Latitude != 0 || t.Longitude != 0))
            {
                miles = GeoHelper.MilesBetween(userLat.Value, userLng.Value, t.Latitude, t.Longitude);
                var km = miles is null ? null : miles * 1.609344;
                dist = GeoHelper.FormatDistanceOrPlace(km, t.City, t.Address);
            }

            var svc = PickService(t.Services);
            var unit = svc != null
                ? (SelectedPet != null ? svc.PriceFor(SelectedPet.Size) : svc.PriceSmall)
                : t.StartingPrice;

            return new TrainerCardVm
            {
                Trainer = t,
                DistanceLabel = dist,
                Miles = miles,
                UnitPrice = unit,
                AvailableToday = todayMap.GetValueOrDefault(t.Id, false),
                AvailabilityLabel = todayMap.GetValueOrDefault(t.Id, false) ? "Abierto ahora" : "Cerrado ahora"
            };
        }).ToList();

        if (userLat != null)
            Results = Results.OrderBy(r => r.Miles ?? double.MaxValue)
                .ThenByDescending(r => r.Trainer.Rating).ToList();

        HasMore = !More && Results.Count > 3;
        if (HasMore)
            Results = Results.Take(3).ToList();

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
                    UnitPrice = SelectedPet != null
                        ? SelectedService.PriceFor(SelectedPet.Size)
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

    private bool MatchesPlace(GroomerProfile t)
    {
        return Place switch
        {
            "domicilio" => AmenityMatch(t, "domicilio", "home", "casa")
                           || t.Type == GroomerType.InHome || t.Type == GroomerType.Mobile
                           || t.Amenities.Count == 0,
            "centro" => AmenityMatch(t, "centro", "lugar", "salon", "salón")
                        || t.Type == GroomerType.Salon
                        || t.Amenities.Count == 0,
            "virtual" => AmenityMatch(t, "virtual", "online", "zoom", "remoto")
                         || (t.About?.Contains("virtual", StringComparison.OrdinalIgnoreCase) ?? false)
                         || t.Amenities.Count == 0,
            _ => true
        };
    }

    private static bool AmenityMatch(GroomerProfile t, params string[] keys)
    {
        var blob = string.Join(" ", t.Amenities.Select(a => a.Label)) + " " + (t.About ?? "");
        return keys.Any(k => blob.Contains(k, StringComparison.OrdinalIgnoreCase));
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

        if (groomerId is not int gid)
        {
            if (startUtc <= DateTime.UtcNow)
            {
                error = CatalogLocalizer.Loc(
                    "No puedes elegir una fecha u hora en el pasado.",
                    "You can't select a past date or time.");
                return false;
            }
            return true;
        }

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
