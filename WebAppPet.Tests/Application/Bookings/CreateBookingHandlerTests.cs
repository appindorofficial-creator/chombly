using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Bookings.CreateBooking;
using WebAppPet.Application.Bookings.Shared;
using WebAppPet.Application.Promotions.ApplyPromoCode;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Bookings;

public class CreateBookingHandlerTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly AppDbContext _db;
    private readonly AppUser _client;
    private readonly GroomerProfile _business;
    private readonly GroomerService _service;
    private readonly Pet _dog;

    public CreateBookingHandlerTests()
    {
        _db = _database.CreateContext();
        _client = TestData.AddUser(_db);
        _business = TestData.AddBusiness(_db);
        _service = TestData.AddService(_db, _business);
        _dog = TestData.AddPet(_db, _client);
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private CreateBookingHandler CreateHandler()
    {
        var db = _database.CreateContext();
        return new CreateBookingHandler(db, new ApplyPromoCodeHandler(db, new KeyLocalizer(), FakeHostEnvironment.Production()));
    }

    private CreateBookingCommand Command(params int[] petIds) => new()
    {
        ClientId = _client.Id,
        BusinessId = _business.Id,
        ServiceId = _service.Id,
        PetIds = petIds.Length > 0 ? petIds : new[] { _dog.Id },
        StartUtc = new DateTime(2030, 1, 10, 15, 0, 0, DateTimeKind.Utc),
        Subtotal = 64_000m,
        ClientNotice = new("Reserva enviada", "Pendiente"),
        BusinessNotice = new("Nueva solicitud", "Thor · Baño")
    };

    private Appointment LoadAppointment(int id)
    {
        using var db = _database.CreateContext();
        return db.Appointments.Include(a => a.Extras).AsNoTracking().Single(a => a.Id == id);
    }

    [Fact]
    public async Task Creates_pending_appointment_with_deposit_and_notifications()
    {
        var result = await CreateHandler().HandleAsync(Command() with
        {
            EndUtc = new DateTime(2030, 1, 10, 16, 0, 0, DateTimeKind.Utc),
            NoteParts = new[] { "Tipo: Obediencia", "Sin escaleras" }
        });

        Assert.True(result.Success);
        var appt = LoadAppointment(result.AppointmentId!.Value);
        Assert.Equal(AppointmentStatus.Pending, appt.Status);
        Assert.Equal(_dog.Id, appt.PetId);
        Assert.Equal(_service.Id, appt.ServiceId);
        Assert.Equal(64_000m, appt.TotalPrice);
        Assert.Equal(22_400m, appt.DepositPaid);
        Assert.Equal(0m, appt.DiscountAmount);
        Assert.Null(appt.PromoCode);
        Assert.Equal("Tipo: Obediencia · Sin escaleras", appt.Notes);
        Assert.Equal(new DateTime(2030, 1, 10, 16, 0, 0), appt.EndAt);

        using var db = _database.CreateContext();
        var notices = db.Notifications.AsNoTracking().OrderBy(n => n.Id).ToList();
        Assert.Collection(notices,
            n => { Assert.Equal(_client.Id, n.UserId); Assert.Equal("Reserva enviada", n.Title); Assert.Equal("appointment", n.Type); },
            n => { Assert.Equal(_business.UserId, n.UserId); Assert.Equal("Nueva solicitud", n.Title); Assert.Equal("Thor · Baño", n.Message); });
    }

    [Fact]
    public async Task No_notes_are_stored_as_null()
    {
        var result = await CreateHandler().HandleAsync(Command());

        Assert.Null(LoadAppointment(result.AppointmentId!.Value).Notes);
    }

    [Fact]
    public async Task First_pet_is_the_primary_pet()
    {
        var cat = TestData.AddPet(_db, _client, PetSpecies.Cat);

        var result = await CreateHandler().HandleAsync(Command(cat.Id, _dog.Id));

        Assert.Equal(cat.Id, LoadAppointment(result.AppointmentId!.Value).PetId);
    }

    [Fact]
    public async Task Valid_promo_discounts_total_and_deposit()
    {
        var result = await CreateHandler().HandleAsync(Command() with { PromoCode = "test20" });

        Assert.True(result.Success);
        var appt = LoadAppointment(result.AppointmentId!.Value);
        Assert.Equal("TEST20", appt.PromoCode);
        Assert.Equal(12_800m, appt.DiscountAmount);
        Assert.Equal(51_200m, appt.TotalPrice);
        Assert.Equal(17_920m, appt.DepositPaid);
    }

    [Fact]
    public async Task Invalid_promo_is_rejected_without_saving()
    {
        var result = await CreateHandler().HandleAsync(Command() with { PromoCode = "NOVALE" });

        Assert.Equal(CreateBookingError.InvalidPromo, result.Error);
        Assert.Equal("Promo_ErrInvalid", result.PromoError);
        Assert.Empty(_db.Appointments.AsNoTracking());
        Assert.Empty(_db.Notifications.AsNoTracking());
    }

    [Fact]
    public async Task Walk_minimum_deposit_is_applied()
    {
        var result = await CreateHandler().HandleAsync(Command() with
        {
            Subtotal = 20m,
            MinimumDeposit = BookingPricing.WalkMinimumDeposit
        });

        Assert.Equal(10m, LoadAppointment(result.AppointmentId!.Value).DepositPaid);
    }

    [Fact]
    public async Task Extras_are_stored_with_their_names_and_prices()
    {
        var bath = TestData.AddExtra(_db, _business, "Baño", 5_000m);
        var meds = TestData.AddExtra(_db, _business, "Medicamentos", 3_000m);

        var result = await CreateHandler().HandleAsync(Command() with
        {
            Extras = new[]
            {
                new BookingExtraLine(bath.Id, "Baño", 5_000m),
                new BookingExtraLine(meds.Id, "Medicamentos (Thor, Luna)", 6_000m)
            }
        });

        var extras = LoadAppointment(result.AppointmentId!.Value).Extras.OrderBy(e => e.Id).ToList();
        Assert.Equal(new[] { "Baño", "Medicamentos (Thor, Luna)" }, extras.Select(e => e.Name));
        Assert.Equal(new[] { 5_000m, 6_000m }, extras.Select(e => e.Price));
    }

    [Fact]
    public async Task Species_not_accepted_by_business_is_rejected()
    {
        var business = _db.Groomers.Single(g => g.Id == _business.Id);
        business.AcceptedSpecies = PetSpecies.Dog;
        _db.SaveChanges();
        var cat = TestData.AddPet(_db, _client, PetSpecies.Cat);

        var result = await CreateHandler().HandleAsync(Command(_dog.Id, cat.Id));

        Assert.Equal(CreateBookingError.SpeciesNotAccepted, result.Error);
        Assert.Equal(PetSpecies.Cat, result.RejectedSpecies);
        Assert.Empty(_db.Appointments.AsNoTracking());
    }

    [Fact]
    public async Task Allowed_species_restricts_further()
    {
        var cat = TestData.AddPet(_db, _client, PetSpecies.Cat);

        var result = await CreateHandler().HandleAsync(Command(cat.Id) with { AllowedSpecies = new[] { PetSpecies.Dog } });

        Assert.Equal(CreateBookingError.SpeciesNotAccepted, result.Error);
        Assert.Equal(PetSpecies.Cat, result.RejectedSpecies);
    }

    [Fact]
    public async Task Pet_of_another_client_is_rejected()
    {
        var otherPet = TestData.AddPet(_db, TestData.AddUser(_db));

        var result = await CreateHandler().HandleAsync(Command(otherPet.Id));

        Assert.Equal(CreateBookingError.MissingData, result.Error);
        Assert.Empty(_db.Appointments.AsNoTracking());
    }

    [Fact]
    public async Task Service_of_another_business_is_rejected()
    {
        var otherService = TestData.AddService(_db, TestData.AddBusiness(_db));

        var result = await CreateHandler().HandleAsync(Command() with { ServiceId = otherService.Id });

        Assert.Equal(CreateBookingError.MissingData, result.Error);
    }

    [Fact]
    public async Task Inactive_business_is_rejected()
    {
        var business = _db.Groomers.Single(g => g.Id == _business.Id);
        business.IsActive = false;
        _db.SaveChanges();

        var result = await CreateHandler().HandleAsync(Command());

        Assert.Equal(CreateBookingError.MissingData, result.Error);
    }

    [Fact]
    public async Task No_pets_is_rejected()
    {
        var result = await CreateHandler().HandleAsync(Command() with { PetIds = Array.Empty<int>() });

        Assert.Equal(CreateBookingError.MissingData, result.Error);
    }
}
