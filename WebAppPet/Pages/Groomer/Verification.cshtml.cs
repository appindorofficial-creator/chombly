using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Groomer;

public class VerificationModel : GroomerPageModel
{
    public VerificationModel(AppDbContext db, AuthService auth) : base(db, auth) { }

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
        var g = Profile!;
        switch (item)
        {
            case "identity": g.VerifiedIdentity = true; break;
            case "license": g.VerifiedLicense = true; break;
            case "insurance": g.VerifiedInsurance = true; break;
            case "bank": g.VerifiedBank = true; break;
        }
        await Db.SaveChangesAsync();
        Message = "Marcado como completado. El equipo de Chombly puede revisarlo.";
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
