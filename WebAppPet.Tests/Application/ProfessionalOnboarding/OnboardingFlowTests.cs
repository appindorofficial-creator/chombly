using WebAppPet.Application.Common;
using WebAppPet.Application.Payments.Shared;
using WebAppPet.Application.ProfessionalOnboarding.ApproveOnboarding;
using WebAppPet.Application.ProfessionalOnboarding.GetOnboardingDraft;
using WebAppPet.Application.ProfessionalOnboarding.GetOnboardingTracks;
using WebAppPet.Application.ProfessionalOnboarding.GetPendingOnboardings;
using WebAppPet.Application.ProfessionalOnboarding.RejectOnboarding;
using WebAppPet.Application.ProfessionalOnboarding.SaveOnboardingApplication;
using WebAppPet.Domain;
using WebAppPet.Domain.Markets;
using WebAppPet.Infrastructure.Persistence;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.ProfessionalOnboarding;

public class OnboardingFlowTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly AppDbContext _db;

    public OnboardingFlowTests()
    {
        _db = _database.CreateContext();
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private SaveOnboardingApplicationHandler Save => new(_db, new CountryCatalogService(_db), new VetAuditService(_db));
    private ApproveOnboardingHandler Approve => new(_db, new ProviderPayoutService(_db, new VetAuditService(_db)), new VetAuditService(_db));
    private RejectOnboardingHandler Reject => new(_db, new VetAuditService(_db));

    private static OnboardingDetails Details(
        string? legalName = "Dra. Ana Ruiz",
        string? jurisdiction = "nc",
        string? breeds = null,
        string? languages = "es,en") =>
        new(legalName, "Clínica Ana", "LIC-1", jurisdiction, new DateTime(2030, 1, 1),
            languages, "Dermatología", breeds, AcceptsInternationalClients: true,
            HasPhysicalClinic: true, VcprCapable: true, DocumentsNote: "  Licencia adjunta  ");

    private SaveOnboardingApplicationCommand Command(
        AppUser user,
        ProfessionalOnboardingTrack track = ProfessionalOnboardingTrack.Local,
        OnboardingDetails? details = null,
        bool submit = true,
        OnboardingDocument? document = null,
        string? countrySearch = null) =>
        new(user.Id, track, details ?? Details(), countrySearch, document, submit);

    private (AppUser Owner, GroomerProfile Business) AddBusiness(params string[] categorySlugs)
    {
        var business = TestData.AddBusiness(_db);
        var ids = categorySlugs.Select(slug =>
        {
            var category = _db.Categories.FirstOrDefault(c => c.Slug == slug);
            if (category is null)
            {
                category = new ServiceCategory { Slug = slug, Name = slug };
                _db.Categories.Add(category);
                _db.SaveChanges();
            }
            return category.Id;
        }).ToList();
        if (ids.Count > 0)
        {
            business.CategoryId = ids[0];
            business.ExtraCategoryIds = string.Join(",", ids.Skip(1));
        }
        _db.SaveChanges();
        return (_db.Users.Single(u => u.Id == business.UserId), business);
    }

    private ProfessionalOnboardingApplication AddApplication(AppUser owner, ProfessionalOnboardingTrack track,
        ProfessionalOnboardingStatus status = ProfessionalOnboardingStatus.Submitted,
        string jurisdiction = "NC", string breeds = "", string languages = "es")
    {
        var application = new ProfessionalOnboardingApplication
        {
            UserId = owner.Id,
            Track = track,
            Status = status,
            LegalName = "Dra. Ana Ruiz",
            ClinicOrPracticeName = "Clínica Ana",
            LicenseNumber = "LIC-1",
            LicenseJurisdiction = jurisdiction,
            BreedExpertiseCsv = breeds,
            Languages = languages,
            SubmittedUtc = DateTime.UtcNow
        };
        _db.ProfessionalOnboardingApplications.Add(application);
        _db.SaveChanges();
        return application;
    }

    private ProfessionalOnboardingApplication Reload(int id)
    {
        using var db = _database.CreateContext();
        return db.ProfessionalOnboardingApplications.Single(a => a.Id == id);
    }

    [Fact]
    public async Task A_colombian_vet_sees_the_colombia_track_emphasized()
    {
        var (owner, business) = AddBusiness("vet", "trainers");
        business.City = "Neiva, Huila";
        _db.SaveChanges();

        var view = await new GetOnboardingTracksHandler(_db).HandleAsync(new GetOnboardingTracksQuery(owner.Id));

        Assert.Null(view.Redirect);
        Assert.Equal(BusinessMarket.Colombia, view.Market);
        Assert.True(view.ProfessionalTracksApply);
        Assert.Collection(view.Tracks,
            vet => Assert.True(vet is { Page: "/Professional/Onboarding/International", Emphasized: true }),
            behavior => Assert.True(behavior is { Track: "behavior", Emphasized: false }));
    }

    [Fact]
    public async Task A_groomer_has_no_professional_tracks()
    {
        var (owner, _) = AddBusiness("grooming");

        var view = await new GetOnboardingTracksHandler(_db).HandleAsync(new GetOnboardingTracksQuery(owner.Id));

        Assert.False(view.ProfessionalTracksApply);
        Assert.Empty(view.Tracks);
    }

    [Theory]
    [InlineData(ProfessionalOnboardingStatus.Submitted, OnboardingTracksRedirect.Status)]
    [InlineData(ProfessionalOnboardingStatus.Approved, OnboardingTracksRedirect.Status)]
    public async Task An_application_in_review_or_approved_goes_to_the_status(ProfessionalOnboardingStatus status, OnboardingTracksRedirect expected)
    {
        var (owner, _) = AddBusiness("vet");
        AddApplication(owner, ProfessionalOnboardingTrack.Local, status);

        var view = await new GetOnboardingTracksHandler(_db).HandleAsync(new GetOnboardingTracksQuery(owner.Id));

        Assert.Equal(expected, view.Redirect);
    }

    [Fact]
    public async Task A_user_without_a_business_is_sent_to_register_one()
    {
        var user = TestData.AddUser(_db);

        Assert.Equal(OnboardingTracksRedirect.RegisterBusiness,
            (await new GetOnboardingTracksHandler(_db).HandleAsync(new GetOnboardingTracksQuery(user.Id))).Redirect);
        Assert.Equal(SaveOnboardingApplicationOutcome.NoBusinessProfile, (await Save.HandleAsync(Command(user))).Outcome);
        Assert.Empty(_db.ProfessionalOnboardingApplications);
    }

    [Fact]
    public async Task The_draft_is_only_offered_while_it_can_be_edited()
    {
        var (owner, _) = AddBusiness("vet");
        var handler = new GetOnboardingDraftHandler(_db);
        var application = AddApplication(owner, ProfessionalOnboardingTrack.Local, ProfessionalOnboardingStatus.Rejected);

        Assert.Equal(application.Id, (await handler.HandleAsync(new GetOnboardingDraftQuery(owner.Id))).Draft?.Id);

        application.Status = ProfessionalOnboardingStatus.Submitted;
        _db.SaveChanges();
        Assert.Null((await handler.HandleAsync(new GetOnboardingDraftQuery(owner.Id))).Draft);
    }

    [Fact]
    public async Task Submitting_a_local_application_saves_it_for_review()
    {
        var (owner, _) = AddBusiness("vet");
        var document = new OnboardingDocument(_ => Task.FromResult("/uploads/professional/license.pdf"));

        var result = await Save.HandleAsync(Command(owner, document: document));

        Assert.Equal(SaveOnboardingApplicationOutcome.Submitted, result.Outcome);
        var saved = Assert.Single(_db.ProfessionalOnboardingApplications.ToList());
        Assert.Equal(ProfessionalOnboardingStatus.Submitted, saved.Status);
        Assert.NotNull(saved.SubmittedUtc);
        Assert.Equal("NC", saved.LicenseJurisdiction);
        Assert.Equal("Licencia adjunta", saved.DocumentsNote);
        Assert.Equal("/uploads/professional/license.pdf", saved.UploadPath);
        Assert.True(saved.HasPhysicalClinic);
        Assert.False(saved.AcceptsInternationalClients);
        Assert.Equal("", saved.BreedExpertiseCsv);
        Assert.Equal(["onboarding_draft_saved", "onboarding_submitted"], _db.AuditLogs.OrderBy(a => a.Id).Select(a => a.Action));
    }

    [Fact]
    public async Task Saving_a_draft_twice_updates_the_same_application()
    {
        var (owner, _) = AddBusiness("vet");

        await Save.HandleAsync(Command(owner, submit: false));
        var result = await Save.HandleAsync(Command(owner, ProfessionalOnboardingTrack.Behavior, Details(legalName: "Ana"), submit: false));

        Assert.Equal(SaveOnboardingApplicationOutcome.Saved, result.Outcome);
        var saved = Assert.Single(_db.ProfessionalOnboardingApplications.ToList());
        Assert.Equal(ProfessionalOnboardingStatus.Draft, saved.Status);
        Assert.Equal(ProfessionalOnboardingTrack.Behavior, saved.Track);
        Assert.Equal("Ana", saved.LegalName);
    }

    [Fact]
    public async Task Submitting_without_legal_name_keeps_the_draft()
    {
        var (owner, _) = AddBusiness("vet");

        var result = await Save.HandleAsync(Command(owner, details: Details(legalName: "  ")));

        Assert.Equal(SaveOnboardingApplicationOutcome.MissingRequired, result.Outcome);
        Assert.Equal(ProfessionalOnboardingStatus.Draft, Assert.Single(_db.ProfessionalOnboardingApplications.ToList()).Status);
    }

    [Fact]
    public async Task Reapplying_after_a_rejection_starts_a_new_application()
    {
        var (owner, _) = AddBusiness("vet");
        var rejected = AddApplication(owner, ProfessionalOnboardingTrack.Local, ProfessionalOnboardingStatus.Rejected);

        await Save.HandleAsync(Command(owner));

        Assert.Equal(ProfessionalOnboardingStatus.Rejected, Reload(rejected.Id).Status);
        Assert.Equal(2, _db.ProfessionalOnboardingApplications.Count());
    }

    [Fact]
    public async Task An_international_application_resolves_the_country_from_the_catalog()
    {
        var (owner, _) = AddBusiness("vet");
        _db.CountryCatalog.Add(new CountryCatalogEntry { Iso2 = "ZB", NameEs = "Zembla", NameEn = "Zembla" });
        _db.SaveChanges();

        var result = await Save.HandleAsync(Command(owner, ProfessionalOnboardingTrack.International,
            Details(jurisdiction: "", breeds: " Beagle , Pug "), countrySearch: "Zembla"));

        Assert.Equal(SaveOnboardingApplicationOutcome.Submitted, result.Outcome);
        Assert.Equal("ZB", result.LicenseJurisdiction);
        var saved = Assert.Single(_db.ProfessionalOnboardingApplications.ToList());
        Assert.Equal("ZB", saved.LicenseJurisdiction);
        Assert.Equal("Beagle , Pug", saved.BreedExpertiseCsv);
        Assert.True(saved.AcceptsInternationalClients);
        Assert.False(saved.HasPhysicalClinic);
        Assert.False(saved.VcprCapable);
    }

    [Fact]
    public async Task An_unknown_country_saves_nothing()
    {
        var (owner, _) = AddBusiness("vet");
        var stored = false;

        var result = await Save.HandleAsync(Command(owner, ProfessionalOnboardingTrack.International,
            Details(jurisdiction: "Atlantis"), document: new OnboardingDocument(_ => { stored = true; return Task.FromResult("x"); })));

        Assert.Equal(SaveOnboardingApplicationOutcome.CountryNotFound, result.Outcome);
        Assert.False(stored);
        Assert.Empty(_db.ProfessionalOnboardingApplications);
    }

    [Fact]
    public async Task Approving_a_local_vet_verifies_the_license_and_adds_the_compensation_rule()
    {
        var (owner, business) = AddBusiness("vet");
        var application = AddApplication(owner, ProfessionalOnboardingTrack.Local, jurisdiction: "nc");
        var reviewer = TestData.AddUser(_db, UserRole.Admin);

        var result = await Approve.HandleAsync(new ApproveOnboardingCommand(application.Id, reviewer.Id));

        Assert.Equal(ApproveOnboardingOutcome.Approved, result.Outcome);
        Assert.Equal(ProfessionalOnboardingStatus.Approved, Reload(application.Id).Status);

        using var db = _database.CreateContext();
        var saved = db.Groomers.Single(g => g.Id == business.Id);
        Assert.Equal(VetProviderKind.LocalVet, saved.VetProviderKind);
        Assert.True(saved.VerifiedLicense);
        Assert.Equal("Clínica Ana", saved.BusinessName);
        var license = Assert.Single(db.ProviderLicenses.Where(l => l.GroomerId == business.Id).ToList());
        Assert.True(license is { Jurisdiction: "NC", LicenseNumber: "LIC-1", IsVerified: true, IsUsState: true });
        Assert.Single(db.ProviderCompensationRules.Where(r => r.ProviderUserId == owner.Id && r.ServiceType == CompensationServiceType.LocalVet));
        Assert.Contains(db.Notifications, n => n.UserId == owner.Id && n.Type == "professional");
        Assert.Contains(db.AuditLogs, a => a.Action == "onboarding_approved" && a.ActorUserId == reviewer.Id);
    }

    [Fact]
    public async Task Approving_an_international_advisor_adds_the_country_and_breeds_once()
    {
        var (owner, business) = AddBusiness("vet");
        _db.ProviderBreedExpertises.Add(new ProviderBreedExpertise { GroomerId = business.Id, Breed = "Beagle" });
        _db.SaveChanges();
        var application = AddApplication(owner, ProfessionalOnboardingTrack.International,
            jurisdiction: "co", breeds: "beagle, Pug, pug", languages: "es,en");

        await Approve.HandleAsync(new ApproveOnboardingCommand(application.Id, TestData.AddUser(_db, UserRole.Admin).Id));

        using var db = _database.CreateContext();
        var saved = db.Groomers.Single(g => g.Id == business.Id);
        Assert.Equal(VetProviderKind.InternationalAdvisor, saved.VetProviderKind);
        Assert.Equal("CO", saved.LicenseCountry);
        Assert.Equal("es,en", saved.SpokenLanguages);
        Assert.False(Assert.Single(db.ProviderLicenses.Where(l => l.GroomerId == business.Id).ToList()).IsUsState);
        Assert.Equal(["Beagle", "Pug"], db.ProviderBreedExpertises.Where(e => e.GroomerId == business.Id).OrderBy(e => e.Breed).Select(e => e.Breed));
        Assert.Single(db.ProviderCompensationRules.Where(r => r.ProviderUserId == owner.Id && r.ServiceType == CompensationServiceType.InternationalVet));
    }

    [Fact]
    public async Task Approving_a_behavior_specialist_sets_the_consultant_role()
    {
        var (owner, business) = AddBusiness("trainers");
        var application = AddApplication(owner, ProfessionalOnboardingTrack.Behavior);

        await Approve.HandleAsync(new ApproveOnboardingCommand(application.Id, TestData.AddUser(_db, UserRole.Admin).Id));

        using var db = _database.CreateContext();
        var saved = db.Groomers.Single(g => g.Id == business.Id);
        Assert.Equal(VetProviderKind.BehaviorSpecialist, saved.VetProviderKind);
        Assert.Equal(BehaviorSpecialistRole.Consultant, saved.BehaviorRole);
        Assert.Empty(db.ProviderLicenses.Where(l => l.GroomerId == business.Id));
    }

    [Fact]
    public async Task Only_pending_applications_with_a_business_can_be_approved()
    {
        var (owner, _) = AddBusiness("vet");
        var draft = AddApplication(owner, ProfessionalOnboardingTrack.Local, ProfessionalOnboardingStatus.Draft);
        var withoutBusiness = AddApplication(TestData.AddUser(_db), ProfessionalOnboardingTrack.Local);

        Assert.Equal(ApproveOnboardingOutcome.NotPending, (await Approve.HandleAsync(new ApproveOnboardingCommand(draft.Id, 1))).Outcome);
        Assert.Equal(ApproveOnboardingOutcome.NoBusinessProfile, (await Approve.HandleAsync(new ApproveOnboardingCommand(withoutBusiness.Id, 1))).Outcome);
        Assert.Equal(ApproveOnboardingOutcome.NotFound, (await Approve.HandleAsync(new ApproveOnboardingCommand(9999, 1))).Outcome);
        Assert.Equal(ProfessionalOnboardingStatus.Submitted, Reload(withoutBusiness.Id).Status);
    }

    [Fact]
    public async Task Rejecting_sends_the_reviewer_notes_to_the_applicant()
    {
        var (owner, _) = AddBusiness("vet");
        var application = AddApplication(owner, ProfessionalOnboardingTrack.Local);

        var rejected = await Reject.HandleAsync(new RejectOnboardingCommand(application.Id, 1, "Falta la licencia."));

        Assert.NotNull(rejected);
        var saved = Reload(application.Id);
        Assert.Equal(ProfessionalOnboardingStatus.Rejected, saved.Status);
        Assert.Equal("Falta la licencia.", saved.ReviewerNotes);
        Assert.Contains(_db.Notifications, n => n.UserId == owner.Id && n.Message == "Falta la licencia.");
        Assert.Null(await Reject.HandleAsync(new RejectOnboardingCommand(9999, 1, "x")));
    }

    [Fact]
    public async Task Pending_lists_submitted_and_under_review_oldest_first()
    {
        var (first, _) = AddBusiness("vet");
        var (second, _) = AddBusiness("vet");
        var (third, _) = AddBusiness("vet");
        var older = AddApplication(first, ProfessionalOnboardingTrack.Local, ProfessionalOnboardingStatus.UnderReview);
        older.SubmittedUtc = DateTime.UtcNow.AddDays(-2);
        var newer = AddApplication(second, ProfessionalOnboardingTrack.Local);
        AddApplication(third, ProfessionalOnboardingTrack.Local, ProfessionalOnboardingStatus.Draft);
        _db.SaveChanges();

        var pending = await new GetPendingOnboardingsHandler(_db).HandleAsync();

        Assert.Equal([older.Id, newer.Id], pending.Select(a => a.Id));
    }
}
