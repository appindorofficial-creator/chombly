using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Consultations.CheckEligibility;
using WebAppPet.Application.Consultations.ChooseConsultationService;
using WebAppPet.Application.Consultations.ContinueVirtual;
using WebAppPet.Application.Consultations.PrepareScreening;
using WebAppPet.Application.Consultations.ScreenConsultation;
using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Application.Consultations.StartConsultation;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Tests.Application.Consultations;

public class VirtualConsultationFlowTests : ConsultationTestBase
{
    private static readonly SafetyScreeningService.SafetyAnswers NoSigns = new(false, false, false, false, false, false, false, null);
    private static readonly SafetyScreeningService.SafetyAnswers Seizures = new(false, true, false, false, false, false, false, null);

    private StartConsultationHandler Start => new(Db, Audit);
    private ScreenConsultationHandler Screen => new(Db, new SafetyScreeningService(), new VcprService(Db), Audit, Router);

    private static ScreenConsultationCommand ScreenCommand(Consultation c, Pet pet, string? next = null,
        SafetyScreeningService.SafetyAnswers? answers = null, bool noRedFlags = true, string? state = "NC",
        ConsultationMedia? media = null) =>
        new(c.ClientId, c.Id, next, pet.Id, state, "  Vomita  ", media, null, answers ?? NoSigns, noRedFlags);

    [Fact]
    public async Task A_client_in_Colombia_always_starts_on_the_guidance_path()
    {
        var client = AddClient("CO");
        var pet = AddPet(client);

        var started = await Start.HandleAsync(new StartConsultationCommand(client.Id, WantsLocal: true, pet.Id));

        Assert.Equal(ConsultationPath.Intl, started.Path);
        var saved = Reload(started.ConsultationId);
        Assert.Equal((ServiceCatalogCodes.VetIntl30, "Other", "CO", pet.Id), (saved.ServiceCatalogCode, saved.PetUsState, saved.ContextCountry, saved.PetId));
        Assert.Equal(ConsultationStatus.Draft, saved.Status);
        Assert.Equal(["modality_selected"], AuditActions());
    }

    [Fact]
    public async Task A_US_client_can_start_the_local_teleconsult_and_foreign_pets_are_ignored()
    {
        var client = AddClient("US");
        var strangersPet = AddPet(AddClient());

        var started = await Start.HandleAsync(new StartConsultationCommand(client.Id, WantsLocal: true, strangersPet.Id));

        Assert.Equal(ConsultationPath.Local, started.Path);
        var saved = Reload(started.ConsultationId);
        Assert.Equal((ServiceCatalogCodes.VetLocal30, "NC", "US"), (saved.ServiceCatalogCode, saved.PetUsState, saved.ContextCountry));
        Assert.Null(saved.PetId);
    }

    [Fact]
    public async Task Preparing_the_form_aligns_the_consultation_with_the_home_market()
    {
        var client = AddClient("CO");
        AddPet(client);
        var consultation = AddConsultation(client, country: "US", usState: "NC");

        var form = await new PrepareScreeningHandler(Db, new VcprService(Db), new SafetyScreeningService())
            .HandleAsync(new PrepareScreeningQuery(client.Id, consultation.Id, null, Edit: false));

        Assert.NotNull(form);
        Assert.Equal("Other", form.PetUsState);
        Assert.Equal(form.Context.Pets[0].Id, form.PetId);
        Assert.Equal(("Other", "CO"), (Reload(consultation.Id).PetUsState, Reload(consultation.Id).ContextCountry));
    }

    [Fact]
    public async Task Missing_confirmation_or_pet_saves_nothing()
    {
        var client = AddClient();
        var pet = AddPet(client);
        var consultation = AddConsultation(client);

        var unconfirmed = await Screen.HandleAsync(ScreenCommand(consultation, pet, noRedFlags: false));
        var strangersPet = await Screen.HandleAsync(ScreenCommand(consultation, AddPet(AddClient())));

        Assert.Equal(ScreenConsultationOutcome.Incomplete, unconfirmed.Outcome);
        Assert.Equal(ScreenConsultationOutcome.Incomplete, strangersPet.Outcome);
        Assert.Equal(ConsultationStatus.Draft, Reload(consultation.Id).Status);
    }

