using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Vet.Virtual;

public class CheckoutModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly ConsultationFlowService _flow;
    private readonly ServiceCatalogService _catalog;
    private readonly ConsentService _consent;
    private readonly VetAuditService _audit;
    private readonly ChomblyCareService _care;

    public CheckoutModel(
        AppDbContext db,
        AuthService auth,
        ConsultationFlowService flow,
        ServiceCatalogService catalog,
        ConsentService consent,
        VetAuditService audit,
        ChomblyCareService care)
    {
        _db = db;
        _auth = auth;
        _flow = flow;
        _catalog = catalog;
        _consent = consent;
        _audit = audit;
        _care = care;
    }

    [BindProperty(SupportsGet = true)]
    public int ConsultationId { get; set; }

    [BindProperty] public bool AcceptTerms { get; set; }
    [BindProperty] public bool AcceptScope { get; set; }
    [BindProperty] public bool AcceptMedia { get; set; }
    [BindProperty] public int? PaymentMethodId { get; set; }

    /// <summary>care | pay</summary>
    [BindProperty]
    public string PayMode { get; set; } = "pay";

    public Consultation? Consultation { get; set; }
    public ServiceCatalogItem? CatalogItem { get; set; }
    public GroomerProfile? Provider { get; set; }
    public List<PaymentMethod> Payments { get; set; } = new();
    public PaymentMethod? DefaultPayment { get; set; }
    public bool HasCareAvailable { get; set; }
    public int CareRemaining { get; set; }
    public bool UsingCareBenefit { get; set; }
    public decimal CatalogPrice { get; set; }
    public decimal ChargeAmount { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Vet/Virtual/Checkout?consultationId={ConsultationId}" });

        if (!await LoadAsync()) return RedirectToPage("/Vet/Index");

        // Default: Care if available, otherwise pay.
        PayMode = HasCareAvailable ? "care" : "pay";
        UsingCareBenefit = PayMode == "care";
        ChargeAmount = UsingCareBenefit ? 0 : CatalogPrice;

        await _audit.LogAsync("checkout_started", _auth.CurrentUserId, "Consultation", ConsultationId);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (_auth.CurrentUserId is null) return RedirectToPage("/Account/Login");
        if (!await LoadAsync()) return RedirectToPage("/Vet/Index");

        if (!AcceptTerms || !AcceptScope)
            return Page();

        if (Consultation!.ProviderId is null || Consultation.PetId is null || Consultation.ScheduledAt is null || CatalogItem is null)
            return Page();

        var wantCare = string.Equals(PayMode, "care", StringComparison.OrdinalIgnoreCase);
        UsingCareBenefit = wantCare && HasCareAvailable;
        ChargeAmount = UsingCareBenefit ? 0 : CatalogPrice;

        if (wantCare && !HasCareAvailable)
        {
            UsingCareBenefit = false;
            ChargeAmount = CatalogPrice;
            PayMode = "pay";
        }

        PaymentMethod? selectedPm = null;
        if (!UsingCareBenefit)
        {
            selectedPm = PaymentMethodId is int payId
                ? Payments.FirstOrDefault(p => p.Id == payId)
                : DefaultPayment;
            if (selectedPm is null)
            {
                ErrorMessage = CatalogLocalizer.Loc(
                    "Agrega un método de pago para continuar.",
                    "Add a payment method to continue.");
                return Page();
            }

            PaymentMethodId = selectedPm.Id;
        }

        var service = await _db.Services.FirstOrDefaultAsync(s => s.GroomerId == Consultation.ProviderId);
        if (service is null)
        {
            service = new GroomerService
            {
                GroomerId = Consultation.ProviderId.Value,
                Name = CatalogItem.NameEs,
                Description = CatalogItem.ScopeEs,
                DurationMinutes = CatalogItem.DurationMinutes,
                PriceSmall = CatalogItem.Price,
                PriceMedium = CatalogItem.Price,
                PriceLarge = CatalogItem.Price,
                PriceGiant = CatalogItem.Price
            };
            _db.Services.Add(service);
            await _db.SaveChangesAsync();
        }

        var payNote = UsingCareBenefit
            ? " | Chombly Care benefit"
            : selectedPm != null
                ? $" | Card {selectedPm.Brand} •••• {selectedPm.Last4}"
                : "";

        var appt = new Appointment
        {
            ClientId = _auth.CurrentUserId.Value,
            PetId = Consultation.PetId.Value,
            GroomerId = Consultation.ProviderId.Value,
            ServiceId = service.Id,
            ScheduledAt = Consultation.ScheduledAt.Value,
            Status = AppointmentStatus.Pending,
            TotalPrice = ChargeAmount,
            DepositPaid = ChargeAmount,
            Notes = $"Vet consultation #{Consultation.Id} ({CatalogItem.Code}){payNote}. Symptoms: {Consultation.Symptoms}",
            CreatedAt = DateTime.UtcNow
        };
        _db.Appointments.Add(appt);
        await _db.SaveChangesAsync();

        if (UsingCareBenefit)
        {
            var ok = await _care.TryConsumeQuickConsultAsync(_auth.CurrentUserId.Value, Consultation.Id);
            if (!ok)
            {
                ErrorMessage = CatalogLocalizer.Loc("No se pudo aplicar el beneficio Care.", "Could not apply Care benefit.");
                return Page();
            }
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers.UserAgent.ToString();
        await _consent.SaveAsync(_auth.CurrentUserId.Value, Consultation.Id, new[]
        {
            (ConsentService.DocTerms, AcceptTerms),
            (ConsentService.DocPrivacy, AcceptTerms),
            (ConsentService.DocIntlOrientation, AcceptScope && CatalogItem.Code == ServiceCatalogCodes.VetIntl30),
            (ConsentService.DocMedia, AcceptMedia)
        }, ip, ua);

        Consultation.AppointmentId = appt.Id;
        Consultation.PriceCharged = ChargeAmount;
        Consultation.UsesCareBenefit = UsingCareBenefit;
        Consultation.Status = ConsultationStatus.Scheduled;
        Consultation.ResponsibleName = Provider?.BusinessName;
        await _flow.TouchAsync(Consultation);

        _db.Notifications.Add(new AppNotification
        {
            UserId = _auth.CurrentUserId.Value,
            Title = CatalogLocalizer.Loc("Consulta reservada", "Consultation booked"),
            Message = CatalogLocalizer.Loc(
                $"Tu consulta ({CatalogItem.Code}) quedó pendiente de confirmación.",
                $"Your consultation ({CatalogItem.Code}) is pending confirmation."),
            Type = "vet-consultation",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        await _audit.LogAsync("payment_completed", _auth.CurrentUserId, "Consultation", Consultation.Id,
            new { price = ChargeAmount, care = UsingCareBenefit, code = CatalogItem.Code, appointmentId = appt.Id });

        return RedirectToPage("/Vet/Virtual/Summary", new { id = Consultation.Id });
    }

    private async Task<bool> LoadAsync()
    {
        Consultation = await _flow.GetOwnedAsync(ConsultationId);
        if (Consultation is null || Consultation.Status < ConsultationStatus.ProviderSelected)
            return false;

        CatalogItem = await _catalog.GetAsync(Consultation.ServiceCatalogCode ?? "");
        if (CatalogItem is null) return false;

        if (Consultation.ProviderId is int pid)
            Provider = await _db.Groomers.AsNoTracking().FirstOrDefaultAsync(g => g.Id == pid);

        CatalogPrice = Provider?.StartingPrice > 0 ? Provider.StartingPrice : CatalogItem.Price;

        if (_auth.CurrentUserId is int uid)
        {
            var sub = await _care.GetActiveAsync(uid);
            CareRemaining = sub != null ? _care.RemainingQuickConsults(sub) : 0;
            HasCareAvailable = CareRemaining > 0;
        }

        Payments = await _db.PaymentMethods.AsNoTracking()
            .Where(p => p.UserId == _auth.CurrentUserId)
            .OrderByDescending(p => p.IsDefault)
            .ToListAsync();
        DefaultPayment = Payments.FirstOrDefault(p => p.IsDefault) ?? Payments.FirstOrDefault();
        if (PaymentMethodId is null && DefaultPayment != null)
            PaymentMethodId = DefaultPayment.Id;

        return true;
    }
}
