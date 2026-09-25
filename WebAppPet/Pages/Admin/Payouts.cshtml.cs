using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.Payments.GetAdminPayouts;
using WebAppPet.Application.Payments.MarkPayoutPaid;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Admin;

public class PayoutsModel : PageModel
{
    private readonly AuthService _auth;
    private readonly GetAdminPayoutsHandler _getPayouts;
    private readonly MarkPayoutPaidHandler _markPaid;

    public PayoutsModel(AuthService auth, GetAdminPayoutsHandler getPayouts, MarkPayoutPaidHandler markPaid)
    {
        _auth = auth;
        _getPayouts = getPayouts;
        _markPaid = markPaid;
    }

    public List<ProviderPayout> Pending { get; set; } = new();
    public List<ProviderPayout> Recent { get; set; } = new();
    public string? Message { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!_auth.IsAdmin) return RedirectToPage("/Account/Login");
        var refreshed = await LoadAsync(refreshTotals: true);
        if (refreshed > 0)
            Message = $"Totales actualizados en {refreshed} payout(s) (incluyen reservas familia).";
        return Page();
    }

    public async Task<IActionResult> OnPostMarkPaidAsync(int id)
    {
        if (!_auth.IsAdmin) return RedirectToPage("/Account/Login");
        var payout = await _markPaid.HandleAsync(new MarkPayoutPaidCommand(id, _auth.CurrentUserId));
        Message = payout is null
            ? "Payout not found."
            : $"Marked paid · neto {AppMoney.Format(payout.NetAmountUsd, payout.ProviderUser?.CountryCode)} · {payout.ExternalReference}";
        await LoadAsync(refreshTotals: false);
        return Page();
    }

    private async Task<int> LoadAsync(bool refreshTotals)
    {
        var view = await _getPayouts.HandleAsync(new GetAdminPayoutsQuery(refreshTotals));
        Pending = view.Pending;
        Recent = view.Recent;
        return view.Refreshed;
    }
}
