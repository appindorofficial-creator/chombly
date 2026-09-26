using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.Consultations.GetIntlMatches;
using WebAppPet.Application.Consultations.SelectIntlVet;
using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Services;
using WebAppPet.Ui;

namespace WebAppPet.Pages.Vet.International;

public class MatchesModel : PageModel
{
    private readonly AuthService _auth;
    private readonly GetIntlMatchesHandler _getMatches;
    private readonly SelectIntlVetHandler _selectVet;

    public MatchesModel(AuthService auth, GetIntlMatchesHandler getMatches, SelectIntlVetHandler selectVet)
    {
        _auth = auth;
        _getMatches = getMatches;
        _selectVet = selectVet;
    }

    [BindProperty(SupportsGet = true)]
    public int ConsultationId { get; set; }

    /// <summary>Language filter code (es, en, …). Empty = all.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Lang { get; set; }

    public List<IntlMatch> Matches { get; set; } = new();
    public List<LanguageChip> LanguageChips { get; set; } = new();
    public decimal ConsultPrice { get; set; } = 30m;
    public bool HasCareBenefit { get; set; }
    public int CareRemaining { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Vet/International/Matches?consultationId={ConsultationId}" });

        var result = await _getMatches.HandleAsync(new GetIntlMatchesQuery(userId, ConsultationId, Lang));
        if (result.Redirect is ConsultationStep step)
            return this.RedirectToStep(step, ConsultationId, ConsultationPath.Intl);

        Matches = result.Matches;
        LanguageChips = result.LanguageChips;
        ConsultPrice = result.ConsultPrice;
        HasCareBenefit = result.HasCareBenefit;
        CareRemaining = result.CareRemaining;
        Lang = result.ActiveLang;
        return Page();
    }

    public async Task<IActionResult> OnPostSelectAsync(int providerId)
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Vet/Index");

        return await _selectVet.HandleAsync(new SelectIntlVetCommand(userId, ConsultationId, providerId)) switch
        {
            SelectIntlVetOutcome.NotFound => RedirectToPage("/Vet/Index"),
            SelectIntlVetOutcome.ProviderUnavailable => RedirectToPage(new { consultationId = ConsultationId, lang = Lang }),
            _ => this.RedirectToStep(ConsultationStep.IntlSchedule, ConsultationId)
        };
    }
}
