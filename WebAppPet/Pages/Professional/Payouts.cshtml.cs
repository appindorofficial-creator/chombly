using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using WebAppPet.Application.Payments.GeneratePayout;
using WebAppPet.Application.Payments.GetProviderPayouts;
using WebAppPet.Application.Payments.Shared;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Professional;

public class PayoutsModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly GetProviderPayoutsHandler _getPayouts;
    private readonly GeneratePayoutHandler _generatePayout;
    private readonly IStringLocalizer<SharedResource> _L;

    public PayoutsModel(
        AppDbContext db,
        AuthService auth,
        GetProviderPayoutsHandler getPayouts,
        GeneratePayoutHandler generatePayout,
        IStringLocalizer<SharedResource> L)
    {
        _db = db;
        _auth = auth;
        _getPayouts = getPayouts;
        _generatePayout = generatePayout;
        _L = L;
    }

    public List<ProviderCompensationRule> Rules { get; set; } = new();
    public List<ProviderPayout> History { get; set; } = new();
    public List<ProviderPayoutService.ProviderPaymentRow> RecentPayments { get; set; } = new();
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
            var result = await _generatePayout.HandleAsync(new GeneratePayoutCommand(
                _auth.CurrentUserId!.Value,
                PeriodStart.ToUniversalTime(),
                PeriodEnd.ToUniversalTime()));
            if (result.InvalidPeriod)
            {
                Error = _L["Payout_PeriodEndError"].Value;
            }
            else
            {
                Message = string.Format(
                    _L["Payout_SummaryCreated"].Value,
                    AppMoney.Format(result.Payout!.NetAmountUsd),
                    result.Payout.ConsultationCount);
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
        var view = await _getPayouts.HandleAsync(new GetProviderPayoutsQuery(_auth.CurrentUserId!.Value));
        Rules = view.Rules;
        History = view.History;
        RecentPayments = view.RecentPayments;
    }

    private async Task<bool> EnsureProviderAsync()
    {
        if (_auth.CurrentUserId is null) return false;
        if (!_auth.IsGroomer && !_auth.IsAdmin) return false;
        return await _db.Groomers.AnyAsync(g => g.UserId == _auth.CurrentUserId);
    }
}