    [Fact]
    public async Task Invalid_media_is_rejected_before_anything_is_saved()
    {
        var client = AddClient();
        var pet = AddPet(client);
        var consultation = AddConsultation(client);
        var saves = 0;
        var media = new ConsultationMedia("Vet_MediaInvalid", _ => { saves++; return Task.FromResult("/uploads/vet/x.jpg"); });

        var result = await Screen.HandleAsync(ScreenCommand(consultation, pet, media: media));

        Assert.Equal(ScreenConsultationOutcome.MediaInvalid, result.Outcome);
        Assert.Equal(0, saves);
        Assert.Null(Reload(consultation.Id).PetId);
    }

    [Fact]
    public async Task A_clean_screening_goes_to_guidance_matches_with_the_pets_breed()
    {
        var client = AddClient("CO");
        var pet = AddPet(client, "Beagle");
        var consultation = AddConsultation(client);
        var media = new ConsultationMedia(null, _ => Task.FromResult("/uploads/vet/rash.jpg"));

        var result = await Screen.HandleAsync(ScreenCommand(consultation, pet, next: "local", media: media));

        Assert.Equal((ScreenConsultationOutcome.Routed, ConsultationStep.IntlMatches), (result.Outcome, result.NextStep));
        var saved = Reload(consultation.Id);
        Assert.Equal((pet.Id, "OTHER", "Vomita", "/uploads/vet/rash.jpg"), (saved.PetId, saved.PetUsState, saved.Symptoms, saved.MediaUrl1));
        Assert.Equal((ServiceCatalogCodes.VetIntl30, IntlMatchMode.Best, "Beagle"), (saved.ServiceCatalogCode, saved.MatchMode, saved.PreferredBreed));
        Assert.Equal(ConsultationStatus.SafetyScreened, saved.Status);
        Assert.False(saved.HasRedFlags);
    }

    [Fact]
    public async Task Warning_signs_stop_the_flow_until_the_client_chooses()
    {
        var client = AddClient();
        var pet = AddPet(client);
        var consultation = AddConsultation(client);

        var result = await Screen.HandleAsync(ScreenCommand(consultation, pet, answers: Seizures, noRedFlags: false));

        Assert.Equal(ScreenConsultationOutcome.RedFlags, result.Outcome);
        var saved = Reload(consultation.Id);
        Assert.True(saved.HasRedFlags);
        Assert.Equal(ConsultationStatus.SafetyScreened, saved.Status);
        Assert.Equal(["safety_flagged"], AuditActions());
    }

    [Theory]
    [InlineData(true, ConsultationStep.LocalProviders, ConsultationStatus.EligibilityVerified)]
    [InlineData(false, ConsultationStep.Eligibility, ConsultationStatus.SafetyScreened)]
    public async Task A_US_client_on_the_local_path_needs_an_active_VCPR(bool hasVcpr, ConsultationStep expected, ConsultationStatus status)
    {
        var client = AddClient("US");
        var pet = AddPet(client);
        if (hasVcpr) AddVcpr(pet, "NC");
        var consultation = AddConsultation(client, catalogCode: ServiceCatalogCodes.VetLocal30, country: "US", usState: "NC");

        var result = await Screen.HandleAsync(ScreenCommand(consultation, pet, state: "nc"));

        Assert.Equal(expected, result.NextStep);
        var saved = Reload(consultation.Id);
        Assert.Equal((ServiceCatalogCodes.VetLocal30, hasVcpr, status), (saved.ServiceCatalogCode, saved.HasActiveVcpr, saved.Status));
    }

    [Fact]
    public async Task Continuing_virtual_after_an_emergency_resumes_on_guidance()
    {
        var client = AddClient("US");
        var pet = AddPet(client);
        AddVcpr(pet, "NC");
        var consultation = AddConsultation(client, pet, ConsultationStatus.EscalatedToEmergency, ServiceCatalogCodes.VetLocal30, "US", "NC");

        var step = await new ContinueVirtualHandler(Db, Audit, Router)
            .HandleAsync(new ContinueVirtualCommand(client.Id, consultation.Id, "local"));

        Assert.Equal(ConsultationStep.IntlMatches, step);
        var saved = Reload(consultation.Id);
        Assert.Equal((ConsultationStatus.SafetyScreened, VetModality.Virtual), (saved.Status, saved.Modality));
        Assert.Equal(["safety_chose_continue_virtual"], AuditActions());
    }

    [Fact]
    public async Task Continuing_virtual_without_a_pet_goes_back_to_the_pet_step()
    {
        var client = AddClient();
        var consultation = AddConsultation(client);

        var step = await new ContinueVirtualHandler(Db, Audit, Router)
            .HandleAsync(new ContinueVirtualCommand(client.Id, consultation.Id, null));

        Assert.Equal(ConsultationStep.Pet, step);
    }

