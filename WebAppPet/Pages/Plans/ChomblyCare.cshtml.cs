using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.Care.ActivateCare;
using WebAppPet.Application.Care.CancelCare;
using WebAppPet.Application.Care.GetCarePlan;
using WebAppPet.Domain;
using WebAppPet.Infrastructure.Identity;
using WebAppPet.Localization;

namespace WebAppPet.Pages.Plans;

public class ChomblyCareModel : PageModel
{
    private readonly AuthService _auth;
    private readonly GetCarePlanHandler _getPlan;
    private readonly ActivateCareHandler _activate;
    private readonly CancelCareHandler _cancel;

    public ChomblyCareModel(
        AuthService auth,
        GetCarePlanHandler getPlan,
        ActivateCareHandler activate,
        CancelCareHandler cancel)
    {
        _auth = auth;
        _getPlan = getPlan;
        _activate = activate;
        _cancel = cancel;
    }

    [BindProperty(SupportsGet = true)]
    public int? ConsultationId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty]
    public bool AcceptTerms { get; set; }

    [BindProperty]
    public bool AcceptRenewal { get; set; }

    public ServiceCatalogItem? CatalogItem { get; set; }
    public CareSubscription? Subscription { get; set; }
    public PaymentMethod? Card { get; set; }
    public int RemainingConsults { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public string BackHref { get; private set; } = "/Index";

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        ResolveBackHref();
        await LoadAsync(_auth.CurrentUserId, ct);
        return Page();
    }

    public async Task<IActionResult> OnPostActivateAsync(CancellationToken ct)
    {
        ResolveBackHref();
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login", new { returnUrl = "/Plans/ChomblyCare" });

        var result = await _activate.HandleAsync(new ActivateCareCommand(
            userId,
            ConsultationId,
            AcceptTerms,
            AcceptRenewal,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString()), ct);

        switch (result.Outcome)
        {
            case ActivateCareOutcome.TermsNotAccepted:
                ErrorMessage = CatalogLocalizer.Loc(
                    "Debes aceptar los términos y la renovación automática para activar Chombly Care.",
                    "You must accept the terms and auto-renewal to activate Chombly Care.");
                break;
            case ActivateCareOutcome.NoPaymentMethod:
                ErrorMessage = CatalogLocalizer.Loc(
                    "Agrega un método de pago para activar Chombly Care.",
                    "Add a payment method to activate Chombly Care.");
                break;
            case ActivateCareOutcome.PaymentDeclined:
                ErrorMessage = result.PaymentError;
                break;
            case ActivateCareOutcome.Activated when ConsultationId is int cid:
                return RedirectToPage("/Vet/International/Home", new { consultationId = cid });
            case ActivateCareOutcome.Activated:
                SuccessMessage = CatalogLocalizer.Loc(
                    "Chombly Care activado (pago simulado). Renovación mensual hasta que canceles.",
                    "Chombly Care activated (simulated payment). Renews monthly until you cancel.");
                break;
        }

        await LoadAsync(userId, ct);
        return Page();
    }

    public async Task<IActionResult> OnPostCancelAsync(CancellationToken ct)
    {
        ResolveBackHref();
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login");

        await _cancel.HandleAsync(new CancelCareCommand(userId), ct);
        await LoadAsync(userId, ct);
        SuccessMessage = CatalogLocalizer.Loc(
            "Cancelación programada al final del ciclo actual.",
            "Cancellation scheduled at the end of the current cycle.");
        return Page();
    }

    private async Task LoadAsync(int? userId, CancellationToken ct)
    {
        var plan = await _getPlan.HandleAsync(new GetCarePlanQuery(userId), ct);
        CatalogItem = plan.CatalogItem;
        Subscription = plan.Subscription;
        RemainingConsults = plan.RemainingConsults;
        Card = plan.Card;
    }

    private void ResolveBackHref()
    {
        if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
        {
            BackHref = ReturnUrl;
            return;
        }

        var referer = Request.Headers.Referer.ToString();
        if (Uri.TryCreate(referer, UriKind.Absolute, out var uri)
            && string.Equals(uri.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase)
            && !uri.AbsolutePath.Contains("/Plans/ChomblyCare", StringComparison.OrdinalIgnoreCase)
            && !uri.AbsolutePath.Contains("/Legal/Terms", StringComparison.OrdinalIgnoreCase)
            && Url.IsLocalUrl(uri.PathAndQuery))
        {
            BackHref = uri.PathAndQuery;
            return;
        }

        BackHref = Url.Page("/Pets/Index") ?? "/Pets";
    }
}
