using BlueprintFestival.Simulation.Core;

namespace BlueprintFestival.Simulation.Tests;

public class SunPathTests
{
    private static SkyLight At(double hour) => SunPath.At(TimeSpan.FromHours(hour));

    [Fact]
    public void The_sun_is_highest_in_the_south_at_one_o_clock()
    {
        var best = Enumerable.Range(0, 96).Select(q => q / 4.0).MaxBy(h => SunPath.Height(TimeSpan.FromHours(h)));
        Assert.Equal(13, best);
        Assert.Equal(180, At(13).Bearing, 3);
        Assert.Equal(SunPath.Highest, At(13).Height, 3);
        Assert.False(At(13).Moon);
    }

    [Fact]
    public void The_sun_is_in_the_east_in_the_morning_and_the_west_in_the_evening()
    {
        Assert.InRange(At(8).Bearing, 60, 135);
        Assert.InRange(At(19).Bearing, 225, 300);
        for (var h = 5.0; h < 21; h += 0.25)
        {
            Assert.True(At(h + 0.25).Bearing > At(h).Bearing, $"The sun should keep going round at {h}.");
        }
    }

    [Fact]
    public void Shadows_are_long_morning_and_evening_and_short_at_lunchtime()
    {
        Assert.True(At(13).ShadowLength < 0.7);
        Assert.True(At(8).ShadowLength > At(10).ShadowLength);
        Assert.True(At(10).ShadowLength > At(13).ShadowLength);
        Assert.True(At(20).ShadowLength > At(17).ShadowLength);
        Assert.True(At(17).ShadowLength > At(13).ShadowLength);
    }

    [Fact]
    public void Shadows_never_run_right_across_the_field()
    {
        for (var h = 0.0; h < 24; h += 0.1)
        {
            Assert.True(At(h).Height >= SunPath.LowestDrawn, $"The light is too low at {h}.");
            Assert.True(At(h).ShadowLength < 5.2);
            Assert.InRange(At(h).Strength, 0, 1);
        }
    }

    [Fact]
    public void The_festival_day_is_in_full_sun_and_the_headline_set_ends_under_the_moon()
    {
        for (var h = 8.0; h <= 19.5; h += 0.5)
        {
            Assert.False(At(h).Moon);
            Assert.Equal(1, At(h).Strength, 3);
        }

        Assert.True(At(22.5).Moon);
        Assert.Equal(1, At(22.5).Strength, 3);
        Assert.True(At(2).Moon);
        Assert.Equal(SunPath.MoonHeight, At(2).Height);
    }

    [Fact]
    public void The_sun_fades_out_before_the_moon_comes_up_so_the_light_never_jumps()
    {
        // At the moment one takes over from the other, neither has any strength.
        var last = At(20);
        for (var h = 20.0; h <= 22.5; h += 0.01)
        {
            var now = At(h);
            if (now.Moon != last.Moon)
            {
                Assert.True(last.Strength < 0.02 && now.Strength < 0.02, $"The light jumps at {h}: {last.Strength} to {now.Strength}.");
            }

            Assert.True(Math.Abs(now.Strength - last.Strength) < 0.05);
            last = now;
        }
    }

    [Fact]
    public void Evening_light_is_warm_and_midday_light_is_white()
    {
        Assert.Equal(0, SunPath.Warmth(TimeSpan.FromHours(13)));
        Assert.True(SunPath.Warmth(TimeSpan.FromHours(20.5)) > 0.6);
        Assert.True(SunPath.Warmth(TimeSpan.FromHours(20.5)) > SunPath.Warmth(TimeSpan.FromHours(18)));
        Assert.Equal(1, SunPath.Warmth(TimeSpan.FromHours(21.5)), 3);
    }
}
