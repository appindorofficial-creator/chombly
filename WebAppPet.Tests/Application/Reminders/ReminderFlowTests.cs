using Microsoft.Extensions.Logging.Abstractions;
using WebAppPet.Application.Reminders.CreateCheckupReminder;
using WebAppPet.Application.Reminders.CreateReminder;
using WebAppPet.Application.Reminders.DeactivateReminder;
using WebAppPet.Application.Reminders.GetPetReminders;
using WebAppPet.Application.Reminders.Shared;
using WebAppPet.Domain;
using WebAppPet.Domain.Markets;
using WebAppPet.Infrastructure.Persistence;
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
        int? petId = null,
        string? dueTime = null) =>
        new(_user.Id, petId ?? _pet.Id, type, title, "Con comida", frequencyDays, nextDue ?? _nextWeek, quietStart, quietEnd, dueTime);

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
    [InlineData("16:00", 16, 0)]
    [InlineData("4:00 PM", 16, 0)]
    [InlineData("07:45", 7, 45)]
    public async Task Fires_at_the_chosen_time(string dueTime, int hours, int minutes)
    {
        var outcome = await Create(Command(dueTime: dueTime, quietStart: "21:00", quietEnd: "07:00"));

        Assert.Equal(CreateReminderOutcome.Created, outcome);
        Assert.Equal(
            AppTimeZones.LocalDateAndTimeToUtc(_nextWeek, new TimeSpan(hours, minutes, 0)),
            SingleSchedule().NextDueUtc);
    }

    [Fact]
    public async Task Rejects_a_time_that_cannot_be_read()
    {
        var outcome = await Create(Command(dueTime: "25:99"));

        Assert.Equal(CreateReminderOutcome.InvalidTime, outcome);
        Assert.Empty(_db.ReminderSchedules);
    }

    [Fact]
    public async Task Rejects_a_time_that_already_passed_today()
    {
        var outcome = await Create(Command(nextDue: AppTimeZones.TodayLocalDate(), dueTime: "00:00"));

        Assert.Equal(CreateReminderOutcome.PastTime, outcome);
        Assert.Empty(_db.ReminderSchedules);
    }

    [Fact]
    public async Task Midnight_is_fine_on_a_later_day()
    {
        var tomorrow = AppTimeZones.TodayLocalDate().AddDays(1);

        var outcome = await Create(Command(nextDue: tomorrow, dueTime: "00:00"));

        Assert.Equal(CreateReminderOutcome.Created, outcome);
        Assert.Equal(AppTimeZones.LocalDateAndTimeToUtc(tomorrow, TimeSpan.Zero), SingleSchedule().NextDueUtc);
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
    public async Task Do_not_disturb_accepts_twelve_hour_times()
    {
        await Create(Command(quietStart: "9:30 PM", quietEnd: "7:00 AM"));

        var schedule = SingleSchedule();
        Assert.Equal(new TimeSpan(21, 30, 0), schedule.QuietHoursStartLocal);
        Assert.Equal(new TimeSpan(7, 0, 0), schedule.QuietHoursEndLocal);
    }

    [Fact]
    public async Task Do_not_disturb_is_optional()
    {
        var outcome = await Create(Command(quietStart: " ", quietEnd: ""));

        Assert.Equal(CreateReminderOutcome.Created, outcome);
        Assert.Null(SingleSchedule().QuietHoursStartLocal);
        Assert.Null(SingleSchedule().QuietHoursEndLocal);
    }

    [Theory]
    [InlineData("22:00", null)]
    [InlineData(null, "07:00")]
    [InlineData("22:00", "later")]
    public async Task Do_not_disturb_needs_both_times(string? start, string? end)
    {
        var outcome = await Create(Command(quietStart: start, quietEnd: end));

        Assert.Equal(CreateReminderOutcome.IncompleteQuietHours, outcome);
        using var verify = _database.CreateContext();
        Assert.Empty(verify.ReminderSchedules);
    }

    [Fact]
    public async Task Do_not_disturb_rejects_the_same_start_and_end()
    {
        var outcome = await Create(Command(quietStart: "22:00", quietEnd: "10:00 PM"));

        Assert.Equal(CreateReminderOutcome.SameQuietHours, outcome);
    }

    [Fact]
    public async Task Today_is_still_a_valid_date_for_a_later_time()
    {
        var later = AppTimeZones.NowLocal().AddMinutes(5);

        var outcome = await Create(Command(nextDue: later.Date, dueTime: later.ToString("HH:mm")));

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

        Assert.Equal(CreateCheckupReminderOutcome.Created, created);
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
    public async Task The_care_page_shortcut_saves_spanish_even_when_the_screen_is_in_english()
    {
        var previous = System.Globalization.CultureInfo.CurrentUICulture;
        System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("en");
        try
        {
            await new CreateCheckupReminderHandler(_db).HandleAsync(new CreateCheckupReminderCommand(_user.Id, _pet.Id));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = previous;
        }

        var schedule = SingleSchedule();
        Assert.Equal($"Vacunas / chequeo · {_pet.Name}", schedule.Title);
        Assert.Equal("Aviso creado desde Control.", schedule.Notes);
    }

    [Fact]
    public async Task Tapping_the_care_page_shortcut_again_does_not_stack_reminders()
    {
        var handler = new CreateCheckupReminderHandler(_db);

        Assert.Equal(CreateCheckupReminderOutcome.Created,
            await handler.HandleAsync(new CreateCheckupReminderCommand(_user.Id, _pet.Id)));
        Assert.Equal(CreateCheckupReminderOutcome.AlreadyActive,
            await handler.HandleAsync(new CreateCheckupReminderCommand(_user.Id, _pet.Id)));

        SingleSchedule();
    }

    [Fact]
    public async Task An_old_english_checkup_reminder_also_counts_as_already_active()
    {
        _db.ReminderSchedules.Add(new ReminderSchedule
        {
            UserId = _user.Id,
            PetId = _pet.Id,
            Type = ReminderType.Vaccine,
            Title = $"Vaccines / checkup · {_pet.Name}",
            FrequencyDays = 365,
            NextDueUtc = DateTime.UtcNow.AddDays(30)
        });
        _db.SaveChanges();

        var outcome = await new CreateCheckupReminderHandler(_db).HandleAsync(new CreateCheckupReminderCommand(_user.Id, _pet.Id));

        Assert.Equal(CreateCheckupReminderOutcome.AlreadyActive, outcome);
    }

    [Fact]
    public async Task A_deactivated_checkup_reminder_can_be_created_again()
    {
        var handler = new CreateCheckupReminderHandler(_db);
        await handler.HandleAsync(new CreateCheckupReminderCommand(_user.Id, _pet.Id));
        _db.ReminderSchedules.Single().IsActive = false;
        _db.SaveChanges();

        var outcome = await handler.HandleAsync(new CreateCheckupReminderCommand(_user.Id, _pet.Id));

        Assert.Equal(CreateCheckupReminderOutcome.Created, outcome);
    }

    [Fact]
    public async Task The_care_page_shortcut_refuses_someone_elses_pet()
    {
        var otherPet = TestData.AddPet(_db, TestData.AddUser(_db));

        var created = await new CreateCheckupReminderHandler(_db).HandleAsync(new CreateCheckupReminderCommand(_user.Id, otherPet.Id));

        Assert.Equal(CreateCheckupReminderOutcome.PetNotFound, created);
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
        await Create(Command());
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

    [Fact]
    public async Task A_repeating_reminder_keeps_its_planned_time_when_the_job_runs_late()
    {
        await Create(Command());
        var schedule = _db.ReminderSchedules.Single();
        var planned = DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(-3), DateTimeKind.Utc);
        planned = planned.AddTicks(-(planned.Ticks % TimeSpan.TicksPerMinute));
        schedule.NextDueUtc = planned;
        _db.SaveChanges();

        await new ReminderEngineService(_db, NullLogger<ReminderEngineService>.Instance).ProcessDueRemindersAsync();

        using var verify = _database.CreateContext();
        Assert.Equal(planned.AddDays(30), verify.ReminderSchedules.Single().NextDueUtc);
    }
}
