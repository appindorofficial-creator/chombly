using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Behavior;

public class IntakeModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly BehaviorFlowService _flow;
    private readonly IWebHostEnvironment _env;
    private readonly VetAuditService _audit;

    public IntakeModel(
        AppDbContext db,
        AuthService auth,
        BehaviorFlowService flow,
        IWebHostEnvironment env,
        VetAuditService audit)
    {
        _db = db;
        _auth = auth;
        _flow = flow;
        _env = env;
        _audit = audit;
    }

    [BindProperty(SupportsGet = true)]
    public int CaseId { get; set; }

    [BindProperty] public int PetId { get; set; }
    [BindProperty] public string? ProblemType { get; set; }
    [BindProperty] public string? Frequency { get; set; }
    [BindProperty] public string? ContextNotes { get; set; }
    [BindProperty] public IFormFile? Video { get; set; }
    [BindProperty] public bool SuddenAggression { get; set; }
    [BindProperty] public bool SeizureLike { get; set; }
    [BindProperty] public bool PainSuspected { get; set; }
    [BindProperty] public bool SelfHarm { get; set; }

    public BehaviorCase? Case { get; set; }
    public List<Models.Pet> Pets { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public static readonly string[] Problems =
    {
        "Ladridos excesivos", "Ansiedad por separación", "Tirones en paseo",
        "Agresión a perros", "Agresión a personas", "Miedos / ruidos", "Otro"
    };

    public static readonly string[] Frequencies = { "Diario", "Varias veces/semana", "Ocasional", "Primera vez" };

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is null)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Behavior/Intake?caseId={CaseId}" });

        Case = await _flow.GetOwnedAsync(CaseId);
        if (Case is null) return RedirectToPage("/Care/Services");

        Pets = await _db.Pets.Where(p => p.OwnerId == _auth.CurrentUserId && p.Species == PetSpecies.Dog)
            .OrderBy(p => p.Name).ToListAsync();
        if (Pets.Count == 0)
            Pets = await _db.Pets.Where(p => p.OwnerId == _auth.CurrentUserId).OrderBy(p => p.Name).ToListAsync();

        PetId = Case.PetId ?? Pets.FirstOrDefault()?.Id ?? 0;
        ProblemType = Case.ProblemType;
        Frequency = Case.Frequency;
        ContextNotes = Case.ContextNotes;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (_auth.CurrentUserId is null) return RedirectToPage("/Account/Login");
        Case = await _flow.GetOwnedAsync(CaseId);
        if (Case is null) return RedirectToPage("/Care/Services");

        Pets = await _db.Pets.Where(p => p.OwnerId == _auth.CurrentUserId).OrderBy(p => p.Name).ToListAsync();

        if (PetId <= 0 || !Pets.Any(p => p.Id == PetId))
        {
            ErrorMessage = CatalogLocalizer.Loc("Selecciona una mascota.", "Select a pet.");
            return Page();
        }

        if (string.IsNullOrWhiteSpace(ProblemType))
        {
            ErrorMessage = CatalogLocalizer.Loc("Indica el problema principal.", "Choose the main problem.");
            return Page();
        }

        var mediaErr = VetMediaStorage.Validate(Video);
        if (mediaErr != null)
        {
            ErrorMessage = CatalogLocalizer.Loc("Video no válido (≤25MB).", "Invalid video (≤25MB).");
            return Page();
        }

        Case.PetId = PetId;
        Case.ProblemType = ProblemType.Trim();
        Case.Frequency = Frequency;
        Case.ContextNotes = ContextNotes?.Trim();
        Case.ClinicalRedFlag = BehaviorFlowService.HasClinicalRedFlag(SuddenAggression, SeizureLike, PainSuspected, SelfHarm);
        if (Video is { Length: > 0 })
            Case.VideoUrl = await VetMediaStorage.SaveAsync(Video, _auth.CurrentUserId.Value, _env);

        if (Case.ClinicalRedFlag)
        {
            Case.Status = BehaviorCaseStatus.ReferredToVet;
            await _flow.TouchAsync(Case);
            await _audit.LogAsync("behavior_referred_vet", _auth.CurrentUserId, "BehaviorCase", Case.Id);
            return RedirectToPage("/Vet/Index");
        }

        Case.Status = BehaviorCaseStatus.IntakeComplete;
        await _flow.TouchAsync(Case);
        return RedirectToPage("/Behavior/Providers", new { caseId = CaseId });
    }
}
