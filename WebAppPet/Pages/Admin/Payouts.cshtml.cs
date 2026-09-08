using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Admin;

public class PayoutsModel : PageModel
{
    private readonly AuthService _auth;
    private readonly ProviderPayoutService _payouts;

    public PayoutsModel(AuthService auth, ProviderPayoutService payouts)
    {
        _auth = auth;
        _payouts = payouts;
    }

    public List<ProviderPayout> Pending { get; set; } = new();
    public List<ProviderPayout> Recent { get; set; } = new();
    public string? Message { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!_auth.IsAdmin) return RedirectToPage("/Account/Login");
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostMarkPaidAsync(int id)
    {
        if (!_auth.IsAdmin) return RedirectToPage("/Account/Login");
        var payout = await _payouts.MarkPaidAsync(id, _auth.CurrentUserId);
        Message = payout is null
            ? "Payout not found."
            : $"Marked paid · {payout.ExternalReference}";
        await LoadAsync();
        return Page();
    }

    private async Task LoadAsync()
    {
        Pending = await _payouts.ListPendingAsync();
        Recent = await _payouts.ListAllAsync(40);
    }
}
