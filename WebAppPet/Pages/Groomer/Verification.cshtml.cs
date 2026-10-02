using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Businesses.MarkVerificationItem;
using WebAppPet.Infrastructure.Identity;
using WebAppPet.Infrastructure.Persistence;
using WebAppPet.Localization;

namespace WebAppPet.Pages.Groomer;

public class VerificationModel : GroomerPageModel
{
    private readonly MarkVerificationItemHandler _markItem;

    public VerificationModel(AppDbContext db, AuthService auth, MarkVerificationItemHandler markItem) : base(db, auth)
    {
        _markItem = markItem;
    }

    public int DoneCount { get; set; }
    public int TotalCount { get; } = 6;
    public string? Message { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (await LoadGroomerAsync() is IActionResult r) return r;
        await EnsureServicesLoadedAsync();
        CountDone();
        return Page();
    }

    public async Task<IActionResult> OnPostMarkAsync(string item)
    {
        if (await LoadGroomerAsync() is IActionResult r) return r;
        await _markItem.HandleAsync(new MarkVerificationItemCommand(Profile!.Id, item));
        Message = CatalogLocalizer.Loc(
            "Marcado como completado. El equipo de Chombly puede revisarlo.",
            "Marked as done. The Chombly team can review it.");
        await EnsureServicesLoadedAsync();
        CountDone();
        return Page();
    }

    private async Task EnsureServicesLoadedAsync()
    {
        if (Profile == null) return;
        await Db.Entry(Profile).Collection(p => p.Services).LoadAsync();
    }

    private void CountDone()
    {
        var g = Profile!;
        DoneCount = 0;
        if (!string.IsNullOrWhiteSpace(g.BusinessName) && !string.IsNullOrWhiteSpace(g.City)) DoneCount++;
        if (g.VerifiedIdentity) DoneCount++;
        if (g.VerifiedLicense) DoneCount++;
        if (g.VerifiedInsurance) DoneCount++;
        if (g.VerifiedBank) DoneCount++;
        if (g.Services.Any()) DoneCount++;
    }
}
