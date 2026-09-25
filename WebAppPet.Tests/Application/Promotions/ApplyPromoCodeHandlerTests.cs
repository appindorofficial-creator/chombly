using WebAppPet.Application.Promotions.ApplyPromoCode;
using WebAppPet.Models;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Promotions;

public class ApplyPromoCodeHandlerTests : IDisposable
{
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private ApplyPromoCodeHandler CreateHandler(FakeHostEnvironment? env = null) =>
        new(_database.CreateContext(), new KeyLocalizer(), env ?? FakeHostEnvironment.Production());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Empty_code_returns_subtotal_without_error(string? code)
    {
        var result = await CreateHandler().HandleAsync(new ApplyPromoCodeCommand(1, code, 80m));

        Assert.False(result.IsValid);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(0m, result.DiscountAmount);
        Assert.Equal(80m, result.FinalTotal);
    }

    [Fact]
    public async Task Unknown_code_is_rejected()
    {
        var result = await CreateHandler().HandleAsync(new ApplyPromoCodeCommand(1, "NOPE", 80m));

        Assert.False(result.IsValid);
        Assert.Equal("NOPE", result.NormalizedCode);
        Assert.Equal("Promo_ErrInvalid", result.ErrorMessage);
        Assert.Equal(80m, result.FinalTotal);
    }

    [Fact]
    public async Task Anonymous_user_must_log_in()
    {
        var result = await CreateHandler().HandleAsync(new ApplyPromoCodeCommand(null, "TEST10", 80m));

        Assert.False(result.IsValid);
        Assert.Equal("Promo_ErrLogin", result.ErrorMessage);
    }

    [Theory]
    [InlineData("TEST10", 80, 8, 72)]
    [InlineData(" test 10 ", 80, 8, 72)]
    [InlineData("TEST20", 80, 16, 64)]
    [InlineData("TEST10", 33.35, 3.34, 30.01)]
    public async Task Percentage_codes_apply_discount(string code, decimal subtotal, decimal discount, decimal total)
    {
        var result = await CreateHandler().HandleAsync(new ApplyPromoCodeCommand(1, code, subtotal));

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(discount, result.DiscountAmount);
        Assert.Equal(total, result.FinalTotal);
    }

    [Fact]
    public async Task First_booking_code_works_without_prior_bookings()
    {
        using var db = _database.CreateContext();
        var client = TestData.AddUser(db);

        var result = await CreateHandler().HandleAsync(new ApplyPromoCodeCommand(client.Id, "chombly10", 50m));

        Assert.True(result.IsValid);
        Assert.Equal("CHOMBLY10", result.NormalizedCode);
        Assert.Equal(5m, result.DiscountAmount);
        Assert.Equal(45m, result.FinalTotal);
    }

    [Fact]
    public async Task First_booking_code_is_rejected_after_a_prior_booking_in_production()
    {
        using var db = _database.CreateContext();
        var client = TestData.AddUser(db);
        TestData.AddAppointment(db, client, TestData.AddBusiness(db), AppointmentStatus.Pending, DateTime.UtcNow);

        var result = await CreateHandler().HandleAsync(new ApplyPromoCodeCommand(client.Id, "CHOMBLY10", 50m));

        Assert.False(result.IsValid);
        Assert.Equal("Promo_ErrNotFirst", result.ErrorMessage);
        Assert.Equal(50m, result.FinalTotal);
    }

    [Fact]
    public async Task First_booking_code_ignores_prior_bookings_in_development()
    {
        using var db = _database.CreateContext();
        var client = TestData.AddUser(db);
        TestData.AddAppointment(db, client, TestData.AddBusiness(db), AppointmentStatus.Pending, DateTime.UtcNow);

        var result = await CreateHandler(FakeHostEnvironment.Development())
            .HandleAsync(new ApplyPromoCodeCommand(client.Id, "CHOMBLY10", 50m));

        Assert.True(result.IsValid);
        Assert.Equal(45m, result.FinalTotal);
    }
}