    [Theory]
    [InlineData(ServiceCatalogCodes.ChomblyCare, "CO", ChooseConsultationServiceOutcome.Moved, ConsultationStep.ChomblyCare)]
    [InlineData(ServiceCatalogCodes.BehaviorSession, "CO", ChooseConsultationServiceOutcome.Invalid, null)]
    [InlineData(ServiceCatalogCodes.VetLocal30, "CO", ChooseConsultationServiceOutcome.Invalid, null)]
    [InlineData(ServiceCatalogCodes.VetLocal30, "US", ChooseConsultationServiceOutcome.Moved, ConsultationStep.Eligibility)]
    [InlineData(ServiceCatalogCodes.VetIntl30, "CO", ChooseConsultationServiceOutcome.Moved, ConsultationStep.IntlHome)]
    public async Task Choosing_a_service_routes_by_service_and_home_market(string code, string country, ChooseConsultationServiceOutcome outcome, ConsultationStep? step)
    {
        var client = AddClient(country);
        var consultation = AddConsultation(client);

        var result = await new ChooseConsultationServiceHandler(Db, HomeCountry, Catalog, Care)
            .HandleAsync(new ChooseConsultationServiceCommand(client.Id, consultation.Id, code));

        Assert.Equal((outcome, step), (result.Outcome, result.NextStep));
    }

    [Fact]
    public async Task Choosing_guidance_saves_it_as_verified_and_paid()
    {
        var client = AddClient();
        var consultation = AddConsultation(client, catalogCode: ServiceCatalogCodes.VetLocal30);
        consultation.UsesCareBenefit = true;
        Db.SaveChanges();

        await new ChooseConsultationServiceHandler(Db, HomeCountry, Catalog, Care)
            .HandleAsync(new ChooseConsultationServiceCommand(client.Id, consultation.Id, ServiceCatalogCodes.VetIntl30));

        var saved = Reload(consultation.Id);
        Assert.Equal((ServiceCatalogCodes.VetIntl30, false, ConsultationStatus.EligibilityVerified),
            (saved.ServiceCatalogCode, saved.UsesCareBenefit, saved.Status));
    }

    [Theory]
    [InlineData(true, ConsultationStep.IntlHome)]
    [InlineData(false, ConsultationStep.ChomblyCare)]
    public async Task Using_Care_needs_a_quick_consult_left(bool hasCare, ConsultationStep expected)
    {
        var client = AddClient();
        if (hasCare) AddCare(client);
        var consultation = AddConsultation(client);

        var result = await new ChooseConsultationServiceHandler(Db, HomeCountry, Catalog, Care)
            .HandleAsync(new ChooseConsultationServiceCommand(client.Id, consultation.Id, null, UseCare: true));

        Assert.Equal(expected, result.NextStep);
        Assert.Equal(hasCare, Reload(consultation.Id).UsesCareBenefit);
    }

    [Fact]
    public async Task The_eligibility_notice_sends_clients_outside_the_US_to_guidance()
    {
        var client = AddClient("CO");
        var consultation = AddConsultation(client, catalogCode: ServiceCatalogCodes.VetLocal30, status: ConsultationStatus.EligibilityVerified);

        var check = await new CheckEligibilityHandler(Db, HomeCountry).HandleAsync(new CheckEligibilityQuery(client.Id, consultation.Id));

        Assert.Equal(ConsultationStep.IntlMatches, check.Redirect);
        var saved = Reload(consultation.Id);
        Assert.Equal((ServiceCatalogCodes.VetIntl30, ConsultationStatus.SafetyScreened), (saved.ServiceCatalogCode, saved.Status));
    }

    [Fact]
    public async Task Other_clients_consultations_are_not_found()
    {
        var consultation = AddConsultation(AddClient());
        var other = AddClient();

        var screen = await Screen.HandleAsync(ScreenCommand(consultation, AddPet(other)) with { ClientId = other.Id });
        var eligibility = await new CheckEligibilityHandler(Db, HomeCountry).HandleAsync(new CheckEligibilityQuery(other.Id, consultation.Id));

        Assert.Equal(ScreenConsultationOutcome.NotFound, screen.Outcome);
        Assert.Equal(ConsultationStep.VetHome, eligibility.Redirect);
        Assert.False(await Db.AuditLogs.AnyAsync());
    }
}
