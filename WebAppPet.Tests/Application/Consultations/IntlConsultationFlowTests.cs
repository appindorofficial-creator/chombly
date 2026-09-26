using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Businesses.Shared;
using WebAppPet.Application.Consultations.FindIntlVet;
using WebAppPet.Application.Consultations.GetIntlHome;
using WebAppPet.Application.Consultations.GetIntlMatches;
using WebAppPet.Application.Consultations.GetIntlSchedule;
using WebAppPet.Application.Consultations.ScheduleIntlConsultation;
using WebAppPet.Application.Consultations.SelectIntlVet;
using WebAppPet.Application.Consultations.Shared;
using WebAppPet.Models;
using WebAppPet.Services;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Consultations;

public class IntlConsultationFlowTests : ConsultationTestBase
{
    public IntlConsultationFlowTests()
    {
        Db.CountryCatalog.Add(new CountryCatalogEntry { Iso2 = "CO", NameEs = "Colombia", NameEn = "Colombia", Status = CountryMarketStatus.Available });
        Db.SaveChanges();
    }

    private FindIntlVetHandler FindVet => new(Db, Router, Audit);
    private GetIntlMatchesHandler GetMatches => new(Db, new CountryCatalogService(Db), Catalog, Care, Audit);
    private GetIntlScheduleHandler GetSchedule => new(Db, Catalog, Care, new AvailabilityService(Db), HomeCountry);
    private ScheduleIntlConsultationHandler Schedule => new(Db, GetSchedule, new ConsentService(Db), Audit);

    private GroomerProfile AddAdvisor(string languages = "es", double rating = 4, decimal startingPrice = 0,
        VetProviderKind kind = VetProviderKind.InternationalAdvisor)
    {
        var advisor = TestData.AddBusiness(Db);
        advisor.IsActive = true;
        advisor.PublishStatus = BusinessPublishStatus.Approved;
        advisor.VetProviderKind = kind;
        advisor.SpokenLanguages = languages;
        advisor.LicenseCountry = "CO";
        advisor.Rating = rating;
        advisor.StartingPrice = startingPrice;
        Db.SaveChanges();
        return advisor;
    }

    private Consultation WithAdvisor(AppUser client, GroomerProfile advisor)
    {
        var consultation = AddConsultation(client, AddPet(client), ConsultationStatus.ProviderSelected);
        consultation.ProviderId = advisor.Id;
        Db.SaveChanges();
        return consultation;
    }

    private static DateTime TomorrowInColombia() =>
        AppTimeZones.TodayLocalDate(AppTimeZones.MarketFromCountry("CO")).AddDays(1);

    [Fact]
    public async Task Finding_a_vet_opens_a_guidance_draft_for_the_pets_breed()
    {
        var client = AddClient("CO");
        var pet = AddPet(client, "Poodle");

        var result = await FindVet.HandleAsync(new FindIntlVetCommand(client.Id, null, pet.Id));

        Assert.Equal(ConsultationStep.IntlMatches, result.Next);
        var saved = Reload(result.ConsultationId);
        Assert.Equal((client.Id, pet.Id, ServiceCatalogCodes.VetIntl30, IntlMatchMode.Best, "Poodle", "Other", ConsultationStatus.Draft),
            (saved.ClientId, saved.PetId, saved.ServiceCatalogCode, saved.MatchMode, saved.PreferredBreed, saved.PetUsState, saved.Status));
        Assert.Equal(["intl_landing_view"], AuditActions());
    }

    [Fact]
    public async Task Finding_a_vet_without_a_pet_asks_for_it_and_never_touches_other_clients_consultations()
    {
        var client = AddClient("CO");
        var someoneElses = AddConsultation(AddClient(), catalogCode: ServiceCatalogCodes.VetLocal30);

        var result = await FindVet.HandleAsync(new FindIntlVetCommand(client.Id, someoneElses.Id, null));

        Assert.Equal(ConsultationStep.Pet, result.Next);
        Assert.NotEqual(someoneElses.Id, result.ConsultationId);
        Assert.Equal(ServiceCatalogCodes.VetLocal30, Reload(someoneElses.Id).ServiceCatalogCode);
    }

    [Fact]
    public async Task The_landing_shows_the_consultations_pet_and_the_clients_language()
    {
        var client = AddClient();
        client.PreferredLanguage = "en";
        Db.SaveChanges();
        var pet = AddPet(client, "Beagle");
        var consultation = AddConsultation(client, pet);

        var home = await new GetIntlHomeHandler(Db).HandleAsync(new GetIntlHomeQuery(client.Id, consultation.Id, null));
        var visitor = await new GetIntlHomeHandler(Db).HandleAsync(new GetIntlHomeQuery(null, consultation.Id, null));

        Assert.Equal(("en", pet.Name, "Beagle"), (home.PreferredLanguage, home.PetName, home.PetBreed));
        Assert.Equal(("es", null, null), (visitor.PreferredLanguage, visitor.PetName, visitor.PetBreed));
    }

