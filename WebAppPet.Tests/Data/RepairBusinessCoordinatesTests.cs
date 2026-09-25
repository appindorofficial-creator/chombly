using WebAppPet.Data;
using WebAppPet.Tests.Support;

namespace WebAppPet.Tests.Data;

public class RepairBusinessCoordinatesTests
{
    [Fact]
    public async Task Fixes_only_businesses_whose_coordinates_are_out_of_range()
    {
        using var database = new TestDatabase();
        int neivaId, charlotteId, unsetId;
        using (var db = database.CreateContext())
        {
            var neiva = TestData.AddBusiness(db);
            neiva.Latitude = 2925704;
            neiva.Longitude = -75289394;
            var charlotte = TestData.AddBusiness(db);
            charlotte.Latitude = 35.2271;
            charlotte.Longitude = -80.8431;
            var unset = TestData.AddBusiness(db);
            db.SaveChanges();
            (neivaId, charlotteId, unsetId) = (neiva.Id, charlotte.Id, unset.Id);
        }

        using (var db = database.CreateContext())
            Assert.Equal(1, await DbInitializer.RepairBusinessCoordinatesAsync(db));

        using var check = database.CreateContext();
        var fixedNeiva = check.Groomers.Single(g => g.Id == neivaId);
        Assert.Equal(2.925704, fixedNeiva.Latitude, 6);
        Assert.Equal(-75.289394, fixedNeiva.Longitude, 6);
        var untouched = check.Groomers.Single(g => g.Id == charlotteId);
        Assert.Equal(35.2271, untouched.Latitude);
        Assert.Equal(-80.8431, untouched.Longitude);
        var withoutLocation = check.Groomers.Single(g => g.Id == unsetId);
        Assert.Equal(0, withoutLocation.Latitude);
        Assert.Equal(0, withoutLocation.Longitude);
    }

    [Fact]
    public async Task Is_a_no_op_once_everything_is_valid()
    {
        using var database = new TestDatabase();
        using (var db = database.CreateContext())
        {
            var business = TestData.AddBusiness(db);
            business.Latitude = 4711000;
            business.Longitude = -74072100;
            db.SaveChanges();
        }

        using (var db = database.CreateContext())
            Assert.Equal(1, await DbInitializer.RepairBusinessCoordinatesAsync(db));
        using (var db = database.CreateContext())
            Assert.Equal(0, await DbInitializer.RepairBusinessCoordinatesAsync(db));
    }
}
