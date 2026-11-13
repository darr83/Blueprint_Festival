using BlueprintFestival.Simulation.Core;

namespace BlueprintFestival.Simulation.Tests;

public class DaylightTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 0)]
    [InlineData(8, 1)]
    [InlineData(13, 1)]
    [InlineData(19.5, 1)]
    [InlineData(22.5, 0)]
    public void Day_is_light_and_night_is_dark(double hour, double expected)
    {
        Assert.Equal(expected, Daylight.At(TimeSpan.FromHours(hour)), 3);
    }

    [Fact]
    public void Dawn_brightens_and_dusk_darkens_steadily()
    {
        var last = -1.0;
        for (var h = Daylight.DawnStarts; h <= Daylight.DawnEnds; h += 0.05)
        {
            var light = Daylight.At(TimeSpan.FromHours(h));
            Assert.True(light >= last);
            last = light;
        }

        last = 2.0;
        for (var h = Daylight.DuskStarts; h <= Daylight.DuskEnds; h += 0.05)
        {
            var light = Daylight.At(TimeSpan.FromHours(h));
            Assert.True(light <= last);
            last = light;
        }
    }

    [Fact]
    public void Light_always_stays_between_zero_and_one()
    {
        for (var minute = 0; minute < 24 * 60; minute++)
        {
            Assert.InRange(Daylight.At(TimeSpan.FromMinutes(minute)), 0, 1);
            Assert.InRange(Daylight.GoldenHour(TimeSpan.FromMinutes(minute)), 0, 1);
        }
    }

    [Fact]
    public void The_sun_is_golden_at_dusk_but_not_at_noon_or_midnight()
    {
        Assert.Equal(1, Daylight.GoldenHour(TimeSpan.FromHours(21)), 3);
        Assert.Equal(0, Daylight.GoldenHour(TimeSpan.FromHours(12)), 3);
        Assert.Equal(0, Daylight.GoldenHour(TimeSpan.FromHours(1)), 3);
    }

    [Fact]
    public void A_headline_set_at_eleven_at_night_is_in_the_dark()
    {
        Assert.True(Daylight.IsDark(TimeSpan.FromHours(23)));
        Assert.False(Daylight.IsDark(TimeSpan.FromHours(15)));
    }
}
