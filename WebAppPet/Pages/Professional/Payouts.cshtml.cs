using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Professional;

public class PayoutsModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly ProviderPayoutService _payouts;

    public PayoutsModel(AppDbContext db, AuthService auth, ProviderPayoutService payouts)
    {
        _db = db;
        _auth = auth;
        _payouts = payouts;
    }

    public List<ProviderCompensationRule> Rules { get; set; } = new();
    public List<ProviderPayout> History { get; set; } = new();
    public string? Message { get; set; }
    public string? Error { get; set; }

    [BindProperty]
    public DateTime PeriodStart { get; set; } = DateTime.UtcNow.Date.AddDays(-30);

    [BindProperty]
    public DateTime PeriodEnd { get; set; } = DateTime.UtcNow.Date.AddDays(1);

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await EnsureProviderAsync()) return RedirectToPage("/Account/Login");
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostGenerateAsync()
    {
        if (!await EnsureProviderAsync()) return RedirectToPage("/Account/Login");
        try
        {
            if (PeriodEnd <= PeriodStart)
            {
                Error = "Period end must be after start.";
            }
            else
            {
                var payout = await _payouts.CreatePendingPayoutAsync(
                    _auth.CurrentUserId!.Value,
                    PeriodStart.ToUniversalTime(),
                    PeriodEnd.ToUniversalTime(),
                    _auth.CurrentUserId);
                Message = $"Period summary created · net ${payout.NetAmountUsd:0.00} ({payout.ConsultationCount} items).";
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }

        await LoadAsync();
        return Page();
    }

    private async Task LoadAsync()
    {
        var uid = _auth.CurrentUserId!.Value;
        Rules = await _payouts.ListRulesForProviderAsync(uid);
        History = await _payouts.ListForProviderAsync(uid);
    }

    private async Task<bool> EnsureProviderAsync()
    {
        if (_auth.CurrentUserId is null) return false;
        if (!_auth.IsGroomer && !_auth.IsAdmin) return false;
        return await _db.Groomers.AnyAsync(g => g.UserId == _auth.CurrentUserId);
    }
}
