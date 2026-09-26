using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.Payments.GetAdminPayments;
using WebAppPet.Application.Payments.RefundPayment;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Admin;

public class PaymentsModel : PageModel
{
    private readonly AuthService _auth;
    private readonly GetAdminPaymentsHandler _getPayments;
    private readonly RefundPaymentHandler _refund;

    public PaymentsModel(AuthService auth, GetAdminPaymentsHandler getPayments, RefundPaymentHandler refund)
    {
        _auth = auth;
        _getPayments = getPayments;
        _refund = refund;
    }

    [BindProperty(SupportsGet = true)] public PaymentTransactionStatus? Status { get; set; }
    [BindProperty(SupportsGet = true)] public PaymentPurpose? Purpose { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? To { get; set; }

    public AdminPaymentsView View { get; set; } = new([], [], 0);
    public string? Message { get; set; }
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!_auth.IsAdmin) return RedirectToPage("/Account/Login");
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRefundAsync(int id, string? reason)
    {
        if (!_auth.IsAdmin) return RedirectToPage("/Account/Login");
        var result = await _refund.HandleAsync(new RefundPaymentCommand(id, _auth.CurrentUserId, reason ?? ""));
        if (result.Success)
            Message = CatalogLocalizer.Loc($"Pago #{id} reembolsado.", $"Payment #{id} refunded.");
        else
            Error = result.Error;
        await LoadAsync();
        return Page();
    }

    private async Task LoadAsync()
    {
        View = await _getPayments.HandleAsync(new GetAdminPaymentsQuery(
            Status,
            Purpose,
            From is DateTime from ? AppTimeZones.LocalDateAndTimeToUtc(from.Date, TimeSpan.Zero) : null,
            To is DateTime to ? AppTimeZones.LocalDateAndTimeToUtc(to.Date.AddDays(1), TimeSpan.Zero) : null));
    }
}
