using Microsoft.EntityFrameworkCore;
using WebAppPet.Application.Businesses.GetAvailability;
using WebAppPet.Application.Businesses.SaveWeeklySchedule;
using WebAppPet.Application.Businesses.Shared;
using WebAppPet.Application.Businesses.ToggleAvailabilityDay;
using WebAppPet.Data;
using WebAppPet.Localization;
using WebAppPet.Models;
using WebAppPet.Services;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Businesses;

public class AvailabilityTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly AppDbContext _db;
    private readonly GroomerProfile _business;

    public AvailabilityTests()
    {
        _db = _database.CreateContext();
        _business = TestData.AddBusiness(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private static List<WeekDayInput> WeekOpen(params int[] openDays) =>
        Enumerable.Range(0, 7).Select(d => new WeekDayInput
        {
            DayOfWeek = d,
            IsOpen = openDays.Contains(d),
            OpenTime = "09:00",
            CloseTime = "17:30"
        }).ToList();

    private List<BusinessDayAvailability> Days()
    {
        using var db = _database.CreateContext();
        return db.DayAvailabilities.AsNoTracking()
            .Where(d => d.GroomerId == _business.Id)
            .OrderBy(d => d.Day)
            .ToList();
    }

    private BusinessDayAvailability AddDay(DateTime day, bool available, int? businessId = null)
    {
        var row = new BusinessDayAvailability { GroomerId = businessId ?? _business.Id, Day = day, IsAvailable = available };
        _db.DayAvailabilities.Add(row);
        _db.SaveChanges();
        return row;
    }

    [Theory]
    [InlineData(600, 540, 1080, true)]
    [InlineData(1080, 540, 1080, false)]
    [InlineData(500, 540, 1080, false)]
    [InlineData(1400, 1320, 360, true)]
    [InlineData(300, 1320, 360, true)]
    [InlineData(700, 1320, 360, false)]
    [InlineData(700, 0, 0, true)]
    public void Open_window_supports_overnight_and_24h(int now, int open, int close, bool expected) =>
        Assert.Equal(expected, AvailabilityService.IsWithinOpenWindow(now, open, close));

    [Fact]
    public async Task Saving_week_replaces_hours_and_builds_60_day_agenda_from_today()
    {
        var result = await new SaveWeeklyScheduleHandler(new AvailabilityService(_database.CreateContext()))
            .HandleAsync(new SaveWeeklyScheduleCommand(_business.Id, WeekOpen(1, 2, 3, 4, 5)));

        Assert.True(result.Success);
        using var db = _database.CreateContext();
        var hours = db.WeeklyHours.AsNoTracking().Where(h => h.GroomerId == _business.Id).ToList();
        Assert.Equal(7, hours.Count);
        Assert.All(hours.Where(h => h.IsOpen), h => Assert.Equal((540, 1050), (h.OpenMinutes, h.CloseMinutes)));

        var days = Days();
        Assert.Equal(SaveWeeklyScheduleHandler.AgendaDays, days.Count);
        Assert.Equal(AppTimeZones.TodayLocalDate(), days[0].Day);
        Assert.All(days, d => Assert.Equal(d.Day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday), d.IsAvailable));
        Assert.Equal("09:00–17:30", days.First(d => d.IsAvailable).Note);
    }

    [Fact]
    public async Task Saving_week_overwrites_manual_day_overrides()
    {
        var today = AppTimeZones.TodayLocalDate();
        AddDay(today, available: false);

        await new SaveWeeklyScheduleHandler(new AvailabilityService(_database.CreateContext()))
            .HandleAsync(new SaveWeeklyScheduleCommand(_business.Id, WeekOpen(0, 1, 2, 3, 4, 5, 6)));

        Assert.True(Days().Single(d => d.Day == today).IsAvailable);
    }

    [Fact]
    public async Task Week_with_no_open_day_is_rejected_and_nothing_changes()
    {
        var result = await new SaveWeeklyScheduleHandler(new AvailabilityService(_database.CreateContext()))
            .HandleAsync(new SaveWeeklyScheduleCommand(_business.Id, WeekOpen()));

        Assert.False(result.Success);
        Assert.Equal(CatalogLocalizer.Loc("Debes tener al menos un día abierto.", "Open at least one day in your schedule."), result.Error);
        using var db = _database.CreateContext();
        Assert.Empty(db.WeeklyHours.AsNoTracking());
        Assert.Empty(Days());
    }

    [Fact]
    public async Task Toggle_flips_own_day_with_manual_note()
    {
        var day = AddDay(AppTimeZones.TodayLocalDate(), available: true);
        var handler = new ToggleAvailabilityDayHandler(_database.CreateContext());

        Assert.True(await handler.HandleAsync(new ToggleAvailabilityDayCommand(_business.Id, day.Id)));

        var saved = Days().Single();
        Assert.False(saved.IsAvailable);
        Assert.Equal(CatalogLocalizer.Loc("Cerrado (manual)", "Closed (manual)"), saved.Note);
    }

    [Fact]
    public async Task Toggle_ignores_days_of_other_businesses()
    {
        var other = TestData.AddBusiness(_db);
        var day = AddDay(AppTimeZones.TodayLocalDate(), available: true, businessId: other.Id);

        Assert.False(await new ToggleAvailabilityDayHandler(_database.CreateContext())
            .HandleAsync(new ToggleAvailabilityDayCommand(_business.Id, day.Id)));

        using var db = _database.CreateContext();
        Assert.True(db.DayAvailabilities.AsNoTracking().Single(d => d.Id == day.Id).IsAvailable);
    }

    [Fact]
    public async Task Day_override_wins_over_weekly_hours_and_no_schedule_means_closed()
    {
        var service = new AvailabilityService(_database.CreateContext());
        var monday = NextDay(DayOfWeek.Monday);

        Assert.False(await service.IsAvailableOnAsync(_business.Id, monday));

        _db.WeeklyHours.Add(new BusinessWeeklyHour { GroomerId = _business.Id, DayOfWeek = (int)DayOfWeek.Monday, IsOpen = true });
        _db.SaveChanges();
        Assert.True(await service.IsAvailableOnAsync(_business.Id, monday));

        AddDay(monday, available: false);
        Assert.False(await service.IsAvailableOnAsync(_business.Id, monday.AddHours(15)));
    }

    [Fact]
    public async Task View_merges_saved_hours_into_default_week_and_lists_next_30_days()
    {
        _db.WeeklyHours.Add(new BusinessWeeklyHour
        {
            GroomerId = _business.Id, DayOfWeek = 0, IsOpen = true, OpenMinutes = 600, CloseMinutes = 840
        });
        var today = AppTimeZones.TodayLocalDate();
        AddDay(today.AddDays(-1), available: true);
        AddDay(today, available: true);
        AddDay(today.AddDays(GetAvailabilityHandler.AgendaDaysShown), available: true);

        var view = await new GetAvailabilityHandler(_database.CreateContext())
            .HandleAsync(new GetAvailabilityQuery(_business.Id));

        Assert.Equal(7, view.Week.Count);
        Assert.Equal((true, "10:00", "14:00"), (view.Week[0].IsOpen, view.Week[0].OpenTime, view.Week[0].CloseTime));
        Assert.Equal((true, "08:00"), (view.Week[1].IsOpen, view.Week[1].OpenTime));
        Assert.Equal([today], view.Days.Select(d => d.Day));
    }

    private static DateTime NextDay(DayOfWeek dow)
    {
        var day = AppTimeZones.TodayLocalDate().AddDays(1);
        while (day.DayOfWeek != dow) day = day.AddDays(1);
        return day;
    }
}
