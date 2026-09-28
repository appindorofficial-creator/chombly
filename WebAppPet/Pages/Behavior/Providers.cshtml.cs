using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAppPet.Application.Behavior.BookBehaviorSession;
using WebAppPet.Application.Behavior.GetBehaviorProviders;
using WebAppPet.Application.Behavior.Shared;
using WebAppPet.Infrastructure.Identity;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Pages.Shared;

namespace WebAppPet.Pages.Behavior;

public class ProvidersModel : PageModel
{
    private readonly AuthService _auth;
    private readonly GetBehaviorProvidersHandler _getProviders;
    private readonly BookBehaviorSessionHandler _book;

    public ProvidersModel(AuthService auth, GetBehaviorProvidersHandler getProviders, BookBehaviorSessionHandler book)
    {
        _auth = auth;
        _getProviders = getProviders;
        _book = book;
    }

    [BindProperty(SupportsGet = true)]
    public int CaseId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int ProviderId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Slot { get; set; }

    /// <summary>Legacy When=hoy|manana; normalized into <see cref="Date"/>.</summary>
    [BindProperty(SupportsGet = true)]
    public string? When { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Date { get; set; }

    [BindProperty] public bool AcceptTerms { get; set; }
    [BindProperty] public bool AcceptScope { get; set; } = true;
    [BindProperty] public int? PaymentMethodId { get; set; }

    public BehaviorCase? Case { get; set; }
    public List<Pet> SelectedPets { get; set; } = new();
    public ServiceCatalogItem? CatalogItem { get; set; }
    public List<GroomerProfile> Providers { get; set; } = new();
    public List<PaymentMethod> Payments { get; set; } = new();
    public Dictionary<int, string> DistanceLabels { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public HashSet<string> OccupiedSlots { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> PastSlots { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> TimeSlots => BehaviorProviderOptions.TimeSlots;

    public bool HasDate => BookingDate.TryParseSelected(Date, out _);
    public bool HasSlot => BookingTime.IsSlotAvailable(Slot, TimeSlots, PastSlots, OccupiedSlots);
    public bool HasProvider => ProviderId > 0 && Providers.Any(p => p.Id == ProviderId);

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Behavior/Providers?caseId={CaseId}" });

        var options = await _getProviders.HandleAsync(new GetBehaviorProvidersQuery(userId, CaseId, ProviderId, Date, When, Slot));
        if (options.Redirect is BehaviorStep step)
            return RedirectTo(step);

        Show(options);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (_auth.CurrentUserId is not int userId) return RedirectToPage("/Account/Login");

        var result = await _book.HandleAsync(new BookBehaviorSessionCommand(
            userId, CaseId, ProviderId, Date, When, Slot, AcceptTerms, PaymentMethodId,
            HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString()));

        if (result.Options.Redirect is BehaviorStep step)
            return RedirectTo(step);
        if (result.Outcome == BookBehaviorSessionOutcome.Booked)
            return RedirectToPage("/Behavior/Summary", new { id = CaseId });

        Show(result.Options);
        AcceptScope = true;
        ErrorMessage = result.Outcome switch
        {
            BookBehaviorSessionOutcome.TermsNotAccepted => CatalogLocalizer.Loc(
                "Debes aceptar los términos para reservar.",
                "You must accept the terms to book."),
            BookBehaviorSessionOutcome.NoDate => CatalogLocalizer.Loc("Elige el día.", "Choose the day."),
            BookBehaviorSessionOutcome.NoSlot => CatalogLocalizer.Loc("Elige un horario.", "Choose a time."),
            BookBehaviorSessionOutcome.NoProvider => CatalogLocalizer.Loc("Selecciona un especialista.", "Select a specialist."),
            BookBehaviorSessionOutcome.PastTime => CatalogLocalizer.Loc(
                "No puedes elegir una fecha u hora en el pasado.",
                "You can't select a past date or time."),
            BookBehaviorSessionOutcome.SlotTaken => CatalogLocalizer.Loc(
                "Ese horario ya no está disponible. Elige otro día u hora.",
                "That time is no longer available. Choose another day or time."),
            BookBehaviorSessionOutcome.NoPaymentMethod => CatalogLocalizer.Loc(
                "Agrega un método de pago para reservar.",
                "Add a payment method to book."),
            BookBehaviorSessionOutcome.PaymentDeclined => result.PaymentError,
            _ => CatalogLocalizer.Loc("Faltan datos de la evaluación.", "Evaluation details are incomplete.")
        };
        return Page();
    }

    private void Show(BehaviorProviderOptions options)
    {
        Case = options.Case;
        SelectedPets = options.SelectedDogs;
        CatalogItem = options.CatalogItem;
        Providers = options.Providers;
        Payments = options.Payments;
        DistanceLabels = options.DistanceLabels;
        ProviderId = options.ProviderId;
        Date = options.Date;
        When = options.When;
        Slot = options.Slot;
        PastSlots = options.PastSlots;
        OccupiedSlots = options.OccupiedSlots;
    }

    private IActionResult RedirectTo(BehaviorStep step) => step switch
    {
        BehaviorStep.VetHome => RedirectToPage("/Vet/Index"),
        BehaviorStep.Intake => RedirectToPage("/Behavior/Intake", new { caseId = CaseId }),
        BehaviorStep.Summary => RedirectToPage("/Behavior/Summary", new { id = CaseId }),
        _ => RedirectToPage("/Care/Services")
    };

    public static string RoleLabel(BehaviorSpecialistRole? role) => role switch
    {
        BehaviorSpecialistRole.Trainer => CatalogLocalizer.Loc("Entrenador", "Trainer"),
        BehaviorSpecialistRole.VeterinaryBehaviorist => CatalogLocalizer.Loc("Veterinario conductista", "Veterinary behaviorist"),
        _ => CatalogLocalizer.Loc("Consultor de conducta", "Behavior consultant")
    };
}
