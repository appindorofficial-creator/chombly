using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Booking;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly PromoCodeService _promo;
    private readonly IStringLocalizer<SharedResource> _L;

    public IndexModel(AppDbContext db, AuthService auth, PromoCodeService promo, IStringLocalizer<SharedResource> L)
    {
        _db = db;
        _auth = auth;
        _promo = promo;
        _L = L;
    }

    [BindProperty(SupportsGet = true)]
    public int GroomerId { get; set; }

    /// <summary>Optional popular-service name from Home → Groomers (pre-selects matching catalog service).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Service { get; set; }

    [BindProperty]
    public int Step { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int ServiceId { get; set; }

    [BindProperty]
    public int PetId { get; set; }

    [BindProperty]
    public string Date { get; set; } = string.Empty;

    [BindProperty]
    public string EndDate { get; set; } = string.Empty;

    [BindProperty]
    public string Time { get; set; } = string.Empty;

    [BindProperty]
    public string? Notes { get; set; }

    [BindProperty]
    public List<int> SelectedExtraIds { get; set; } = new();

    [BindProperty]
    public string? PromoCode { get; set; }

    public GroomerProfile? Groomer { get; set; }
    public bool IsOvernight { get; set; }
    public List<GroomerService> Services { get; set; } = new();
    public List<ServiceExtra> Extras { get; set; } = new();
    public List<Pet> Pets { get; set; } = new();
    public List<string> TimeSlots { get; } = new()
    {
        "9:00 AM", "10:00 AM", "11:00 AM", "12:00 PM", "1:00 PM", "2:00 PM", "3:00 PM", "4:00 PM", "5:00 PM"
    };

    public GroomerService? SelectedService { get; set; }
    public Pet? SelectedPet { get; set; }
    public List<ServiceExtra> SelectedExtras { get; set; } = new();
    public int Nights { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal EstimatedTotal { get; set; }
    public decimal Deposit { get; set; }
    public string? PromoError { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login");

        await LoadAsync();
        if (Groomer == null) return RedirectToPage("/Groomers/Index");
        Step = 1;
        ApplyServicePreselect();
        return Page();
    }

    private void ApplyServicePreselect()
    {
        if (ServiceId != 0)
        {
            if (!Services.Any(s => s.Id == ServiceId))
                ServiceId = 0;
            return;
        }

        if (string.IsNullOrWhiteSpace(Service) || Services.Count == 0)
            return;

        var exact = Services.FirstOrDefault(s =>
            s.Name.Equals(Service, StringComparison.OrdinalIgnoreCase));
        if (exact != null)
        {
            ServiceId = exact.Id;
            return;
        }

        var partial = Services.FirstOrDefault(s =>
            s.Name.Contains(Service, StringComparison.OrdinalIgnoreCase)
            || Service.Contains(s.Name, StringComparison.OrdinalIgnoreCase));
        if (partial != null)
            ServiceId = partial.Id;
    }

    public async Task<IActionResult> OnPostAsync(string handler)
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login");

        await LoadAsync();
        if (Groomer == null) return RedirectToPage("/Groomers/Index");

        if (handler == "Back")
        {
            Step = Math.Max(1, Step - 1);
            ModelState.Remove(nameof(Step));
            await PrepareConfirmAsync(applyPromo: Step == 4);
            return Page();
        }

        if (handler == "ApplyPromo")
        {
            Step = 4;
            ModelState.Remove(nameof(Step));
            await PrepareConfirmAsync(applyPromo: true);
            return Page();
        }

        if (handler == "Next")
        {
            if (Step == 1)
            {
                if (ServiceId == 0 || !Services.Any(s => s.Id == ServiceId))
                {
                    ErrorMessage = _L["Booking_SelectService"].Value;
                    ServiceId = 0;
                    return Page();
                }
            }

            if (Step == 2)
            {
                if (IsOvernight)
                {
                    if (!DateTime.TryParse(Date, out var cin) || !DateTime.TryParse(EndDate, out var cout) || cout <= cin)
                    {
                        ErrorMessage = _L["Booking_CheckDates"].Value;
                        return Page();
                    }
                    if (cin.Date < AppTimeZones.TodayLocalDate())
                    {
                        ErrorMessage = _L["Booking_DateNotPast"].Value;
                        return Page();
                    }
                }
                else if (string.IsNullOrWhiteSpace(Date) || string.IsNullOrWhiteSpace(Time))
                {
                    ErrorMessage = _L["Booking_SelectDateTime"].Value;
                    return Page();
                }
                else if (DateTime.TryParse(Date, out var day) && day.Date < AppTimeZones.TodayLocalDate())
                {
                    ErrorMessage = _L["Booking_DateNotPast"].Value;
                    return Page();
                }
            }

            if (Step == 3)
            {
                if (PetId == 0)
                {
                    ErrorMessage = _L["Booking_SelectPet"].Value;
                    return Page();
                }
                var petCheck = Pets.FirstOrDefault(p => p.Id == PetId);
                if (petCheck != null && !Groomer.AcceptsSpecies(petCheck.Species))
                {
                    ErrorMessage = string.Format(_L["Booking_SpeciesNotAccepted"].Value, petCheck.Species);
                    return Page();
                }
            }

            Step = Math.Min(4, Step + 1);
            ModelState.Remove(nameof(Step));
            await PrepareConfirmAsync(applyPromo: Step == 4);
            return Page();
        }

        if (handler == "Confirm")
        {
            await PrepareConfirmAsync(applyPromo: true);
            if (SelectedService == null || SelectedPet == null)
            {
                ErrorMessage = _L["Booking_MissingData"].Value;
                Step = 1;
                ModelState.Remove(nameof(Step));
                return Page();
            }

            if (!string.IsNullOrWhiteSpace(PromoCode) && !string.IsNullOrEmpty(PromoError))
            {
                Step = 4;
                ModelState.Remove(nameof(Step));
                return Page();
            }

            if (!Groomer.AcceptsSpecies(SelectedPet.Species))
            {
                ErrorMessage = string.Format(_L["Booking_SpeciesNotAccepted"].Value, SelectedPet.Species);
                Step = 3;
                ModelState.Remove(nameof(Step));
                return Page();
            }

            DateTime scheduled;
            DateTime? endAt = null;
            var nights = 0;

            if (IsOvernight)
            {
                if (!DateTime.TryParse(Date, out var cin) || !DateTime.TryParse(EndDate, out var cout) || cout <= cin)
                {
                    ErrorMessage = _L["Booking_InvalidDates"].Value;
                    Step = 2;
                    ModelState.Remove(nameof(Step));
                    return Page();
                }
                if (cin.Date < AppTimeZones.TodayLocalDate())
                {
                    ErrorMessage = _L["Booking_DateNotPast"].Value;
                    Step = 2;
                    ModelState.Remove(nameof(Step));
                    return Page();
                }
                scheduled = cin.Date.AddHours(14); // check-in default 2pm
                endAt = cout.Date.AddHours(11);    // check-out default 11am
                nights = Math.Max(1, (int)(cout.Date - cin.Date).TotalDays);
            }
            else
            {
                if (!DateTime.TryParse($"{Date} {Time}", out scheduled))
                {
                    ErrorMessage = _L["Booking_InvalidDateTime"].Value;
                    Step = 2;
                    ModelState.Remove(nameof(Step));
                    return Page();
                }
                if (scheduled.Date < AppTimeZones.TodayLocalDate())
                {
                    ErrorMessage = _L["Booking_DateNotPast"].Value;
                    Step = 2;
                    ModelState.Remove(nameof(Step));
                    return Page();
                }
            }

            var appt = new Appointment
            {
                ClientId = userId,
                PetId = PetId,
                GroomerId = GroomerId,
                ServiceId = ServiceId,
                ScheduledAt = scheduled,
                EndAt = endAt,
                Nights = nights,
                Status = AppointmentStatus.Pending,
                TotalPrice = EstimatedTotal,
                DepositPaid = Deposit,
                PromoCode = DiscountAmount > 0 ? PromoCode?.Trim().ToUpperInvariant() : null,
                DiscountAmount = DiscountAmount,
                Notes = Notes
            };

            foreach (var ex in SelectedExtras)
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
                Title = "Reserva enviada",
                Message = $"Tu solicitud en {Groomer.BusinessName} está pendiente de confirmación.",
                Type = "appointment"
            });
            _db.Notifications.Add(new AppNotification
            {
                UserId = Groomer.UserId,
                Title = "Nueva solicitud de reserva",
                Message = $"{SelectedPet.Name} · {SelectedService.Name}.",
                Type = "appointment"
            });
            await _db.SaveChangesAsync();

            return RedirectToPage("./Confirm", new { id = appt.Id });
        }

        return Page();
    }

    private async Task LoadAsync()
    {
        Groomer = await _db.Groomers
            .Include(g => g.Category)
            .FirstOrDefaultAsync(g => g.Id == GroomerId && g.IsActive);

        IsOvernight = Groomer?.Category?.IsOvernight == true
            || string.Equals(Groomer?.Category?.Slug, "hotel", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Groomer?.Category?.Slug, "daycare", StringComparison.OrdinalIgnoreCase);

        Services = await _db.Services.Where(s => s.GroomerId == GroomerId).ToListAsync();
        Extras = await _db.ServiceExtras.Where(e => e.GroomerId == GroomerId && e.IsActive).ToListAsync();

        if (_auth.CurrentUserId is int userId)
            Pets = await _db.Pets.Where(p => p.OwnerId == userId).ToListAsync();
    }

    private async Task PrepareConfirmAsync(bool applyPromo)
    {
        SelectedService = await _db.Services.FirstOrDefaultAsync(s => s.Id == ServiceId);
        SelectedPet = await _db.Pets.FirstOrDefaultAsync(p => p.Id == PetId);
        SelectedExtras = Extras.Where(e => SelectedExtraIds.Contains(e.Id)).ToList();

        Nights = 0;
        Subtotal = 0;
        DiscountAmount = 0;
        EstimatedTotal = 0;
        PromoError = null;

        if (SelectedService != null && SelectedPet != null)
        {
            var unit = SelectedService.PriceFor(SelectedPet.Size);
            if (IsOvernight && DateTime.TryParse(Date, out var cin) && DateTime.TryParse(EndDate, out var cout) && cout > cin)
            {
                Nights = Math.Max(1, (int)(cout.Date - cin.Date).TotalDays);
                Subtotal = unit * Nights;
            }
            else
            {
                Subtotal = unit;
            }

            Subtotal += SelectedExtras.Sum(e => e.Price);
            EstimatedTotal = Subtotal;

            if (applyPromo)
            {
                var promo = await _promo.TryApplyAsync(_auth.CurrentUserId, PromoCode, Subtotal);
                if (!string.IsNullOrWhiteSpace(PromoCode))
                {
                    if (promo.IsValid)
                    {
                        DiscountAmount = promo.DiscountAmount;
                        EstimatedTotal = promo.FinalTotal;
                        PromoCode = promo.NormalizedCode;
                    }
                    else if (promo.ErrorMessage != null)
                    {
                        PromoError = promo.ErrorMessage;
                        DiscountAmount = 0;
                        EstimatedTotal = Subtotal;
                    }
                }
            }

            Deposit = Math.Round(EstimatedTotal * 0.35m, 2);
            if (Deposit < 15) Deposit = Math.Min(15, EstimatedTotal);
        }
    }
}
