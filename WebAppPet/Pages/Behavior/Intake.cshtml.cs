using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.Behavior.GetBehaviorIntake;
using WebAppPet.Application.Behavior.SubmitBehaviorIntake;
using WebAppPet.Domain;
using WebAppPet.Infrastructure.Identity;
using WebAppPet.Localization;

namespace WebAppPet.Pages.Behavior;

public class IntakeModel : PageModel
{
    private readonly AuthService _auth;
    private readonly GetBehaviorIntakeHandler _getIntake;
    private readonly SubmitBehaviorIntakeHandler _submit;

    public IntakeModel(AuthService auth, GetBehaviorIntakeHandler getIntake, SubmitBehaviorIntakeHandler submit)
    {
        _auth = auth;
        _getIntake = getIntake;
        _submit = submit;
    }

    [BindProperty(SupportsGet = true)]
    public int CaseId { get; set; }

    [BindProperty] public List<int> SelectedPetIds { get; set; } = new();
    [BindProperty] public string? ProblemType { get; set; }
    [BindProperty] public string? Frequency { get; set; }
    [BindProperty] public string? ContextNotes { get; set; }
    [BindProperty] public bool HasClinicalConcern { get; set; }

    public BehaviorCase? Case { get; set; }
    public List<Pet> Pets { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public static readonly string[] Problems =
    {
        "Ladridos excesivos", "Ansiedad por separación", "Tirones en paseo",
        "Agresión a perros", "Agresión a personas", "Miedos / ruidos", "Otro"
    };

    public static readonly string[] Frequencies = { "Diario", "Varias veces/semana", "Ocasional", "Primera vez" };

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Behavior/Intake?caseId={CaseId}" });

        var form = await _getIntake.HandleAsync(new GetBehaviorIntakeQuery(userId, CaseId));
        if (form is null) return RedirectToPage("/Care/Services");
        Show(form);

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
        if (_auth.CurrentUserId is not int userId) return RedirectToPage("/Account/Login");

        var result = await _submit.HandleAsync(new SubmitBehaviorIntakeCommand(
            userId, CaseId, SelectedPetIds, ProblemType, Frequency, ContextNotes, HasClinicalConcern));

        switch (result.Outcome)
        {
            case SubmitBehaviorIntakeOutcome.NotFound:
                return RedirectToPage("/Care/Services");
            case SubmitBehaviorIntakeOutcome.ReferredToVet:
                return RedirectToPage("/Vet/Index");
            case SubmitBehaviorIntakeOutcome.Completed:
                return RedirectToPage("/Behavior/Providers", new { caseId = CaseId });
        }

        Show(result.Form!);
        SelectedPetIds = result.SelectedPetIds;
        ErrorMessage = result.Outcome switch
        {
            SubmitBehaviorIntakeOutcome.NoDogSelected => CatalogLocalizer.Loc("Activa al menos un perro.", "Turn on at least one dog."),
            SubmitBehaviorIntakeOutcome.NoProblem => CatalogLocalizer.Loc("Indica el problema principal.", "Choose the main problem."),
            _ => CatalogLocalizer.Loc("Indica la frecuencia.", "Choose the frequency.")
        };
        return Page();
    }

    private void Show(BehaviorIntakeForm form)
    {
        Case = form.Case;
        Pets = form.Dogs;
    }
}
