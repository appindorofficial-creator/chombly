using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;
using WebAppPet.Ui;

namespace WebAppPet.Pages.Reviews;

public class WriteModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly ReviewService _reviews;

    public WriteModel(AppDbContext db, AuthService auth, ReviewService reviews)
    {
        _db = db;
        _auth = auth;
        _reviews = reviews;
    }

    [BindProperty(SupportsGet = true)]
    public int GroomerId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? AppointmentId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty]
    [Range(1, 5)]
    public int Rating { get; set; } = 5;

    [BindProperty]
    [MaxLength(600)]
    public string? Comment { get; set; }

    public GroomerProfile? Groomer { get; set; }
    public bool CanSubmit { get; private set; }
    public string? ErrorMessage { get; set; }
    public string BackHref { get; private set; } = "/Appointments";

    public async Task<IActionResult> OnGetAsync()
    {
        if (_auth.CurrentUserId is not int userId)
        {
            return RedirectToPage("/Account/Login", new
            {
                returnUrl = $"/Reviews/Write?groomerId={GroomerId}"
                    + (AppointmentId is int a ? $"&appointmentId={a}" : "")
                    + (!string.IsNullOrWhiteSpace(ReturnUrl) ? $"&returnUrl={Uri.EscapeDataString(ReturnUrl)}" : "")
            });
        }

        await LoadAsync(userId);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (_auth.CurrentUserId is not int userId)
            return RedirectToPage("/Account/Login");

        await LoadAsync(userId);
        if (!CanSubmit || Groomer == null)
            return Page();

        var result = await _reviews.SubmitAsync(userId, GroomerId, Rating, Comment, AppointmentId);
        if (!result.Ok)
        {
            ErrorMessage = result.Error;
            CanSubmit = await _reviews.CanReviewAsync(userId, GroomerId, AppointmentId);
            return Page();
        }

        AppFlash.Toast(this, CatalogLocalizer.Loc("¡Gracias por tu reseña!", "Thanks for your review!"));
        if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
            return LocalRedirect(ReturnUrl);

        return RedirectToPage("/Groomers/Details", new { id = GroomerId });
    }

    private async Task LoadAsync(int userId)
    {
        Groomer = await _db.Groomers.AsNoTracking()
            .Include(g => g.Category)
            .FirstOrDefaultAsync(g => g.Id == GroomerId && g.IsActive);

        BackHref = ResolveBack();

        if (Groomer == null)
        {
            ErrorMessage = CatalogLocalizer.Loc("Negocio no encontrado.", "Business not found.");
            CanSubmit = false;
            return;
        }

        CanSubmit = await _reviews.CanReviewAsync(userId, GroomerId, AppointmentId);
        if (!CanSubmit)
        {
            if (await _reviews.HasReviewedAsync(userId, GroomerId))
            {
                ErrorMessage = CatalogLocalizer.Loc(
                    "Ya valoraste este negocio.",
                    "You already reviewed this business.");
            }
            else
            {
                ErrorMessage = CatalogLocalizer.Loc(
                    "Solo puedes valorar después de una visita confirmada o completada.",
                    "You can only review after a confirmed or completed visit.");
            }
        }
    }

    private string ResolveBack()
    {
        if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
            return ReturnUrl!;
        if (AppointmentId is int aid)
            return Url.Page("/Appointments/Details", new { id = aid }) ?? "/Appointments";
        return Url.Page("/Groomers/Details", new { id = GroomerId }) ?? "/Groomers";
    }
}
