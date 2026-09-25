using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Bookings.CreateBooking;
using WebAppPet.Application.Businesses.SearchBusinesses;
using WebAppPet.Application.Businesses.Shared;
using WebAppPet.Application.Promotions.ApplyPromoCode;
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

    /// <summary>Pets that need medication admin when that extra is selected (multi-pet stays).</summary>
    [BindProperty(SupportsGet = true)]
    public List<int> MedsPetIds { get; set; } = new();

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
    /// <summary>Catalog of extras from listed hotels; availability depends on the selected hotel.</summary>
    public List<HotelExtraOptionVm> ExtraOptions { get; set; } = new();
    public GroomerProfile? SelectedHotel { get; set; }
    public GroomerService? SelectedService { get; set; }
    public Pet? SelectedPet { get; set; }
    public List<Pet> SelectedPets { get; set; } = new();

    /// <summary>Hotel cards can be chosen only after at least one pet is selected.</summary>
    public bool CanSelectHotel => SelectedPets.Count > 0;

    public bool MedsExtraSelected { get; private set; }
    public int? MedsExtraId { get; private set; }
    public bool NeedsMedsPetPick => MedsExtraSelected && SelectedPets.Count > 1;

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

        ResolveDates(out var cin, out var cout);
        if (cout <= cin)
        {
            ErrorMessage = "Revisa las fechas de check-in y check-out.";
            Pay = true;
            return Page();
        }

        var nights = Math.Max(1, (int)(cout.Date - cin.Date).TotalDays);
        var selectedExtras = Extras.Where(e => ExtraIds.Contains(e.Id)).ToList();
        NormalizeMedsPetSelection();
        if (MedsExtraSelected && SelectedPets.Count > 1 && MedsPetIds.Count == 0)
        {
            ErrorMessage = CatalogLocalizer.Loc(
                "Indica para qué mascota(s) es la administración de medicamentos.",
                "Choose which pet(s) need medication administration.");
            Pay = true;
            return Page();
        }

        var subtotal = SelectedPets.Sum(p => SelectedService.PriceFor(p.Size) * nights)
            + SumSelectedExtras(selectedExtras);

        var petNames = string.Join(", ", SelectedPets.Select(p => $"{PetSpecies.Emoji(p.Species)} {p.Name}"));
        var medsPets = SelectedPets.Where(p => MedsPetIds.Contains(p.Id)).ToList();
        var medsPetNames = string.Join(", ", medsPets.Select(p => p.Name));
        var noteParts = new List<string>();
        if (SelectedPets.Count > 1)
            noteParts.Add(CatalogLocalizer.Loc($"Mascotas: {petNames}", $"Pets: {petNames}"));
        if (MedsExtraSelected && medsPets.Count > 0)
            noteParts.Add(CatalogLocalizer.Loc(
                $"Medicamentos: {medsPetNames}",
                $"Medication: {medsPetNames}"));
        if (!string.IsNullOrWhiteSpace(Notes)) noteParts.Add(Notes.Trim());
        if (PaymentMethodId.HasValue)
        {
            var pm = Payments.FirstOrDefault(p => p.Id == PaymentMethodId) ?? DefaultPayment;
            if (pm != null)
                noteParts.Add($"{CatalogLocalizer.Loc("Pago:", "Payment:")} {pm.Brand} •••• {pm.Last4}");
        }

        var extraLines = selectedExtras.Select(ex =>
        {
            var isMeds = HotelCoreExtras.MatchesMeds(ex.Name);
            var qty = isMeds ? Math.Max(1, MedsPetIds.Count) : 1;
            var label = isMeds && medsPets.Count > 0 ? $"{ex.Name} ({medsPetNames})" : ex.Name;
            return new BookingExtraLine(ex.Id, label, ex.Price * qty);
        }).ToList();

        var result = await _createBooking.HandleAsync(new CreateBookingCommand
        {
            ClientId = userId,
            BusinessId = SelectedHotel.Id,
            ServiceId = SelectedService.Id,
            PetIds = SelectedPets.Select(p => p.Id).ToList(),
            StartUtc = AppTimeZones.LocalDateAndTimeToUtc(cin.Date, TimeSpan.FromHours(14)),
            EndUtc = AppTimeZones.LocalDateAndTimeToUtc(cout.Date, TimeSpan.FromHours(11)),
            Nights = nights,
            Subtotal = subtotal,
            PromoCode = PromoCode,
            Extras = extraLines,
            NoteParts = noteParts,
            ClientNotice = new("Reserva de hotel enviada", $"{SelectedHotel.BusinessName} · {nights} noche(s) · pendiente de confirmación."),
            BusinessNotice = new("Nueva reserva de hotel", $"{petNames} · {nights} noche(s).")
        });

        if (!result.Success)
        {
            ErrorMessage = result.Error switch
            {
                CreateBookingError.SpeciesNotAccepted => CatalogLocalizer.Loc(
                    $"Este hotel no atiende {result.RejectedSpecies}.",
                    $"This hotel does not accept {result.RejectedSpecies}."),
                CreateBookingError.InvalidPromo => result.PromoError,
                _ => CatalogLocalizer.Loc("Elige un hotel para continuar.", "Choose a hotel to continue.")
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
        When ??= "hoy";
        Filters ??= new List<string>();
        ExtraIds ??= new List<int>();
        MedsPetIds ??= new List<int>();

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
            .Where(g => g.IsActive && g.PublishStatus == BusinessPublishStatus.Approved);

        var hotels = await hotelsQuery
            .OrderByDescending(g => g.IsFeatured)
            .ThenByDescending(g => g.Rating)
            .ToListAsync();

        if (Category is not null)
            hotels = hotels.Where(h => h.OffersCategory(Category.Id)).ToList();
        else
            hotels = hotels.Where(h => h.Category != null && h.Category.Slug == "hotel").ToList();

        hotels = BusinessMarketResolver.FilterHomeMarket(hotels, AppTimeZones.CurrentCountryCode).ToList();

        // Results must accept every selected pet species.
        if (SelectedPets.Count > 0)
            hotels = hotels.Where(h => BusinessListing.AcceptsAll(h, SelectedPets.Select(p => p.Species))).ToList();
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
        // Calendar-day availability for check-in "hoy", not "open right now".
        if (string.Equals(When, "hoy", StringComparison.OrdinalIgnoreCase))
        {
            var openToday = await _availability.OpenOnAsync(hotels.Select(h => h.Id), cin.Date);
            hotels = hotels.Where(h => openToday.Contains(h.Id)).ToList();
        }

        Results = hotels.Select(h =>
        {
            var (_, dist) = BusinessListing.DistanceFrom(userLat, userLng, h);
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
        Results = BusinessListing.FirstPage(Results, More, out _);

        if (GroomerId.HasValue)
        {
            SelectedHotel = await _db.Groomers
                .Include(g => g.Amenities)
                .Include(g => g.Services)
                .FirstOrDefaultAsync(g => g.Id == GroomerId && g.IsActive);
            if (SelectedHotel != null)
            {
                SelectedService = SelectedHotel.Services.OrderBy(s => s.PriceSmall).FirstOrDefault();
                await HotelCoreExtras.EnsureMissingAsync(_db, new[] { SelectedHotel.Id });
                Extras = await _db.ServiceExtras
                    .Where(e => e.GroomerId == SelectedHotel.Id && e.IsActive)
                    .OrderBy(e => e.Price)
                    .ToListAsync();

                // Drop selections that this hotel does not offer.
                var offeredIds = Extras.Select(e => e.Id).ToHashSet();
                ExtraIds = ExtraIds.Where(id => offeredIds.Contains(id)).ToList();
                NormalizeMedsPetSelection();

                if (SelectedService != null && SelectedPets.Count > 0)
                {
                    var extrasTotal = SumSelectedExtras(Extras);
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
            Extras = new List<ServiceExtra>();
            ExtraIds = new List<int>();
            MedsPetIds = new List<int>();
            MedsExtraSelected = false;
            MedsExtraId = null;
        }

        // Catalog from visible hotels; bath + meds are always offered (price set by each hotel).
        var catalogIds = Results.Select(r => r.Hotel.Id).ToList();
        if (SelectedHotel != null && !catalogIds.Contains(SelectedHotel.Id))
            catalogIds.Add(SelectedHotel.Id);
        await HotelCoreExtras.EnsureMissingAsync(_db, catalogIds);
        await BuildExtraOptionsAsync(catalogIds);
    }

    private async Task BuildExtraOptionsAsync(List<int> hotelIds)
    {
        ExtraOptions = new List<HotelExtraOptionVm>();
        if (hotelIds.Count == 0) return;

        var raw = await _db.ServiceExtras
            .AsNoTracking()
            .Where(e => hotelIds.Contains(e.GroomerId) && e.IsActive)
            .ToListAsync();
        if (raw.Count == 0) return;

        var byName = raw
            .Where(e => !string.IsNullOrWhiteSpace(e.Name))
            .GroupBy(e => e.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => HotelCoreExtras.IsCore(g.Key) ? 0 : 1)
            .ThenBy(g => g.Min(x => x.Price))
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Take(8);

        foreach (var g in byName)
        {
            var isCore = HotelCoreExtras.IsCore(g.Key);
            var offered = SelectedHotel == null
                ? null
                : g.FirstOrDefault(e => e.GroomerId == SelectedHotel.Id);
            string? coreDesc = null;
            decimal? coreDefault = null;
            if (HotelCoreExtras.MatchesBath(g.Key))
            {
                coreDesc = HotelCoreExtras.BathDescription;
                coreDefault = HotelCoreExtras.BathDefaultFor(AppTimeZones.CurrentCountryCode);
            }
            else if (HotelCoreExtras.MatchesMeds(g.Key))
            {
                coreDesc = HotelCoreExtras.MedsDescription;
                coreDefault = HotelCoreExtras.MedsDefaultFor(AppTimeZones.CurrentCountryCode);
            }

            ExtraOptions.Add(new HotelExtraOptionVm
            {
                Name = g.First().Name,
                Description = offered?.Description
                    ?? (isCore ? coreDesc : null)
                    ?? g.Select(x => x.Description).FirstOrDefault(d => !string.IsNullOrWhiteSpace(d)),
                Price = offered?.Price ?? (isCore ? coreDefault ?? g.Min(x => x.Price) : g.Min(x => x.Price)),
                ExtraId = offered?.Id,
                Available = offered != null
            });
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

    private void NormalizeMedsPetSelection()
    {
        MedsPetIds ??= new List<int>();
        var meds = Extras.FirstOrDefault(e => HotelCoreExtras.MatchesMeds(e.Name));
        MedsExtraId = meds?.Id;
        MedsExtraSelected = meds != null && ExtraIds.Contains(meds.Id);

        if (!MedsExtraSelected)
        {
            MedsPetIds = new List<int>();
            return;
        }

        var allowed = SelectedPets.Select(p => p.Id).ToHashSet();
        MedsPetIds = MedsPetIds.Where(allowed.Contains).Distinct().ToList();

        // Single pet stay: medication always applies to that pet.
        if (SelectedPets.Count == 1)
            MedsPetIds = new List<int> { SelectedPets[0].Id };
    }

    private decimal SumSelectedExtras(IEnumerable<ServiceExtra> extras)
    {
        decimal total = 0;
        foreach (var e in extras.Where(x => ExtraIds.Contains(x.Id)))
        {
            if (HotelCoreExtras.MatchesMeds(e.Name))
            {
                var qty = MedsPetIds.Count > 0 ? MedsPetIds.Count : (SelectedPets.Count == 1 ? 1 : 0);
                total += e.Price * qty;
            }
            else
            {
                total += e.Price;
            }
        }
        return total;
    }

    private void ResolveDates(out DateTime cin, out DateTime cout)
    {
        var today = AppTimeZones.TodayLocalDate();
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

    public class HotelExtraOptionVm
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int? ExtraId { get; set; }
        public bool Available { get; set; }
    }
}
