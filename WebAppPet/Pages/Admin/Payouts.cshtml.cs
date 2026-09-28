using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using WebAppPet.Application.Payments.GetAdminPayouts;
using WebAppPet.Application.Payments.MarkPayoutPaid;
using WebAppPet.Domain;
using WebAppPet.Domain.Markets;
using WebAppPet.Infrastructure.Identity;
using WebAppPet.Localization;

namespace WebAppPet.Pages.Admin;

public class PayoutsModel : PageModel
{
    private readonly AuthService _auth;
    private readonly GetAdminPayoutsHandler _getPayouts;
    private readonly MarkPayoutPaidHandler _markPaid;
    private readonly IStringLocalizer<SharedResource> _L;

    public PayoutsModel(
        AuthService auth,
        GetAdminPayoutsHandler getPayouts,
        MarkPayoutPaidHandler markPaid,
        IStringLocalizer<SharedResource> L)
    {
        _auth = auth;
        _getPayouts = getPayouts;
        _markPaid = markPaid;
        _L = L;
    }

    public List<ProviderPayout> Pending { get; set; } = new();
    public List<ProviderPayout> Recent { get; set; } = new();
    public IReadOnlyDictionary<int, string> Currencies { get; set; } = new Dictionary<int, string>();
    public string? Message { get; set; }

    public string CurrencyOf(ProviderPayout payout) =>
        Currencies.GetValueOrDefault(payout.ProviderUserId) ?? AppMoney.Code(payout.ProviderUser?.CountryCode);

    public async Task<IActionResult> OnGetAsync()
    {
        if (!_auth.IsAdmin) return RedirectToPage("/Account/Login");
        var refreshed = await LoadAsync(refreshTotals: true);
        if (refreshed > 0)
            Message = string.Format(_L["AdminPayout_TotalsRefreshed"].Value, refreshed);
        return Page();
    }

    public async Task<IActionResult> OnPostMarkPaidAsync(int id)
    {
        if (!_auth.IsAdmin) return RedirectToPage("/Account/Login");
        var payout = await _markPaid.HandleAsync(new MarkPayoutPaidCommand(id, _auth.CurrentUserId));
        await LoadAsync(refreshTotals: false);
        Message = payout is null
            ? _L["AdminPayout_NotFound"].Value
            : string.Format(
                _L["AdminPayout_MarkedPaid"].Value,
                AppMoney.FormatCurrency(payout.NetAmountUsd, CurrencyOf(payout)),
                payout.ExternalReference);
        return Page();
    }

    private async Task<int> LoadAsync(bool refreshTotals)
    {
        var view = await _getPayouts.HandleAsync(new GetAdminPayoutsQuery(refreshTotals));
        Pending = view.Pending;
        Recent = view.Recent;
        Currencies = view.Currencies;
        return view.Refreshed;
    }
}
