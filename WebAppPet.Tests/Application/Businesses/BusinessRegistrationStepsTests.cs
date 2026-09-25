using WebAppPet.Application.Businesses.CreateBusiness;
using WebAppPet.Models;

namespace WebAppPet.Tests.Application.Businesses;

public class BusinessRegistrationStepsTests
{
    private static readonly BusinessBasics ValidBasics =
        new(" Ana ", " Peluquería Luna ", "300 123 4567", " ana@test.local ", " Bogotá ", 4.711, -74.0721);

    [Theory]
    [InlineData("business", RegistrationError.None)]
    [InlineData("independent", RegistrationError.None)]
    [InlineData("otro", RegistrationError.ChooseType)]
    [InlineData(null, RegistrationError.ChooseType)]
    public void Type_must_be_business_or_independent(string? kind, RegistrationError expected) =>
        Assert.Equal(expected, BusinessRegistrationSteps.ValidateType(kind));

    [Fact]
    public void Basics_are_trimmed_and_phone_normalized()
    {
        var (error, value) = BusinessRegistrationSteps.ValidateBasics(ValidBasics);

        Assert.Equal(RegistrationError.None, error);
        Assert.Equal(new BusinessBasics("Ana", "Peluquería Luna", "3001234567", "ana@test.local", "Bogotá", 4.711, -74.0721), value);
    }

    public static TheoryData<BusinessBasics, RegistrationError> InvalidBasics => new()
    {
        { ValidBasics with { FullName = " " }, RegistrationError.NameRequired },
        { ValidBasics with { BusinessName = "" }, RegistrationError.BusinessNameRequired },
        { ValidBasics with { Phone = " " }, RegistrationError.PhoneRequired },
        { ValidBasics with { Phone = "12ab" }, RegistrationError.PhoneInvalid },
        { ValidBasics with { Email = "ana" }, RegistrationError.EmailInvalid },
        { ValidBasics with { City = "" }, RegistrationError.LocationRequired },
        { ValidBasics with { Latitude = 0, Longitude = 0 }, RegistrationError.LocationRequired }
    };

    [Theory]
    [MemberData(nameof(InvalidBasics))]
    public void Basics_report_the_first_problem(BusinessBasics input, RegistrationError expected) =>
        Assert.Equal(expected, BusinessRegistrationSteps.ValidateBasics(input).Error);

    [Fact]
    public void Categories_must_be_selected_and_active()
    {
        var active = new[] { new ServiceCategory { Id = 1 }, new ServiceCategory { Id = 2 } };

        Assert.Equal(RegistrationError.None, BusinessRegistrationSteps.ValidateCategories([1, 2], active));
        Assert.Equal(RegistrationError.PickService, BusinessRegistrationSteps.ValidateCategories([], active));
        Assert.Equal(RegistrationError.PickService, BusinessRegistrationSteps.ValidateCategories([1, 9], active));
    }

    [Fact]
    public void Schedule_needs_one_open_day()
    {
        var week = WeekDayInput.DefaultWeek();
        Assert.Equal(RegistrationError.None, BusinessRegistrationSteps.ValidateSchedule(week));

        week.ForEach(d => d.IsOpen = false);
        Assert.Equal(RegistrationError.OpenDay, BusinessRegistrationSteps.ValidateSchedule(week));
    }

    [Theory]
    [InlineData("Bañamos y peinamos perros", RegistrationError.None)]
    [InlineData("   corto          ", RegistrationError.Description)]
    [InlineData(null, RegistrationError.Description)]
    public void About_needs_twenty_characters(string? about, RegistrationError expected) =>
        Assert.Equal(expected, BusinessRegistrationSteps.ValidateAbout(about));

    [Theory]
    [InlineData(new[] { "Baño" }, new[] { 30.0 }, false, false, RegistrationError.None)]
    [InlineData(new string[0], new double[0], false, false, RegistrationError.ServicePrice)]
    [InlineData(new[] { "Baño" }, new[] { 0.0 }, false, false, RegistrationError.ServicePrice)]
    [InlineData(new[] { "Baño" }, new[] { 30.0 }, true, false, RegistrationError.AcceptTerms)]
    [InlineData(new[] { "Baño" }, new[] { 30.0 }, true, true, RegistrationError.None)]
    public void Prices_need_one_paid_service_and_terms_for_signed_in_users(
        string[] names, double[] prices, bool signedIn, bool acceptTerms, RegistrationError expected) =>
        Assert.Equal(expected, BusinessRegistrationSteps.ValidatePrices(
            names, prices.Select(p => (decimal)p), signedIn, acceptTerms));

    [Theory]
    [InlineData(false, "Clave#2026", "Clave#2026", true, RegistrationError.None)]
    [InlineData(false, "123456", "123456", true, RegistrationError.PasswordWeak)]
    [InlineData(false, "Clave#2026", "Clave#2027", true, RegistrationError.PasswordMismatch)]
    [InlineData(false, "Clave#2026", "Clave#2026", false, RegistrationError.AcceptTerms)]
    [InlineData(true, null, null, true, RegistrationError.None)]
    public void Account_checks_password_only_for_guests(
        bool signedIn, string? password, string? confirm, bool acceptTerms, RegistrationError expected) =>
        Assert.Equal(expected, BusinessRegistrationSteps.ValidateAccount(signedIn, password, confirm, acceptTerms));

    [Fact]
    public void Defaults_merge_selected_categories_without_duplicates_and_fall_back_to_grooming()
    {
        var (names, prices) = BusinessServiceDefaults.ForCategories(
            [new ServiceCategory { Slug = "walkers" }, new ServiceCategory { Slug = "walkers" }, new ServiceCategory { Slug = "hotel" }]);
        Assert.Equal(["Paseo 30 min", "Paseo 60 min", "Noche estándar", "Noche premium"], names);
        Assert.Equal([15m, 25m, 45m, 60m], prices);

        Assert.Equal(["Baño básico", "Corte de pelo", "Grooming completo"], BusinessServiceDefaults.ForCategories([]).Names);
    }

    [Fact]
    public void English_placeholders_go_back_to_spanish_only_when_all_are_placeholders()
    {
        var onlyPlaceholders = new List<string> { "Basic bath", " Haircut " };
        BusinessServiceDefaults.NormalizePlaceholdersToSpanish(onlyPlaceholders);
        Assert.Equal(["Baño básico", "Corte de pelo"], onlyPlaceholders);

        var custom = new List<string> { "Basic bath", "Spa canino" };
        BusinessServiceDefaults.NormalizePlaceholdersToSpanish(custom);
        Assert.Equal(["Basic bath", "Spa canino"], custom);
    }

    [Fact]
    public void Normalize_drops_blank_names_and_pairs_prices()
    {
        var (names, prices) = BusinessServiceDefaults.Normalize([" Baño ", "", null, "Corte"], [30m, 10m, 5m]);

        Assert.Equal(["Baño", "Corte"], names);
        Assert.Equal([30m, 0m], prices);
    }

    [Theory]
    [InlineData(0, true, 0)]
    [InlineData(10, false, 10)]
    [InlineData(10, true, 6)]
    [InlineData(1, true, 1)]
    public void Service_area_is_stored_in_miles(int selected, bool usesKm, int expected) =>
        Assert.Equal(expected, CreateBusinessHandler.ToStoredMiles(selected, usesKm));
}
