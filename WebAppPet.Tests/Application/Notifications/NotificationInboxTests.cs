using WebAppPet.Application.Notifications.ClearNotifications;
using WebAppPet.Application.Notifications.DeleteNotification;
using WebAppPet.Application.Notifications.GetUnreadCount;
using WebAppPet.Application.Notifications.OpenInbox;
using WebAppPet.Domain;
using WebAppPet.Infrastructure.Persistence;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Application.Notifications;

public class NotificationInboxTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly AppDbContext _db;
    private readonly AppUser _user;
    private readonly AppUser _other;

    public NotificationInboxTests()
    {
        _db = _database.CreateContext();
        _user = TestData.AddUser(_db);
        _other = TestData.AddUser(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    private AppNotification AddNotification(AppUser user, string title, bool isRead = false, int minutesAgo = 0)
    {
        var notification = new AppNotification
        {
            UserId = user.Id,
            Title = title,
            Message = title,
            IsRead = isRead,
            CreatedAt = DateTime.UtcNow.AddMinutes(-minutesAgo)
        };
        _db.Notifications.Add(notification);
        _db.SaveChanges();
        return notification;
    }

    private ReminderDelivery AddDeliveryFor(AppNotification notification)
    {
        var pet = TestData.AddPet(_db, _user);
        var schedule = new ReminderSchedule { UserId = _user.Id, PetId = pet.Id, Title = "Vacuna", NextDueUtc = DateTime.UtcNow };
        _db.ReminderSchedules.Add(schedule);
        _db.SaveChanges();
        var delivery = new ReminderDelivery
        {
            ReminderScheduleId = schedule.Id,
            ScheduledForUtc = DateTime.UtcNow,
            Status = ReminderDeliveryStatus.Sent,
            Message = notification.Message,
            AppNotificationId = notification.Id
        };
        _db.ReminderDeliveries.Add(delivery);
        _db.SaveChanges();
        return delivery;
    }

    [Fact]
    public async Task Opening_the_inbox_lists_newest_first_and_keeps_unread_styling_for_this_visit()
    {
        AddNotification(_user, "old", isRead: true, minutesAgo: 30);
        AddNotification(_user, "new", minutesAgo: 1);
        AddNotification(_other, "someone else");

        var items = await new OpenInboxHandler(_db).HandleAsync(new OpenInboxCommand(_user.Id));

        Assert.Equal(["new", "old"], items.Select(n => n.Title));
        Assert.False(items[0].IsRead);
    }

    [Fact]
    public async Task Opening_the_inbox_marks_only_the_users_notifications_read()
    {
        AddNotification(_user, "a");
        AddNotification(_user, "b");
        var foreign = AddNotification(_other, "someone else");

        await new OpenInboxHandler(_db).HandleAsync(new OpenInboxCommand(_user.Id));

        using var verify = _database.CreateContext();
        Assert.All(verify.Notifications.Where(n => n.UserId == _user.Id), n => Assert.True(n.IsRead));
        Assert.False(verify.Notifications.Single(n => n.Id == foreign.Id).IsRead);
    }

    [Fact]
    public async Task Unread_count_ignores_read_and_other_users_notifications()
    {
        AddNotification(_user, "a");
        AddNotification(_user, "b");
        AddNotification(_user, "seen", isRead: true);
        AddNotification(_other, "someone else");

        var count = await new GetUnreadCountHandler(_db).HandleAsync(new GetUnreadCountQuery(_user.Id));

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Deleting_a_notification_keeps_the_reminder_delivery_history()
    {
        var notification = AddNotification(_user, "Vacuna");
        var delivery = AddDeliveryFor(notification);

        var deleted = await new DeleteNotificationHandler(_db)
            .HandleAsync(new DeleteNotificationCommand(_user.Id, notification.Id));

        Assert.True(deleted);
        using var verify = _database.CreateContext();
        Assert.False(verify.Notifications.Any(n => n.Id == notification.Id));
        var kept = verify.ReminderDeliveries.Single(d => d.Id == delivery.Id);
        Assert.Null(kept.AppNotificationId);
    }

    [Fact]
    public async Task Cannot_delete_someone_elses_notification()
    {
        var foreign = AddNotification(_other, "someone else");

        var deleted = await new DeleteNotificationHandler(_db)
            .HandleAsync(new DeleteNotificationCommand(_user.Id, foreign.Id));

        Assert.False(deleted);
        using var verify = _database.CreateContext();
        Assert.True(verify.Notifications.Any(n => n.Id == foreign.Id));
    }

    [Fact]
    public async Task Clearing_removes_all_of_the_users_notifications_only()
    {
        var withDelivery = AddNotification(_user, "Vacuna");
        var delivery = AddDeliveryFor(withDelivery);
        AddNotification(_user, "b", isRead: true);
        var foreign = AddNotification(_other, "someone else");

        var removed = await new ClearNotificationsHandler(_db).HandleAsync(new ClearNotificationsCommand(_user.Id));

        Assert.Equal(2, removed);
        using var verify = _database.CreateContext();
        Assert.False(verify.Notifications.Any(n => n.UserId == _user.Id));
        Assert.True(verify.Notifications.Any(n => n.Id == foreign.Id));
        Assert.Null(verify.ReminderDeliveries.Single(d => d.Id == delivery.Id).AppNotificationId);
    }

    [Fact]
    public async Task Clearing_an_empty_inbox_removes_nothing()
    {
        var removed = await new ClearNotificationsHandler(_db).HandleAsync(new ClearNotificationsCommand(_user.Id));

        Assert.Equal(0, removed);
    }
}
