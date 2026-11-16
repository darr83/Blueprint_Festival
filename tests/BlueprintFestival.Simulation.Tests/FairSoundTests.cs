using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.Sound;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

/// <summary>The fairground is only heard near where the player is looking, not all over the site.</summary>
public class FairSoundTests
{
    private static readonly DateTime Day = GameClock.DefaultStart.Date;

    /// <summary>A village day with a carousel in one corner, run until it is going round with people on it.</summary>
    private static (FestivalGame Game, PlacedStall Carousel) RideGoing()
    {
        var game = new FestivalGame("Fair", seed: 8);
        game.SetWeather(DayWeather.Of(WeatherKind.Fine, 3));
        var f = game.Field.Field;
        Assert.True(game.BuildStall(StallKind.Bar, f.Right - 16, f.Y + 20, Facing.West).Ok);
        Assert.True(game.BuildStall(StallKind.Toilets, f.Right - 12, f.Y + 36, Facing.West).Ok);
        Assert.True(game.BuildStall(StallKind.Carousel, f.X + 4, f.Y + 4, Facing.South).Ok);
        Assert.True(game.BuildStall(StallKind.HookADuck, f.X + 4, f.Y + 40, Facing.East).Ok);
        var carousel = game.Site.Stalls.Count - 2;
        var until = Day.AddHours(17);
        while (game.Now < until)
        {
            game.Clock.Step();
            if (game.Crowd.Riding(carousel) > 0 && !Rides.TurnAt(game.Site.Stalls[carousel], game.Now).Boarding)
            {
                return (game, game.Site.Stalls[carousel]);
            }
        }

        Assert.Fail("The carousel never went round with anyone on it.");
        return default;
    }

    [Fact]
    public void A_ride_is_loud_close_to_and_fades_away_with_distance()
    {
        var close = StageMix.Fair(0, 0, 30, 0, 0);
        var near = StageMix.Fair(0, 0, 30, 15, 0);
        var far = StageMix.Fair(0, 0, 30, 60, 0);
        Assert.True(close > 0.3, $"Looking straight at it: {close}");
        Assert.True(near < close);
        Assert.True(far < near);
        Assert.Equal(0, StageMix.Fair(0, 0, 30, 150, 0));
    }

    [Fact]
    public void A_ride_is_quieter_zoomed_out_and_silent_zoomed_right_out()
    {
        Assert.True(StageMix.Fair(0, 0, StageMix.ClosestView, 0, 0) > StageMix.Fair(0, 0, 60, 0, 0));
        Assert.True(StageMix.Fair(0, 0, 60, 0, 0) > StageMix.Fair(0, 0, 150, 0, 0));
        Assert.Equal(0, StageMix.Fair(0, 0, StageMix.FurthestView, 0, 0));
    }

    [Fact]
    public void The_fairground_is_heard_looking_at_it_and_not_from_the_other_side_of_the_site()
    {
        var (game, carousel) = RideGoing();
        using var script = new SoundScript(game);
        var (cx, cy) = SoundScript.MiddleOf(carousel);

        script.Listen(cx, cy, 30, filming: false);
        script.Poll();
        var looking = script.Levels[Sounds.Fairground];
        Assert.True(looking > 0.3, $"Looking at the carousel: {looking}");

        var f = game.Field.Field;
        script.Listen(f.Right * FieldMap.TileMetres, f.Bottom * FieldMap.TileMetres, 30, filming: false);
        script.Poll();
        Assert.Equal(0, script.Levels[Sounds.Fairground]);

        script.Listen(cx, cy, StageMix.FurthestView, filming: false);
        script.Poll();
        Assert.Equal(0, script.Levels[Sounds.Fairground]);

        script.Listen(cx, cy, 30, filming: true);
        script.Poll();
        Assert.Equal(0, script.Levels[Sounds.Fairground]);
    }

    [Fact]
    public void Ride_bells_and_ducks_are_not_heard_from_far_away()
    {
        var (game, _) = RideGoing();
        using var script = new SoundScript(game);
        var heard = new List<string>();
        script.Cue += heard.Add;
        var f = game.Field.Field;
        script.Listen(f.Right * FieldMap.TileMetres, f.Bottom * FieldMap.TileMetres, 30, filming: false);
        var until = game.Now.AddHours(1);
        while (game.Now < until)
        {
            game.Clock.Step();
        }

        Assert.DoesNotContain(Sounds.RideBell, heard);
        Assert.DoesNotContain(Sounds.HookADuck, heard);
        Assert.DoesNotContain(Sounds.TeddyWin, heard);

        // Looking at the fair, they are.
        heard.Clear();
        var (cx, cy) = SoundScript.MiddleOf(game.Site.Stalls.First(s => s.Kind == StallKind.HookADuck));
        script.Listen(cx, cy, 30, filming: false);
        until = game.Now.AddHours(1);
        while (game.Now < until)
        {
            game.Clock.Step();
        }

        Assert.True(heard.Contains(Sounds.RideBell) || heard.Contains(Sounds.HookADuck), "Nothing heard looking at the fair.");
    }
}