    [Fact]
    public async Task Matches_need_the_clients_consultation_with_a_pet()
    {
        var client = AddClient();
        var noPet = AddConsultation(client);
        var someoneElses = AddConsultation(AddClient(), AddPet(client));

        Assert.Equal(ConsultationStep.Pet, (await GetMatches.HandleAsync(new GetIntlMatchesQuery(client.Id, noPet.Id, null))).Redirect);
        Assert.Equal(ConsultationStep.VetHome, (await GetMatches.HandleAsync(new GetIntlMatchesQuery(client.Id, someoneElses.Id, null))).Redirect);
        Assert.False(await Db.AuditLogs.AnyAsync());
    }

    [Fact]
    public async Task Matches_show_advisors_who_speak_the_clients_language_at_their_own_price()
    {
        var client = AddClient();
        AddCare(client);
        var consultation = AddConsultation(client, AddPet(client), catalogCode: ServiceCatalogCodes.VetLocal30);
        var spanishOnly = AddAdvisor("es", rating: 3);
        var bilingual = AddAdvisor("es, EN", rating: 5, startingPrice: 45);
        AddAdvisor("en");
        AddAdvisor("es", kind: VetProviderKind.LocalVet);

        var result = await GetMatches.HandleAsync(new GetIntlMatchesQuery(client.Id, consultation.Id, null));

        Assert.Null(result.Redirect);
        Assert.Equal([bilingual.Id, spanishOnly.Id], result.Matches.Select(m => m.Provider.Id));
        Assert.Equal((45m, 30m), (result.Matches[0].Price, result.Matches[1].Price));
        Assert.Contains("Colombia", result.Matches[0].Why);
        Assert.Equal("ES, EN", result.Matches[0].LanguagesDisplay);
        Assert.Equal([("es", 2, true), ("en", 2, false)], result.LanguageChips.Select(c => (c.Code, c.Count, c.Active)));
        Assert.Equal(("es", true, 1), (result.ActiveLang, result.HasCareBenefit, result.CareRemaining));
        Assert.Equal((ServiceCatalogCodes.VetIntl30, IntlMatchMode.Best), (Reload(consultation.Id).ServiceCatalogCode, Reload(consultation.Id).MatchMode));
        Assert.Equal(["match_viewed"], AuditActions());
    }

    [Fact]
    public async Task When_nobody_speaks_the_selected_language_everyone_is_shown()
    {
        var client = AddClient();
        var consultation = AddConsultation(client, AddPet(client));
        AddAdvisor("es");
        AddAdvisor("en");

        var result = await GetMatches.HandleAsync(new GetIntlMatchesQuery(client.Id, consultation.Id, " PT "));

        Assert.Equal("pt", result.ActiveLang);
        Assert.Equal(2, result.Matches.Count);
        Assert.All(result.Matches, m => Assert.False(m.SpeaksUserLang));
    }

    [Fact]
    public async Task Selecting_an_advisor_assigns_them_in_their_country_and_uses_Care_when_left()
    {
        var client = AddClient();
        AddCare(client);
        var consultation = AddConsultation(client, AddPet(client));
        var advisor = AddAdvisor();
        var localVet = AddAdvisor(kind: VetProviderKind.LocalVet);
        var handler = new SelectIntlVetHandler(Db, Care, Audit);

        var unavailable = await handler.HandleAsync(new SelectIntlVetCommand(client.Id, consultation.Id, localVet.Id));
        var selected = await handler.HandleAsync(new SelectIntlVetCommand(client.Id, consultation.Id, advisor.Id));
        var notMine = await handler.HandleAsync(new SelectIntlVetCommand(AddClient().Id, consultation.Id, advisor.Id));

        Assert.Equal((SelectIntlVetOutcome.ProviderUnavailable, SelectIntlVetOutcome.Selected, SelectIntlVetOutcome.NotFound), (unavailable, selected, notMine));
        var saved = Reload(consultation.Id);
        Assert.Equal((advisor.Id, "CO", ConsultationStatus.ProviderSelected, true),
            (saved.ProviderId, saved.ContextCountry, saved.Status, saved.UsesCareBenefit));
        Assert.Equal(["profile_viewed"], AuditActions());
    }

