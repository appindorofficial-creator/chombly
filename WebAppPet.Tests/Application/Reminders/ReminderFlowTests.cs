using Microsoft.Extensions.Logging.Abstractions;
using WebAppPet.Application.Reminders.CreateCheckupReminder;
using WebAppPet.Application.Reminders.CreateReminder;
using WebAppPet.Application.Reminders.DeactivateReminder;
using WebAppPet.Application.Reminders.GetPetReminders;
using WebAppPet.Domain;
using WebAppPet.Domain.Markets;
using WebAppPet.Infrastructure.Persistence;
using WebAppPet.Services;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Reminders;

public class ReminderFlowTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly AppDbContext _db;
    private readonly AppUser _user;
    private readonly Pet _pet;
    private readonly DateTime _nextWeek = AppTimeZones.TodayLocalDate().AddDays(7);

    public ReminderFlowTests()
    {
        _db = _database.CreateContext();
        _user = TestData.AddUser(_db);
        _pet = TestData.AddPet(_db, _user);
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private CreateReminderCommand Command(
        ReminderType? type = ReminderType.Medication,
        DateTime? nextDue = null,
        string? title = "Antipulgas",
        int? frequencyDays = 30,
        string? quietStart = null,
        string? quietEnd = null,
        int? petId = null) =>
        new(_user.Id, petId ?? _pet.Id, type, title, "Con comida", frequencyDays, nextDue ?? _nextWeek, quietStart, quietEnd);

    private Task<CreateReminderOutcome> Create(CreateReminderCommand command) =>
        new CreateReminderHandler(_db).HandleAsync(command);

    private ReminderSchedule SingleSchedule()
    {
        using var verify = _database.CreateContext();
        return verify.ReminderSchedules.Single();
    }

    [Fact]
    public async Task Creates_a_reminder_at_nine_in_the_morning_of_the_chosen_day()
    {
        var outcome = await Create(Command(quietStart: "21:00", quietEnd: "07:30"));

        Assert.Equal(CreateReminderOutcome.Created, outcome);
        var schedule = SingleSchedule();
        Assert.Equal(_user.Id, schedule.UserId);
        Assert.Equal(_pet.Id, schedule.PetId);
        Assert.Equal(ReminderType.Medication, schedule.Type);
        Assert.Equal("Antipulgas", schedule.Title);
        Assert.Equal("Con comida", schedule.Notes);
        Assert.Equal(30, schedule.FrequencyDays);
        Assert.Equal(AppTimeZones.LocalDateAndTimeToUtc(_nextWeek, TimeSpan.FromHours(9)), schedule.NextDueUtc);
        Assert.Equal(TimeSpan.FromHours(21), schedule.QuietHoursStartLocal);
        Assert.Equal(new TimeSpan(7, 30, 0), schedule.QuietHoursEndLocal);
        Assert.Equal(ReminderChannel.InApp, schedule.Channel);
        Assert.True(schedule.IsActive);
    }

    [Theory]
    [InlineData(ReminderType.Vaccine, "Recordatorio de vacuna")]
    [InlineData(ReminderType.Medication, "Recordatorio de medicamento")]
    [InlineData(ReminderType.Appointment, "Recordatorio de cita")]
    [InlineData(ReminderType.Custom, "Recordatorio de cuidado")]
    public async Task A_blank_title_is_named_after_the_reminder_type(ReminderType type, string expected)
    {
        await Create(Command(type: type, title: "  "));

        Assert.Equal(expected, SingleSchedule().Title);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(null)]
    public async Task No_positive_frequency_means_a_one_time_reminder(int? frequencyDays)
    {
        await Create(Command(frequencyDays: frequencyDays));

        Assert.Null(SingleSchedule().FrequencyDays);
    }

    [Fact]
    public async Task Quiet_hours_accept_twelve_hour_times_and_ignore_garbage()
    {
        await Create(Command(quietStart: "9:30 PM", quietEnd: "later"));

        var schedule = SingleSchedule();
        Assert.Equal(new TimeSpan(21, 30, 0), schedule.QuietHoursStartLocal);
        Assert.Null(schedule.QuietHoursEndLocal);
    }

    [Fact]
    public async Task Today_is_still_a_valid_date()
    {
        var outcome = await Create(Command(nextDue: AppTimeZones.TodayLocalDate()));

        Assert.Equal(CreateReminderOutcome.Created, outcome);
    }

    [Fact]
    public async Task Rejects_a_missing_type_a_missing_date_or_a_past_date()
    {
        Assert.Equal(CreateReminderOutcome.NoType, await Create(Command(type: null)));
        Assert.Equal(CreateReminderOutcome.NoDate,
            await Create(new CreateReminderCommand(_user.Id, _pet.Id, ReminderType.Vaccine, "", null, null, null, null, null)));
        Assert.Equal(CreateReminderOutcome.PastDate, await Create(Command(nextDue: AppTimeZones.TodayLocalDate().AddDays(-1))));

        using var verify = _database.CreateContext();
        Assert.Empty(verify.ReminderSchedules);
    }

    [Fact]
    public async Task Cannot_create_a_reminder_for_someone_elses_pet()
    {
        var otherPet = TestData.AddPet(_db, TestData.AddUser(_db));

        var outcome = await Create(Command(petId: otherPet.Id));

        Assert.Equal(CreateReminderOutcome.PetNotFound, outcome);
    }

    [Fact]
    public async Task Pet_reminders_show_only_active_ones_soonest_first()
    {
        await Create(Command(title: "Later", nextDue: _nextWeek.AddDays(10)));
        await Create(Command(title: "Sooner", nextDue: _nextWeek));
        await Create(Command(title: "Stopped", nextDue: _nextWeek.AddDays(1)));
        var stopped = _db.ReminderSchedules.Single(r => r.Title == "Stopped");
        await new DeactivateReminderHandler(_db).HandleAsync(new DeactivateReminderCommand(_user.Id, stopped.Id));

        var view = await new GetPetRemindersHandler(_db).HandleAsync(new GetPetRemindersQuery(_user.Id, _pet.Id));

        Assert.NotNull(view);
        Assert.Equal(_pet.Id, view.Pet.Id);
        Assert.Equal(["Sooner", "Later"], view.Schedules.Select(s => s.Title));
    }

    [Fact]
    public async Task Someone_elses_pet_has_no_reminders_page()
    {
        var otherPet = TestData.AddPet(_db, TestData.AddUser(_db));

        var view = await new GetPetRemindersHandler(_db).HandleAsync(new GetPetRemindersQuery(_user.Id, otherPet.Id));

        Assert.Null(view);
    }

    [Fact]
    public async Task Cannot_deactivate_someone_elses_reminder()
    {
        await Create(Command());
        var schedule = _db.ReminderSchedules.Single();
        var stranger = TestData.AddUser(_db);

        var result = await new DeactivateReminderHandler(_db).HandleAsync(new DeactivateReminderCommand(stranger.Id, schedule.Id));

        Assert.False(result);
        Assert.True(SingleSchedule().IsActive);
    }

    [Fact]
    public async Task The_care_page_shortcut_creates_a_yearly_checkup_next_week()
    {
        var created = await new CreateCheckupReminderHandler(_db).HandleAsync(new CreateCheckupReminderCommand(_user.Id, _pet.Id));

        Assert.True(created);
        var schedule = SingleSchedule();
        Assert.Equal(ReminderType.Vaccine, schedule.Type);
        Assert.Contains(_pet.Name, schedule.Title);
        Assert.Equal(365, schedule.FrequencyDays);
        Assert.Equal(AppTimeZones.LocalDateAndTimeToUtc(_nextWeek, TimeSpan.FromHours(9)), schedule.NextDueUtc);
        Assert.Equal(TimeSpan.FromHours(21), schedule.QuietHoursStartLocal);
        Assert.Equal(TimeSpan.FromHours(8), schedule.QuietHoursEndLocal);
        Assert.True(schedule.IsActive);
    }

    [Fact]
    public async Task The_care_page_shortcut_refuses_someone_elses_pet()
    {
        var otherPet = TestData.AddPet(_db, TestData.AddUser(_db));

        var created = await new CreateCheckupReminderHandler(_db).HandleAsync(new CreateCheckupReminderCommand(_user.Id, otherPet.Id));

        Assert.False(created);
        using var verify = _database.CreateContext();
        Assert.Empty(verify.ReminderSchedules);
    }

    [Fact]
    public async Task Reminders_use_the_family_market_time_zone()
    {
        await Create(Command(title: "Colombia"));
        using (AppTimeZones.UseMarket(BusinessMarket.UnitedStates))
            await new CreateCheckupReminderHandler(_db).HandleAsync(new CreateCheckupReminderCommand(_user.Id, _pet.Id));

        using var verify = _database.CreateContext();
        Assert.Equal(AppTimeZones.ZoneFor(BusinessMarket.Colombia).Id, verify.ReminderSchedules.Single(r => r.Title == "Colombia").TimeZoneId);
        Assert.Equal(AppTimeZones.ZoneFor(BusinessMarket.UnitedStates).Id, verify.ReminderSchedules.Single(r => r.Title != "Colombia").TimeZoneId);
    }

    [Fact]
    public async Task A_colombian_reminder_due_during_bogota_quiet_hours_is_held_back()
    {
        var bogotaNow = AppTimeZones.NowLocal(BusinessMarket.Colombia).TimeOfDay;
        static string At(TimeSpan t) =>
            TimeSpan.FromMinutes(((int)t.TotalMinutes % 1440 + 1440) % 1440).ToString(@"hh\:mm");
        await Create(Command(
            nextDue: AppTimeZones.TodayLocalDate(),
            quietStart: At(bogotaNow - TimeSpan.FromMinutes(20)),
            quietEnd: At(bogotaNow + TimeSpan.FromMinutes(20))));
        var schedule = _db.ReminderSchedules.Single();
        schedule.NextDueUtc = DateTime.UtcNow.AddMinutes(-1);
        _db.SaveChanges();

        await new ReminderEngineService(_db, NullLogger<ReminderEngineService>.Instance).ProcessDueRemindersAsync();

        using var verify = _database.CreateContext();
        Assert.Empty(verify.Notifications);
        Assert.Equal(ReminderDeliveryStatus.SuppressedQuietHours, verify.ReminderDeliveries.Single().Status);
    }

    [Fact]
    public async Task A_due_reminder_becomes_a_linked_notification_and_repeats()
    {
        await Create(Command(nextDue: AppTimeZones.TodayLocalDate()));
        var schedule = _db.ReminderSchedules.Single();
        schedule.NextDueUtc = DateTime.UtcNow.AddMinutes(-1);
        _db.SaveChanges();

        var processed = await new ReminderEngineService(_db, NullLogger<ReminderEngineService>.Instance).ProcessDueRemindersAsync();

        Assert.Equal(1, processed);
        using var verify = _database.CreateContext();
        var notification = verify.Notifications.Single(n => n.UserId == _user.Id);
        Assert.Equal("Antipulgas", notification.Title);
        Assert.Equal("reminder-medication", notification.Type);
        Assert.Equal(notification.Id, verify.ReminderDeliveries.Single().AppNotificationId);
        var sent = verify.ReminderSchedules.Single();
        Assert.True(sent.IsActive);
        Assert.True(sent.NextDueUtc > DateTime.UtcNow.AddDays(29));
    }
}
