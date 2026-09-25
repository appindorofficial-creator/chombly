using WebAppPet.Application.Bookings.GetBookings;
using WebAppPet.Data;
using WebAppPet.Models;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Bookings;

public class GetBookingsHandlersTests : IDisposable
{
    private static readonly DateTime Now = new(2030, 6, 15, 12, 0, 0);

    private readonly TestDatabase _database = new();
    private readonly AppDbContext _db;
    private readonly AppUser _client;
    private readonly GroomerProfile _business;

    public GetBookingsHandlersTests()
    {
        _db = _database.CreateContext();
        _client = TestData.AddUser(_db);
        _business = TestData.AddBusiness(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private Appointment Add(AppointmentStatus status, DateTime at, AppUser? client = null, GroomerProfile? business = null) =>
        TestData.AddAppointment(_db, client ?? _client, business ?? _business, status, at);

    private Task<List<Appointment>> Client(bool history) =>
        new GetClientBookingsHandler(_database.CreateContext())
            .HandleAsync(new GetClientBookingsQuery(_client.Id, history, Now));

    private Task<List<Appointment>> Business(BusinessBookingsTab tab) =>
        new GetBusinessBookingsHandler(_database.CreateContext())
            .HandleAsync(new GetBusinessBookingsQuery(_business.Id, tab));

    [Fact]
    public async Task Client_upcoming_lists_active_bookings_from_today_soonest_first()
    {
        var later = Add(AppointmentStatus.Confirmed, Now.AddDays(3));
        var earlierToday = Add(AppointmentStatus.Pending, Now.Date.AddHours(8));
        Add(AppointmentStatus.Pending, Now.AddDays(-1));
        Add(AppointmentStatus.Cancelled, Now.AddDays(2));
        Add(AppointmentStatus.Completed, Now.AddDays(2));
        Add(AppointmentStatus.Pending, Now.AddDays(1), client: TestData.AddUser(_db));

        var items = await Client(history: false);

        Assert.Equal(new[] { earlierToday.Id, later.Id }, items.Select(a => a.Id));
        Assert.All(items, a => Assert.NotNull(a.Groomer));
    }

    [Fact]
    public async Task Client_history_lists_finished_or_past_bookings_newest_first()
    {
        var pastPending = Add(AppointmentStatus.Pending, Now.AddDays(-2));
        var futureCancelled = Add(AppointmentStatus.Cancelled, Now.AddDays(5));
        var completed = Add(AppointmentStatus.Completed, Now.AddDays(-1));
        Add(AppointmentStatus.Confirmed, Now.AddDays(1));

        var items = await Client(history: true);

        Assert.Equal(new[] { futureCancelled.Id, completed.Id, pastPending.Id }, items.Select(a => a.Id));
    }

    [Fact]
    public async Task Business_requests_are_pending_soonest_first()
    {
        var second = Add(AppointmentStatus.Pending, Now.AddDays(2));
        var first = Add(AppointmentStatus.Pending, Now.AddDays(1));
        Add(AppointmentStatus.Confirmed, Now.AddDays(1));
        Add(AppointmentStatus.Pending, Now.AddDays(1), business: TestData.AddBusiness(_db));

        var items = await Business(BusinessBookingsTab.Requests);

        Assert.Equal(new[] { first.Id, second.Id }, items.Select(a => a.Id));
        Assert.All(items, a => Assert.NotNull(a.Client));
    }

    [Fact]
    public async Task Business_upcoming_are_confirmed()
    {
        var confirmed = Add(AppointmentStatus.Confirmed, Now.AddDays(1));
        Add(AppointmentStatus.Pending, Now.AddDays(1));

        var items = await Business(BusinessBookingsTab.Upcoming);

        Assert.Equal(confirmed.Id, Assert.Single(items).Id);
    }

    [Fact]
    public async Task Business_history_is_completed_or_cancelled_newest_first()
    {
        var older = Add(AppointmentStatus.Completed, Now.AddDays(-3));
        var newer = Add(AppointmentStatus.Cancelled, Now.AddDays(-1));
        Add(AppointmentStatus.Confirmed, Now.AddDays(-2));

        var items = await Business(BusinessBookingsTab.History);

        Assert.Equal(new[] { newer.Id, older.Id }, items.Select(a => a.Id));
    }
}
