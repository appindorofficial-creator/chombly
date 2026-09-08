using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Plans;

public class ChomblyCareModel : PageModel
{
    private readonly AuthService _auth;
    private readonly ServiceCatalogService _catalog;
    private readonly ChomblyCareService _care;
    private readonly VetAuditService _audit;
    private readonly ConsentService _consent;

    public ChomblyCareModel(
        AuthService auth,
        ServiceCatalogService catalog,
        ChomblyCareService care,
        VetAuditService audit,
        ConsentService consent)
    {
        _auth = auth;
        _catalog = catalog;
        _care = care;
        _audit = audit;
        _consent = consent;
    }

    [BindProperty(SupportsGet = true)]
    public int? ConsultationId { get; set; }

    [BindProperty]
    public bool AcceptTerms { get; set; }

    [BindProperty]
    public bool AcceptRenewal { get; set; }

    public ServiceCatalogItem? CatalogItem { get; set; }
    public CareSubscription? Subscription { get; set; }
    public int RemainingConsults { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        CatalogItem = await _catalog.GetAsync(ServiceCatalogCodes.ChomblyCare);
        if (_auth.CurrentUserId is int uid)
        {
            Subscription = await _care.GetActiveAsync(uid);
            if (Subscription != null)
                RemainingConsults = _care.RemainingQuickConsults(Subscription);
        }
        return Page();
    }

    public async Task<IActionResult> OnPostActivateAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = "/Plans/ChomblyCare" });

        CatalogItem = await _catalog.GetAsync(ServiceCatalogCodes.ChomblyCare);
        var price = CatalogItem?.Price ?? 14.99m;

        if (!AcceptTerms || !AcceptRenewal)
        {
            ErrorMessage = CatalogLocalizer.Loc(
                "Debes aceptar los términos y la renovación automática para activar Chombly Care.",
                "You must accept the terms and auto-renewal to activate Chombly Care.");
            Subscription = await _care.GetActiveAsync(_auth.CurrentUserId.Value);
            return Page();
        }

        var sub = await _care.ActivateAsync(_auth.CurrentUserId.Value, price);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers.UserAgent.ToString();
        await _consent.SaveAsync(_auth.CurrentUserId.Value, ConsultationId, new[]
        {
            (ConsentService.DocTerms, true),
            ("care_auto_renewal", true)
        }, ip, ua);

        await _audit.LogAsync("care_activated", _auth.CurrentUserId, "CareSubscription", sub.Id,
            new { price });

        Subscription = sub;
        RemainingConsults = _care.RemainingQuickConsults(sub);
        SuccessMessage = CatalogLocalizer.Loc(
            "Chombly Care activado (pago simulado). Renovación mensual hasta que canceles.",
            "Chombly Care activated (simulated payment). Renews monthly until you cancel.");

        if (ConsultationId is int cid)
            return RedirectToPage("/Vet/International/Home", new { consultationId = cid });

        return Page();
    }

    public async Task<IActionResult> OnPostCancelAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login");

        await _care.CancelAtPeriodEndAsync(_auth.CurrentUserId.Value);
        CatalogItem = await _catalog.GetAsync(ServiceCatalogCodes.ChomblyCare);
        Subscription = await _care.GetActiveAsync(_auth.CurrentUserId.Value);
        if (Subscription != null)
            RemainingConsults = _care.RemainingQuickConsults(Subscription);

        SuccessMessage = CatalogLocalizer.Loc(
            "Cancelación programada al final del ciclo actual.",
            "Cancellation scheduled at the end of the current cycle.");
        return Page();
    }
}
