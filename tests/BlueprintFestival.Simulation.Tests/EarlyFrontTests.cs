using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;

namespace BlueprintFestival.Simulation.Tests;

/// <summary>For an act with a big following, people fill the front of the stage before the set starts.</summary>
public class EarlyFrontTests
{
    private static readonly DateTime Day = GameClock.DefaultStart.Date;

    private static FestivalGame Festival(int fans)
    {
        var game = new FestivalGame(seed: 21);
        var f = game.Field.Field;
        Assert.True(game.BuildStage(StageKind.Bandstand, f.X + 50, f.Y + 14, Facing.South).Ok);
        Amenities.Add(game);
        game.Show.Book(1, game.Show.At(1)! with { Fans = fans });
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
    public void Only_an_act_with_twenty_thousand_fans_or_more_draws_people_in_the_last_twenty_minutes_before_the_set()
    {
        Assert.Equal(20000, RunningOrder.BigFollowing);
        Assert.Equal(20, RunningOrder.EarlyMinutes);
        var big = Festival(20000).Show;
        var small = Festival(19999).Show;
        var start = big.TimeOf(1).Start;

        Assert.Null(big.FillingUp(start.AddMinutes(-21)));
        Assert.Equal(1, big.FillingUp(start.AddMinutes(-20))?.Index);
        Assert.Equal(1, big.FillingUp(start.AddMinutes(-1))?.Index);
        Assert.Null(big.FillingUp(start));
        Assert.Equal(1, big.Drawing(start.AddMinutes(-5))?.Index);
        Assert.Equal(1, big.Drawing(start.AddMinutes(5))?.Index);
        Assert.Null(small.FillingUp(start.AddMinutes(-5)));
        Assert.Null(small.Drawing(start.AddMinutes(-5)));
    }

    [Fact]
    public void People_wait_at_the_front_for_a_big_act_but_not_for_a_small_one()
    {
        var big = Festival(52000);
        var small = Festival(500);
        var start = big.Show.TimeOf(1).Start;

        // Ten minutes into the break the stage is still empty for both.
        RunUntil(big, start.AddMinutes(-21));
        RunUntil(small, start.AddMinutes(-21));
        Assert.Equal(0, big.Crowd.Watching);
        Assert.Equal(0, small.Crowd.Watching);

        // Two minutes before the start the front is filling for the big act only.
        RunUntil(big, start.AddMinutes(-2));
        RunUntil(small, start.AddMinutes(-2));
        Assert.Null(big.Show.Playing(big.Now));
        Assert.True(big.Crowd.Watching > 10, $"People should be waiting at the front for a big act, not {big.Crowd.Watching}.");
        Assert.Equal(0, small.Crowd.Watching);

        // And they stay on to watch it.
        RunUntil(big, start.AddMinutes(10));
        Assert.True(big.Crowd.Watching > 10, $"People should watch the big act once it starts, not {big.Crowd.Watching}.");
    }

    [Fact]
    public void A_big_act_can_be_called_off_while_the_crowd_waits_for_it()
    {
        var show = Festival(52000).Show;
        var start = show.TimeOf(1).Start;
        Assert.False(show.CutShort(1, start.AddMinutes(-40)), "Too early: nobody is waiting yet.");
        Assert.True(show.CutShort(1, start.AddMinutes(-5)));
        Assert.Null(show.Playing(start.AddMinutes(10)));
        Assert.Null(show.FillingUp(start.AddMinutes(-4)));
        Assert.Equal(2, show.Next(start.AddMinutes(-4))?.Index);
    }
}
