using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;

namespace BlueprintFestival.Simulation.Tests;

/// <summary>The band playing along with its music, the gaps between songs, and the end of a set.</summary>
public class PerformingTests
{
    private static readonly DateTime Day = GameClock.DefaultStart.Date;

    [Fact]
    public void The_drummer_keeps_time_on_the_hi_hat_hits_the_snare_on_two_and_four_and_fills_every_fourth_bar()
    {
        // Bar 0 (eighths 0 to 7): a crash to start, then the hi-hat on every eighth.
        Assert.Equal(Drum.Crash, Performing.Hit(0, right: true));
        for (var e = 1; e < 8; e++)
        {
            Assert.Equal(Drum.HiHat, Performing.Hit(e, right: true));
        }

        // The snare on the second and fourth beats, with the left stick only.
        for (var e = 0; e < 8; e++)
        {
            Assert.Equal(e is 2 or 6 ? Drum.Snare : null, Performing.Hit(e, right: false));
        }

        Assert.True(Performing.Kick(0));
        Assert.True(Performing.Kick(4));
        Assert.False(Performing.Kick(2));

        // The second half of every fourth bar is a fill round the toms, one stick after the other.
        var fill = (3 * 8) + 4;
        Assert.Equal(Drum.HighTom, Performing.Hit(fill, right: true));
        Assert.Null(Performing.Hit(fill, right: false));
        Assert.Equal(Drum.HighTom, Performing.Hit(fill + 1, right: false));
        Assert.Equal(Drum.FloorTom, Performing.Hit(fill + 2, right: true));
        Assert.Equal(Drum.LowTom, Performing.Hit(fill + 3, right: false));

        // And the next bar starts on the crash again.
        Assert.Equal(Drum.Crash, Performing.Hit(32, right: true));
        Assert.Equal(Drum.HiHat, Performing.Hit(8, right: true));
    }

    [Fact]
    public void A_stick_comes_down_on_the_drum_exactly_as_it_is_hit_and_is_up_in_between()
    {
        for (var beat = 0.0; beat < 32; beat += 0.5)
        {
            var eighth = (long)Math.Round(beat * 2);
            foreach (var right in new[] { true, false })
            {
                var (lift, from, _, _) = Performing.Stick(beat, right);
                if (Performing.Hit(eighth, right) is { } drum)
                {
                    Assert.True(lift < 0.01, $"The {(right ? "right" : "left")} stick should be down at beat {beat}, not {lift:0.00} up.");
                    Assert.Equal(drum, from);
                }
            }
        }

        // The left stick has a beat between snare hits, so it is well up halfway.
        Assert.True(Performing.Stick(1.5, right: false).Lift > 0.6);
        Assert.InRange(Performing.SinceHit(1.1, Drum.Snare), 0.09, 0.11);
        Assert.InRange(Performing.SinceKick(2.25), 0.24, 0.26);
    }

    [Fact]
    public void The_strumming_hand_crosses_the_strings_going_down_on_each_beat_and_up_between()
    {
        for (var beat = 0; beat < 8; beat++)
        {
            Assert.InRange(Performing.Strum(beat), -0.001, 0.001);
            Assert.True(Performing.Strum(beat + 0.05) < 0, "Just after the beat the hand is below the strings, having come down.");
            Assert.True(Performing.Strum(beat + 0.55) > 0, "Just after halfway it is above them again, having come up.");
        }
    }

    [Fact]
    public void The_last_song_fades_over_the_last_seconds_of_a_set_at_any_speed()
    {
        Assert.Equal(1, Performing.FadeLevel(60));
        Assert.Equal(0.5, Performing.FadeLevel(Performing.FadeOutSeconds / 2), 6);
        Assert.Equal(0, Performing.FadeLevel(0));
        Assert.Equal(0, Performing.FadeLevel(-3));

        // A minute of the festival takes eight real seconds at normal speed and one at the fastest.
        Assert.Equal(8, Performing.RealSeconds(TimeSpan.FromMinutes(1), GameSpeed.Normal), 6);
        Assert.Equal(1, Performing.RealSeconds(TimeSpan.FromMinutes(1), GameSpeed.Fastest), 6);
        Assert.True(double.IsPositiveInfinity(Performing.RealSeconds(TimeSpan.FromMinutes(1), GameSpeed.Paused)));
    }

    [Fact]
    public void Most_people_stay_to_the_end_and_everyone_claps_for_one_to_three_minutes()
    {
        var stay = Enumerable.Range(0, 1000).Count(Performing.StaysToTheEnd);
        Assert.InRange(stay, 800, 900);
        Assert.All(Enumerable.Range(0, 1000), party => Assert.InRange(Performing.ApplauseMinutes(party), 1, Performing.BowMinutes));
        Assert.Equal(Performing.ApplauseMinutes(17), Performing.ApplauseMinutes(17));
    }

    private static FestivalGame Festival(int seed = 21)
    {
        var game = new FestivalGame(seed: seed);
        var f = game.Field.Field;
        Assert.True(game.BuildStage(StageKind.Bandstand, f.X + 50, f.Y + 14, Facing.South).Ok);
        Amenities.Add(game);
        return game;
    }

    private static void RunUntil(FestivalGame game, DateTime when)
    {
        while (game.Now < when)
        {
            game.Clock.Step();
        }
    }

    [Fact]
    public void The_act_stays_on_to_take_its_applause_for_a_few_minutes_after_the_set()
    {
        var game = Festival();
        var first = game.Show.Slots[0];
        Assert.Null(game.Show.Bowing(first.End.AddSeconds(-1)));
        Assert.Equal(first, game.Show.Bowing(first.End));
        Assert.Equal(first, game.Show.Bowing(first.End.AddMinutes(Performing.BowMinutes).AddSeconds(-1)));
        Assert.Null(game.Show.Bowing(first.End.AddMinutes(Performing.BowMinutes)));
        Assert.Null(game.Show.Bowing(first.Start.AddMinutes(10)));

        // Not once the music has been called off.
        game.Show.CallOff(first.Start.AddMinutes(20));
        Assert.Null(game.Show.Bowing(first.End));
    }

    [Fact]
    public void Most_of_the_audience_watch_to_the_end_clap_the_act_off_and_then_drift_away()
    {
        var game = Festival();
        var set = game.Show.Slots[0];
        RunUntil(game, set.End.AddMinutes(-20));
        var earlier = game.Crowd.Watching;
        Assert.True(earlier > 20, $"People should be watching twenty minutes from the end, not {earlier}.");

        RunUntil(game, set.End.AddSeconds(-5));
        var atTheEnd = game.Crowd.Watching;
        Assert.True(atTheEnd >= earlier * 0.8, $"Most should still be there at the end: {earlier} twenty minutes before, {atTheEnd} at the end.");

        // A minute after the end nearly all of them are still there, clapping.
        RunUntil(game, set.End.AddSeconds(50));
        var clapping = game.Crowd.Watching;
        Assert.True(clapping >= atTheEnd * 0.8, $"Most should stay to clap: {atTheEnd} at the end, {clapping} just after.");

        // Once the act has gone they drift off.
        RunUntil(game, set.End.AddMinutes(Performing.BowMinutes + 1));
        Assert.Equal(0, game.Crowd.Watching);
    }
}
