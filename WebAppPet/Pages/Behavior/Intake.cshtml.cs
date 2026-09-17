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
    private readonly VetAuditService _audit;

    public IntakeModel(
        AppDbContext db,
        AuthService auth,
        BehaviorFlowService flow,
        VetAuditService audit)
    {
        _db = db;
        _auth = auth;
        _flow = flow;
        _audit = audit;
    }

    [BindProperty(SupportsGet = true)]
    public int CaseId { get; set; }

    [BindProperty] public List<int> SelectedPetIds { get; set; } = new();
    [BindProperty] public string? ProblemType { get; set; }
    [BindProperty] public string? Frequency { get; set; }
    [BindProperty] public string? ContextNotes { get; set; }
    [BindProperty] public bool HasClinicalConcern { get; set; }

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

        Pets = await LoadDogPetsAsync();

        // Fresh form every visit — do not preselect pets, problem, or frequency.
        SelectedPetIds = new();
        ProblemType = null;
        Frequency = null;
        ContextNotes = null;
        HasClinicalConcern = false;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (_auth.CurrentUserId is null) return RedirectToPage("/Account/Login");
        Case = await _flow.GetOwnedAsync(CaseId);
        if (Case is null) return RedirectToPage("/Care/Services");

        Pets = await LoadDogPetsAsync();

        var dogIds = Pets.Select(p => p.Id).ToHashSet();
        SelectedPetIds = SelectedPetIds.Where(id => dogIds.Contains(id)).Distinct().ToList();

        if (SelectedPetIds.Count == 0)
        {
            ErrorMessage = CatalogLocalizer.Loc(
                "Activa al menos un perro.",
                "Turn on at least one dog.");
            return Page();
        }

        if (string.IsNullOrWhiteSpace(ProblemType))
        {
            ErrorMessage = CatalogLocalizer.Loc("Indica el problema principal.", "Choose the main problem.");
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Frequency))
        {
            ErrorMessage = CatalogLocalizer.Loc("Indica la frecuencia.", "Choose the frequency.");
            return Page();
        }

        BehaviorFlowService.SetSelectedPetIds(Case, SelectedPetIds);
        Case.ProblemType = ProblemType.Trim();
        Case.Frequency = Frequency.Trim();
        Case.ContextNotes = ContextNotes?.Trim();
        Case.ClinicalRedFlag = HasClinicalConcern;
        Case.VideoUrl = null;

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

    /// <summary>Comportamiento canino: solo perros (no gatos u otras especies).</summary>
    private async Task<List<Models.Pet>> LoadDogPetsAsync()
    {
        return await _db.Pets.AsNoTracking()
            .Where(p => p.OwnerId == _auth.CurrentUserId
                        && p.Species == PetSpecies.Dog)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }
}
