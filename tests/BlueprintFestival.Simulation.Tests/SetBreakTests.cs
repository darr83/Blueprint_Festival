using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;

namespace BlueprintFestival.Simulation.Tests;

/// <summary>The half hour with an empty stage after each set, and skipping on to the next act.</summary>
public class SetBreakTests
{
    private static readonly DateTime Day = GameClock.DefaultStart.Date;

    private static FestivalGame Festival(int seed = 21)
    {
        var game = new FestivalGame(seed: seed);
        var f = game.Field.Field;
        Assert.True(game.BuildStage(StageKind.Bandstand, f.X + 50, f.Y + 14, Facing.South).Ok);
        Amenities.Add(game);
        return game;
    }

    private static void RunUntil(FestivalGame game, double hour)
    {
        while (game.Now < Day.AddHours(hour))
        {
            game.Clock.Step();
        }
    }

    private static void Skip(FestivalGame game)
    {
        Assert.True(game.SkipToNextAct());
        for (var frames = 0; game.SkippingTo is not null; frames++)
        {
            Assert.True(frames < 100000, "The skip should come to an end.");
            Assert.InRange(game.Advance(1 / 60.0), 0, FestivalGame.SkipTicksAFrame);
        }
    }

    [Fact]
    public void An_act_plays_for_ninety_minutes_and_a_break_of_thirty_follows()
    {
        Assert.Equal(90, RunningOrder.SetMinutes);
        Assert.Equal(30, RunningOrder.BreakMinutes);
        var show = new FestivalGame(seed: 3).Show;
        for (var place = 0; place < RunningOrder.SetsAStage; place++)
        {
            var (start, end) = show.TimeOf(place);
            Assert.Equal(Day.AddHours(14 + (2 * place)), start);
            Assert.Equal(start.AddMinutes(RunningOrder.SetMinutes), end);
        }
    }

    [Fact]
    public void When_a_set_ends_the_audience_leaves_the_stage_and_more_people_go_to_the_stalls()
    {
        var game = Festival();
        RunUntil(game, 15.45);
        var watching = game.Crowd.Watching;
        var atStalls = game.Crowd.AtStalls;
        Assert.True(watching > 10, $"People should be watching near the end of the set, not {watching}.");

        RunUntil(game, 15.6);
        Assert.Null(game.Show.Playing(game.Now));
        Assert.True(game.Show.Changeover(game.Now));
        Assert.Equal(0, game.Crowd.Watching);
        Assert.True(game.Crowd.AtStalls > atStalls, $"More people should be at the stalls in the break: {atStalls} before, {game.Crowd.AtStalls} after.");

        // And they come back for the next act.
        RunUntil(game, 16.3);
        Assert.True(game.Crowd.Watching > 10, $"People should watch the next act, not {game.Crowd.Watching}.");
    }

    [Fact]
    public void Skipping_goes_to_the_start_of_the_next_act_from_a_set_or_from_the_break()
    {
        var game = Festival();
        RunUntil(game, 12);
        Assert.Equal(0, game.NextAct!.Index);
        Skip(game);
        Assert.Equal(Day.AddHours(14), game.Now);
        Assert.Equal(0, game.Show.Playing(game.Now)!.Index);

        // In the middle of a set it goes past the rest of the set and the break.
        RunUntil(game, 14.5);
        Skip(game);
        Assert.Equal(Day.AddHours(16), game.Now);

        // In the break it goes to the end of the break.
        RunUntil(game, 17.6);
        Skip(game);
        Assert.Equal(Day.AddHours(18), game.Now);
        Assert.Equal(2, game.Show.Playing(game.Now)!.Index);
    }

    [Fact]
    public void Skipping_plays_out_the_time_in_between_just_as_running_it_would()
    {
        var skipped = Festival(seed: 8);
        var run = Festival(seed: 8);
        RunUntil(skipped, 14.5);
        RunUntil(run, 14.5);
        skipped.Clock.SetSpeed(GameSpeed.Paused);
        Skip(skipped);
        RunUntil(run, 16);
        Assert.Equal(run.Now, skipped.Now);
        Assert.Equal(run.Money, skipped.Money);
        Assert.Equal(run.Crowd.OnSite, skipped.Crowd.OnSite);
        Assert.Equal(run.Crowd.Happiness, skipped.Crowd.Happiness);

        // The speed is left as the player had it.
        Assert.Equal(GameSpeed.Paused, skipped.Clock.Speed);
        Assert.Equal(0, skipped.Advance(1));
    }

    [Fact]
    public void Skipping_looks_at_both_stages_skips_empty_sets_and_stops_after_the_last_act()
    {
        var game = Festival();
        game.Show.Book(1, null);
        RunUntil(game, 14.2);
        Assert.Equal(2, game.NextAct!.Index);
        Skip(game);
        Assert.Equal(Day.AddHours(18), game.Now);
        Skip(game);
        Assert.Equal(Day.AddHours(20), game.Now);
        Assert.Null(game.NextAct);
        Assert.False(game.SkipToNextAct());
        Assert.Null(game.SkippingTo);
    }

    [Fact]
    public void A_skip_can_be_stopped_and_there_is_nothing_to_skip_to_without_a_stage()
    {
        var game = Festival();
        RunUntil(game, 12);
        Assert.True(game.SkipToNextAct());
        game.Advance(1 / 60.0);
        game.StopSkipping();
        Assert.Null(game.SkippingTo);
        Assert.True(game.Now > Day.AddHours(12) && game.Now < Day.AddHours(14));

        var bare = new FestivalGame(seed: 21);
        Assert.Null(bare.NextAct);
        Assert.False(bare.SkipToNextAct());
    }
}