    [Fact]
    public async Task The_schedule_needs_a_chosen_advisor()
    {
        var client = AddClient();
        var consultation = AddConsultation(client, AddPet(client));

        Assert.Null(await GetSchedule.HandleAsync(new GetIntlScheduleQuery(client.Id, consultation.Id, "hoy", null)));
    }

    [Fact]
    public async Task Without_weekly_hours_the_default_slots_are_offered_and_Care_is_applied()
    {
        var client = AddClient();
        AddCare(client);
        var consultation = WithAdvisor(client, AddAdvisor(startingPrice: 40));

        var schedule = await GetSchedule.HandleAsync(new GetIntlScheduleQuery(client.Id, consultation.Id, "mañana", "9:00 AM"));

        Assert.NotNull(schedule);
        Assert.Equal(BookingTimeSlots(), schedule.TimeSlots);
        Assert.Empty(schedule.PastSlots);
        Assert.Equal((true, "9:00 AM", 40m, true), (schedule.DayOpen, schedule.Slot, schedule.ConsultPrice, schedule.UsingCare));
        Assert.True(Reload(consultation.Id).UsesCareBenefit);
    }

    [Fact]
    public async Task Weekly_hours_set_the_slots_and_a_closed_day_offers_none()
    {
        var client = AddClient();
        var advisor = AddAdvisor();
        var tomorrow = TomorrowInColombia();
        Db.WeeklyHours.AddRange(
            new BusinessWeeklyHour { GroomerId = advisor.Id, DayOfWeek = (int)tomorrow.DayOfWeek, IsOpen = true, OpenMinutes = 14 * 60, CloseMinutes = 15 * 60 + 30 },
            new BusinessWeeklyHour { GroomerId = advisor.Id, DayOfWeek = (int)tomorrow.AddDays(-1).DayOfWeek, IsOpen = false });
        Db.SaveChanges();
        var consultation = WithAdvisor(client, advisor);

        var open = await GetSchedule.HandleAsync(new GetIntlScheduleQuery(client.Id, consultation.Id, "mañana", "10:00 AM"));
        var closed = await GetSchedule.HandleAsync(new GetIntlScheduleQuery(client.Id, consultation.Id, "hoy", null));

        Assert.Equal(["2:00 PM", "2:30 PM", "3:00 PM"], open!.TimeSlots);
        Assert.Null(open.Slot);
        Assert.Equal((false, 0), (closed!.DayOpen, closed.TimeSlots.Count));
    }

    [Fact]
    public async Task Scheduling_tomorrow_saves_the_advisors_local_time_and_the_consent()
    {
        var client = AddClient();
        var consultation = WithAdvisor(client, AddAdvisor());

        var result = await Schedule.HandleAsync(new ScheduleIntlConsultationCommand(client.Id, consultation.Id, "mañana", "10:00 AM", true, "10.0.0.1", "Tests"));

        Assert.Equal(ScheduleIntlConsultationOutcome.Scheduled, result.Outcome);
        var expected = AppTimeZones.LocalDateAndTimeToUtc(TomorrowInColombia(), TimeSpan.FromHours(10), AppTimeZones.MarketFromCountry("CO"));
        var saved = Reload(consultation.Id);
        Assert.Equal((expected, ConsultationStatus.ProviderSelected, ServiceCatalogCodes.VetIntl30),
            (saved.ScheduledAt, saved.Status, saved.ServiceCatalogCode));
        Assert.Equal(ConsentService.DocIntlOrientation, (await Db.ConsentRecords.SingleAsync()).DocumentKey);
        Assert.Equal(["schedule_selected"], AuditActions());
    }

    [Theory]
    [InlineData(false, "mañana", "10:00 AM")]
    [InlineData(true, "pasado", "10:00 AM")]
    [InlineData(true, "mañana", "8:15 AM")]
    [InlineData(true, "mañana", null)]
    public async Task Scheduling_needs_the_scope_accepted_and_a_free_slot(bool acceptScope, string when, string? slot)
    {
        var client = AddClient();
        var consultation = WithAdvisor(client, AddAdvisor());

        var result = await Schedule.HandleAsync(new ScheduleIntlConsultationCommand(client.Id, consultation.Id, when, slot, acceptScope, null, null));

        Assert.Equal(ScheduleIntlConsultationOutcome.Invalid, result.Outcome);
        Assert.NotNull(result.Schedule);
        Assert.Null(Reload(consultation.Id).ScheduledAt);
        Assert.False(await Db.ConsentRecords.AnyAsync());
    }

    private static List<string> BookingTimeSlots() => WebAppPet.Pages.Shared.BookingTime.DefaultSlots.ToList();
}
