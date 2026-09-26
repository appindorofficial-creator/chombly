using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.Consultations.BookConsultation;
using WebAppPet.Application.Consultations.GetConsultationCheckout;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Vet.Virtual;

public class CheckoutModel : PageModel
{
    private readonly AuthService _auth;
    private readonly GetConsultationCheckoutHandler _getCheckout;
    private readonly BookConsultationHandler _book;

    public CheckoutModel(AuthService auth, GetConsultationCheckoutHandler getCheckout, BookConsultationHandler book)
    {
        _auth = auth;
        _getCheckout = getCheckout;
        _book = book;
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
    public string HomeCountryCode { get; set; } = MarketCountry.DefaultIso;

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Vet/Virtual/Checkout?consultationId={ConsultationId}" });

        var details = await _getCheckout.HandleAsync(new GetConsultationCheckoutQuery(userId, ConsultationId, LogStarted: true));
        if (details is null) return RedirectToPage("/Vet/Index");
        Show(details);

        // Default: Care if available, otherwise pay.
        PayMode = HasCareAvailable ? "care" : "pay";
        UsingCareBenefit = PayMode == "care";
        ChargeAmount = UsingCareBenefit ? 0 : CatalogPrice;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (_auth.CurrentUserId is not int userId) return RedirectToPage("/Account/Login");

        var wantsCare = string.Equals(PayMode, "care", StringComparison.OrdinalIgnoreCase);
        var result = await _book.HandleAsync(new BookConsultationCommand(
            userId, ConsultationId, AcceptTerms, AcceptScope, AcceptMedia, PaymentMethodId, wantsCare,
            HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString()));

        if (result.Outcome == BookConsultationOutcome.NotFound)
            return RedirectToPage("/Vet/Index");
        if (result.Outcome == BookConsultationOutcome.Booked)
            return RedirectToPage("/Vet/Virtual/Summary", new { id = ConsultationId });

        Show(result.Details!);
        if (result.Outcome == BookConsultationOutcome.Incomplete)
            return Page();

        UsingCareBenefit = result.UsingCare;
        ChargeAmount = UsingCareBenefit ? 0 : CatalogPrice;
        if (wantsCare && !HasCareAvailable)
            PayMode = "pay";

        ErrorMessage = result.Outcome switch
        {
            BookConsultationOutcome.NoPaymentMethod => CatalogLocalizer.Loc("Agrega un método de pago para continuar.", "Add a payment method to continue."),
            BookConsultationOutcome.PaymentDeclined => result.PaymentError,
            _ => CatalogLocalizer.Loc("No se pudo aplicar el beneficio Care.", "Could not apply Care benefit.")
        };
        return Page();
    }

    private void Show(CheckoutDetails details)
    {
        Consultation = details.Consultation;
        CatalogItem = details.CatalogItem;
        Provider = details.Provider;
        CatalogPrice = details.CatalogPrice;
        CareRemaining = details.CareRemaining;
        HasCareAvailable = details.HasCareAvailable;
        HomeCountryCode = details.HomeCountryCode;
        Payments = details.Payments;
        DefaultPayment = details.DefaultPayment;
        if (PaymentMethodId is null && DefaultPayment != null)
            PaymentMethodId = DefaultPayment.Id;
    }
}
