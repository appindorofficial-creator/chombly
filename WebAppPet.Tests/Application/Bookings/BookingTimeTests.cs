using WebAppPet.Application.Bookings.Shared;

namespace WebAppPet.Tests.Application.Bookings;

public class BookingTimeTests
{
    [Fact]
    public void Slots_start_at_opening_and_end_with_the_last_hour_before_closing()
    {
        var slots = BookingTime.OpenWindowSlots(8 * 60, 18 * 60);

        Assert.Equal(
            new[] { "8:00 AM", "9:00 AM", "10:00 AM", "11:00 AM", "12:00 PM", "1:00 PM", "2:00 PM", "3:00 PM", "4:00 PM", "5:00 PM" },
            slots);
    }

    [Fact]
    public void Slots_follow_a_half_hour_opening()
    {
        var slots = BookingTime.OpenWindowSlots(7 * 60 + 30, 11 * 60);

        Assert.Equal(new[] { "7:30 AM", "8:30 AM", "9:30 AM", "10:30 AM" }, slots);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(22 * 60, 6 * 60)]
    public void Full_day_and_overnight_windows_keep_the_standard_slots(int open, int close)
    {
        Assert.Equal(BookingTime.StandardDaySlots, BookingTime.OpenWindowSlots(open, close));
    }

    [Theory]
    [InlineData("17:00", "5:00 PM")]
    [InlineData("5:00 PM", "5:00 PM")]
    [InlineData("08:00", "8:00 AM")]
    [InlineData("19:00", null)]
    [InlineData("", null)]
    public void A_requested_time_matches_its_slot_in_either_format(string time, string? expected)
    {
        var slots = BookingTime.OpenWindowSlots(8 * 60, 18 * 60);

        Assert.Equal(expected, BookingTime.MatchSlot(time, slots));
    }
}
