using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Pages.Professional.Onboarding;

public class LocalModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly ProfessionalOnboardingService _onboarding;
    private readonly IWebHostEnvironment _env;

    public LocalModel(
        AppDbContext db,
        AuthService auth,
        ProfessionalOnboardingService onboarding,
        IWebHostEnvironment env)
    {
        _db = db;
        _auth = auth;
        _onboarding = onboarding;
        _env = env;
    }

    [BindProperty(SupportsGet = true)]
    public string? Track { get; set; }

    public bool IsBehavior => string.Equals(Track, "behavior", StringComparison.OrdinalIgnoreCase);

    [BindProperty] public string LegalName { get; set; } = "";
    [BindProperty] public string ClinicOrPracticeName { get; set; } = "";
    [BindProperty] public string LicenseNumber { get; set; } = "";
    [BindProperty] public string LicenseJurisdiction { get; set; } = "NC";
    [BindProperty] public DateTime? LicenseExpiry { get; set; }
    [BindProperty] public string Languages { get; set; } = "en,es";
    [BindProperty] public string Specialties { get; set; } = "";
    [BindProperty] public bool HasPhysicalClinic { get; set; } = true;
    [BindProperty] public bool VcprCapable { get; set; } = true;
    [BindProperty] public string? DocumentsNote { get; set; }
    [BindProperty] public IFormFile? DocumentUpload { get; set; }

    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await GateAsync()) return RedirectToPage("/Account/RegisterBusiness");
        var latest = await _onboarding.GetLatestAsync(_auth.CurrentUserId!.Value);
        if (latest != null && (latest.Track == ProfessionalOnboardingTrack.Local ||
                               latest.Track == ProfessionalOnboardingTrack.Behavior ||
                               IsBehavior))
        {
            LegalName = latest.LegalName;
            ClinicOrPracticeName = latest.ClinicOrPracticeName;
            LicenseNumber = latest.LicenseNumber;
            LicenseJurisdiction = latest.LicenseJurisdiction;
            LicenseExpiry = latest.LicenseExpiry;
            Languages = latest.Languages;
            Specialties = latest.Specialties;
            HasPhysicalClinic = latest.HasPhysicalClinic;
            VcprCapable = latest.VcprCapable;
            DocumentsNote = latest.DocumentsNote;
            if (latest.Track == ProfessionalOnboardingTrack.Behavior)
                Track = "behavior";
        }
        return Page();
    }

    public Task<IActionResult> OnPostDraftAsync() => SaveInternalAsync(submit: false);

    public Task<IActionResult> OnPostSubmitAsync() => SaveInternalAsync(submit: true);

    private async Task<IActionResult> SaveInternalAsync(bool submit)
    {
        if (!await GateAsync()) return RedirectToPage("/Account/RegisterBusiness");
        try
        {
            var track = IsBehavior ? ProfessionalOnboardingTrack.Behavior : ProfessionalOnboardingTrack.Local;
            var uploadPath = await SaveUploadAsync();

            var app = await _onboarding.StartOrUpdateDraftAsync(_auth.CurrentUserId!.Value, track, a =>
            {
                a.LegalName = LegalName.Trim();
                a.ClinicOrPracticeName = ClinicOrPracticeName.Trim();
                a.LicenseNumber = LicenseNumber.Trim();
                a.LicenseJurisdiction = LicenseJurisdiction.Trim().ToUpperInvariant();
                a.LicenseExpiry = LicenseExpiry;
                a.Languages = Languages.Trim();
                a.Specialties = Specialties.Trim();
                a.HasPhysicalClinic = HasPhysicalClinic;
                a.VcprCapable = VcprCapable;
                a.DocumentsNote = DocumentsNote;
                a.AcceptsInternationalClients = false;
                a.BreedExpertiseCsv = "";
                if (uploadPath != null) a.UploadPath = uploadPath;
            });

            if (submit)
                await _onboarding.SubmitAsync(_auth.CurrentUserId.Value, app.Id);

            return RedirectToPage("/Professional/Onboarding/Status");
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            return Page();
        }
    }

    private async Task<bool> GateAsync()
    {
        if (_auth.CurrentUserId is null) return false;
        if (!_auth.IsGroomer && !_auth.IsAdmin) return false;
        return await _db.Groomers.AnyAsync(g => g.UserId == _auth.CurrentUserId);
    }

    private async Task<string?> SaveUploadAsync()
    {
        if (DocumentUpload is null || DocumentUpload.Length == 0) return null;
        var dir = Path.Combine(_env.WebRootPath, "uploads", "professional");
        Directory.CreateDirectory(dir);
        var ext = Path.GetExtension(DocumentUpload.FileName);
        if (ext.Length > 10) ext = ".bin";
        var name = $"{_auth.CurrentUserId}_{Guid.NewGuid():N}{ext}";
        var full = Path.Combine(dir, name);
        await using var fs = System.IO.File.Create(full);
        await DocumentUpload.CopyToAsync(fs);
        return "/uploads/professional/" + name;
    }
}
