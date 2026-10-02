using WebAppPet.Application.Bookings.GetBookingConfirmation;
using WebAppPet.Application.Bookings.GetBookingForm;
using WebAppPet.Application.Bookings.GetDaySlots;
using WebAppPet.Application.Bookings.Shared;
using WebAppPet.Application.Businesses.Shared;
using WebAppPet.Domain;
using WebAppPet.Domain.Markets;
using WebAppPet.Infrastructure.Persistence;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Bookings;

public class BookingQueriesTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly AppDbContext _db;
    private readonly GroomerProfile _business;
    private readonly AppUser _client;

    /// <summary>A weekday far enough ahead that none of its slots are in the past.</summary>
    private static readonly DateTime FutureMonday = NextMonday(AppTimeZones.TodayLocalDate().AddDays(14));

    public BookingQueriesTests()
    {
        _db = _database.CreateContext();
        _business = TestData.AddBusiness(_db);
        _client = TestData.AddUser(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private static DateTime NextMonday(DateTime from)
    {
        var day = from.Date;
        while (day.DayOfWeek != DayOfWeek.Monday) day = day.AddDays(1);
        return day;
    }

    private GetBookingFormHandler FormHandler() => new(_db);
    private GetDaySlotsHandler SlotsHandler() => new(_db, new AvailabilityService(_db));

    private void AddWeeklyHours(DayOfWeek day, bool isOpen, int openMinutes = 9 * 60, int closeMinutes = 18 * 60)
    {
        _db.WeeklyHours.Add(new BusinessWeeklyHour
        {
            GroomerId = _business.Id,
            DayOfWeek = (int)day,
            IsOpen = isOpen,
            OpenMinutes = openMinutes,
            CloseMinutes = closeMinutes
        });
        _db.SaveChanges();
    }

    private void AddAppointmentAt(DateTime localDay, TimeSpan localTime, AppointmentStatus status = AppointmentStatus.Confirmed) =>
        TestData.AddAppointment(_db, _client, _business, status, AppTimeZones.LocalDateAndTimeToUtc(localDay, localTime));

    [Fact]
    public async Task Form_is_null_for_a_missing_or_inactive_business()
    {
        _business.IsActive = false;
        _db.SaveChanges();

        Assert.Null(await FormHandler().HandleAsync(new GetBookingFormQuery(_business.Id, _client.Id)));
        Assert.Null(await FormHandler().HandleAsync(new GetBookingFormQuery(999_999, _client.Id)));
    }

    [Fact]
    public async Task Form_loads_the_business_offer_and_only_the_clients_pets_and_cards()
    {
        var service = TestData.AddService(_db, _business);
        TestData.AddService(_db, TestData.AddBusiness(_db));
        var extra = TestData.AddExtra(_db, _business, "Perfume", 5);
        var inactive = TestData.AddExtra(_db, _business, "Corte de uñas", 8);
        inactive.IsActive = false;
        _db.SaveChanges();
        var pet = TestData.AddPet(_db, _client);
        TestData.AddPet(_db, TestData.AddUser(_db));
        var otherCard = TestData.AddCard(_db, _client, "1111", isDefault: false);
        var defaultCard = TestData.AddCard(_db, _client, "4242", isDefault: true);

        var form = await FormHandler().HandleAsync(new GetBookingFormQuery(_business.Id, _client.Id));

        Assert.NotNull(form);
        Assert.Equal(_business.Id, form.Business.Id);
        Assert.Equal(service.Id, Assert.Single(form.Services).Id);
        Assert.Equal(extra.Id, Assert.Single(form.Extras).Id);
        Assert.Equal(pet.Id, Assert.Single(form.Pets).Id);
        Assert.Equal(new[] { defaultCard.Id, otherCard.Id }, form.Payments.Select(p => p.Id));
        Assert.False(form.IsOvernight);
    }

    [Fact]
    public async Task Form_without_a_client_has_no_pets_or_cards()
    {
        TestData.AddPet(_db, _client);
        TestData.AddCard(_db, _client);

        var form = await FormHandler().HandleAsync(new GetBookingFormQuery(_business.Id, null));

        Assert.NotNull(form);
        Assert.Empty(form.Pets);
        Assert.Empty(form.Payments);
    }

    [Theory]
    [InlineData("hotel", false, true)]
    [InlineData("daycare", false, true)]
    [InlineData("boarding", true, true)]
    [InlineData("grooming", false, false)]
    public async Task Form_is_overnight_for_hotel_daycare_or_overnight_categories(string slug, bool isOvernight, bool expected)
    {
        var category = new ServiceCategory { Slug = slug, Name = slug, IsOvernight = isOvernight };
        _db.Categories.Add(category);
        _db.SaveChanges();
        _business.CategoryId = category.Id;
        _db.SaveChanges();

        var form = await FormHandler().HandleAsync(new GetBookingFormQuery(_business.Id, _client.Id));

        Assert.Equal(expected, form!.IsOvernight);
    }

    [Fact]
    public async Task Slots_follow_the_weekly_opening_window()
    {
        AddWeeklyHours(DayOfWeek.Monday, isOpen: true, openMinutes: 10 * 60, closeMinutes: 13 * 60);

        var slots = await SlotsHandler().HandleAsync(new GetDaySlotsQuery(_business.Id, false, FutureMonday));

        Assert.True(slots.IsOpen);
        Assert.Equal("10:00–13:00", slots.HoursLabel);
        Assert.Equal(new[] { "10:00 AM", "11:00 AM", "12:00 PM" }, slots.TimeSlots);
        Assert.Equal(slots.TimeSlots, slots.Bookable);
        Assert.Empty(slots.Past);
    }

    [Fact]
    public async Task Slots_taken_by_active_appointments_are_not_bookable_but_cancelled_ones_are()
    {
        AddWeeklyHours(DayOfWeek.Monday, isOpen: true);
        AddAppointmentAt(FutureMonday, TimeSpan.FromHours(10));
        AddAppointmentAt(FutureMonday, TimeSpan.FromHours(11), AppointmentStatus.Cancelled);

        var slots = await SlotsHandler().HandleAsync(new GetDaySlotsQuery(_business.Id, false, FutureMonday));

        Assert.Equal(new[] { "10:00 AM" }, slots.Occupied);
        Assert.DoesNotContain("10:00 AM", slots.Bookable);
        Assert.Contains("11:00 AM", slots.Bookable);
    }

    [Fact]
    public async Task A_closed_day_has_no_bookable_slots()
    {
        AddWeeklyHours(DayOfWeek.Monday, isOpen: false);

        var slots = await SlotsHandler().HandleAsync(new GetDaySlotsQuery(_business.Id, false, FutureMonday));

        Assert.False(slots.IsOpen);
        Assert.Null(slots.HoursLabel);
        Assert.Empty(slots.Bookable);
        Assert.Equal(BookingTime.StandardDaySlots, slots.TimeSlots);
    }

    [Fact]
    public async Task A_day_marked_unavailable_overrides_open_weekly_hours()
    {
        AddWeeklyHours(DayOfWeek.Monday, isOpen: true);
        _db.DayAvailabilities.Add(new BusinessDayAvailability { GroomerId = _business.Id, Day = FutureMonday, IsAvailable = false });
        _db.SaveChanges();

        var slots = await SlotsHandler().HandleAsync(new GetDaySlotsQuery(_business.Id, false, FutureMonday));

        Assert.False(slots.IsOpen);
        Assert.Empty(slots.Bookable);
    }

    [Fact]
    public async Task Open_all_day_businesses_ignore_weekly_hours()
    {
        AddWeeklyHours(DayOfWeek.Monday, isOpen: false);

        var slots = await SlotsHandler().HandleAsync(new GetDaySlotsQuery(_business.Id, true, FutureMonday));

        Assert.True(slots.IsOpen);
        Assert.Null(slots.HoursLabel);
        Assert.Equal(BookingTime.StandardDaySlots, slots.Bookable);
    }
    [Fact]
    public async Task Confirmation_returns_the_clients_appointment_with_its_latest_successful_charge()
    {
        var appointment = TestData.AddAppointment(_db, _client, _business, AppointmentStatus.Pending, DateTime.UtcNow.AddDays(3));
        TestData.AddCharge(_db, _business, 10, DateTime.UtcNow.AddMinutes(-10), appointment: appointment);
        var latest = TestData.AddCharge(_db, _business, 12, DateTime.UtcNow.AddMinutes(-5), appointment: appointment);
        TestData.AddCharge(_db, _business, 15, DateTime.UtcNow, status: PaymentTransactionStatus.Failed, appointment: appointment);

        var confirmation = await new GetBookingConfirmationHandler(_db)
            .HandleAsync(new GetBookingConfirmationQuery(appointment.Id, _client.Id));

        Assert.NotNull(confirmation);
        Assert.Equal(appointment.Id, confirmation.Appointment.Id);
        Assert.Equal(_business.Id, confirmation.Appointment.Groomer.Id);
        Assert.Equal(latest.Id, confirmation.Payment?.Id);
    }

    [Fact]
    public async Task Confirmation_is_null_for_another_clients_appointment()
    {
        var appointment = TestData.AddAppointment(_db, _client, _business, AppointmentStatus.Pending, DateTime.UtcNow.AddDays(3));
        var stranger = TestData.AddUser(_db);

        Assert.Null(await new GetBookingConfirmationHandler(_db)
            .HandleAsync(new GetBookingConfirmationQuery(appointment.Id, stranger.Id)));
    }
}
