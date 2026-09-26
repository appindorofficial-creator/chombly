using WebAppPet.Data;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Data;

public class RepairServiceDurationsTests
{
    [Fact]
    public async Task Fixes_session_services_whose_name_states_other_minutes()
    {
        using var database = new TestDatabase();
        int shortWalkId, hourWalkId, longWalkId, nightId;
        using (var db = database.CreateContext())
        {
            var business = TestData.AddBusiness(db);
            var shortWalk = TestData.AddService(db, business);
            shortWalk.Name = "Paseo 30 min";
            var hourWalk = TestData.AddService(db, business);
            hourWalk.Name = "Paseo 60 min";
            var longWalk = TestData.AddService(db, business);
            longWalk.Name = "Paseo largo";
            var night = TestData.AddService(db, business);
            night.Name = "Noche 30 min";
            night.BillingUnit = "noche";
            night.DurationMinutes = 1440;
            db.SaveChanges();
            (shortWalkId, hourWalkId, longWalkId, nightId) = (shortWalk.Id, hourWalk.Id, longWalk.Id, night.Id);
        }

        using (var db = database.CreateContext())
            Assert.Equal(1, await DbInitializer.RepairServiceDurationsAsync(db));

        using var check = database.CreateContext();
        Assert.Equal(30, check.Services.Single(s => s.Id == shortWalkId).DurationMinutes);
        Assert.Equal(60, check.Services.Single(s => s.Id == hourWalkId).DurationMinutes);
        Assert.Equal(60, check.Services.Single(s => s.Id == longWalkId).DurationMinutes);
        Assert.Equal(1440, check.Services.Single(s => s.Id == nightId).DurationMinutes);
    }

    [Fact]
    public async Task Is_a_no_op_once_durations_match_the_names()
    {
        using var database = new TestDatabase();
        using (var db = database.CreateContext())
        {
            var service = TestData.AddService(db, TestData.AddBusiness(db));
            service.Name = "Paseo 30 min";
            db.SaveChanges();
        }

        using (var db = database.CreateContext())
            Assert.Equal(1, await DbInitializer.RepairServiceDurationsAsync(db));
        using (var db = database.CreateContext())
            Assert.Equal(0, await DbInitializer.RepairServiceDurationsAsync(db));
    }
}
