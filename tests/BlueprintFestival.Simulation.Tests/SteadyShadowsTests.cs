using BlueprintFestival.Simulation.Core;

namespace BlueprintFestival.Simulation.Tests;

public class SteadyShadowsTests
{
    private static SkyLight Sun(TimeSpan time) => SteadyShadows.Stepped(SunPath.At(time));

    [Fact]
    public void The_sun_holds_still_between_steps_instead_of_turning_every_frame()
    {
        // A frame at normal speed is about an eighth of a game second; over ten game seconds the sun must not move at all.
        var start = new TimeSpan(15, 0, 20);
        var first = Sun(start);
        for (var tick = 1; tick <= 80; tick++)
        {
            Assert.Equal(first, Sun(start + TimeSpan.FromSeconds(tick / 8.0)));
        }
    }

    [Fact]
    public void The_sun_still_goes_round_through_the_day_in_small_steps()
    {
        var changes = 0;
        var before = Sun(TimeSpan.FromHours(6));
        for (var minute = 6 * 60 + 1; minute <= 20 * 60; minute++)
        {
            var now = Sun(TimeSpan.FromMinutes(minute));
            Assert.True(Math.Abs(now.Bearing - before.Bearing) <= SteadyShadows.SunStepDegrees + 1e-9, $"Bearing jumped at minute {minute}.");
            Assert.True(Math.Abs(now.Height - before.Height) <= SteadyShadows.SunStepDegrees + 1e-9, $"Height jumped at minute {minute}.");
            if (now != before)
            {
                changes++;
            }

            before = now;
        }

        // Fourteen hours of sun: it should move often enough to look like it travels, not every frame.
        Assert.InRange(changes, 500, 14 * 60);
    }

    [Fact]
    public void The_stepped_sun_stays_close_to_the_real_one()
    {
        for (var h = 5.0; h < 21; h += 0.1)
        {
            var real = SunPath.At(TimeSpan.FromHours(h));
            var stepped = SteadyShadows.Stepped(real);
            Assert.InRange(stepped.Bearing - real.Bearing, -0.125 - 1e-9, 0.125 + 1e-9);
            Assert.InRange(stepped.Height - real.Height, -0.125 - 1e-9, 0.125 + 1e-9);
            Assert.Equal(real.Strength, stepped.Strength);
            Assert.Equal(real.Moon, stepped.Moon);
        }
    }

    [Fact]
    public void The_shadowed_ground_only_changes_size_in_whole_steps_while_zooming()
    {
        // A zoom eases in over many frames; the shadowed patch should change a handful of times, not every frame.
        var sizes = Enumerable.Range(0, 200).Select(i => 60.0 + (i * 0.05)).ToList();
        var reaches = sizes.Select(s => SteadyShadows.Reach(s, 30)).Distinct().ToList();
        Assert.InRange(reaches.Count, 1, 3);
        Assert.All(reaches, r => Assert.Equal(0, r % SteadyShadows.ReachStepMetres));
    }

    [Fact]
    public void The_shadowed_ground_always_covers_what_is_on_screen()
    {
        foreach (var size in new[] { 8.0, 30, 60, 120, 260 })
        {
            var onScreen = (size * 0.5 / Math.Tan(Math.PI / 6)) + 40;
            var reach = SteadyShadows.Reach(size, 30);
            Assert.InRange(reach, onScreen, onScreen + SteadyShadows.ReachStepMetres);
        }
    }
}
