using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;

namespace BlueprintFestival.Simulation.Tests;

public class StageTimesTests
{
    private static FestivalGame Planning(bool second = true)
    {
        var game = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(4, 1500, 60000, false, 80));
        game.ChooseHeadliner(0);
        game.BookSecondStage(second);
        return game;
    }

    [Fact]
    public void Sets_start_at_two_four_six_and_eight_until_they_are_moved()
    {
        var game = Planning();
        Assert.Equal(new[] { 14, 16, 18, 20 }, game.Show.Hours);
        Assert.Equal(new[] { 14, 16, 18, 20 }, game.SecondShow.Hours);
        Assert.Equal(game.Day.AddHours(14), game.Show.TimeOf(0).Start);
        Assert.Equal(game.Day.AddHours(15.5), game.Show.TimeOf(0).End);
    }

    [Fact]
    public void Moving_one_stage_leaves_the_other_alone_so_they_can_start_and_finish_apart()
    {
        var game = Planning();
        var first = RunningOrder.SecondBase;
        Assert.True(game.MoveSet(first, -1));
        Assert.Equal(new[] { 13, 16, 18, 20 }, game.SecondShow.Hours);
        Assert.Equal(new[] { 14, 16, 18, 20 }, game.Show.Hours);
        Assert.Equal(game.Day.AddHours(13), game.SecondShow.Slots.First(s => s.Index == first).Start);

        // The second stage's whole day an hour earlier: 13, 15, 17, 19.
        for (var i = 1; i < RunningOrder.SetsAStage; i++)
        {
            Assert.True(game.MoveSet(RunningOrder.SecondBase + i, -1));
        }

        Assert.Equal(new[] { 13, 15, 17, 19 }, game.SecondShow.Hours);
        Assert.Equal(game.Day.AddHours(20.5), game.SecondShow.Slots[^1].End);
        Assert.Equal(game.Day.AddHours(21.5), game.Show.Slots[^1].End);
    }

    [Fact]
    public void Moving_a_set_later_pushes_the_ones_after_it_along_and_never_past_nine()
    {
        var game = Planning();
        Assert.True(game.MoveSet(1, 1));
        Assert.Equal(new[] { 14, 17, 19, 21 }, game.Show.Hours);

        // Nine o'clock is the latest start, so there is no more room later on.
        Assert.False(game.CanMoveSet(1, 1));
        Assert.False(game.MoveSet(3, 1));
        Assert.Equal(new[] { 14, 17, 19, 21 }, game.Show.Hours);

        // Earlier pushes the ones before it back, down to midday.
        Assert.True(game.MoveSet(1, -3));
        Assert.Equal(new[] { 12, 14, 19, 21 }, game.Show.Hours);
        Assert.False(game.CanMoveSet(0, -1));
        Assert.False(game.CanMoveSet(1, -1));
    }

    [Fact]
    public void Sets_on_one_stage_stay_an_hour_and_a_half_long_with_the_next_on_the_following_hour()
    {
        var game = Planning();
        game.MoveSet(2, -1);
        var slots = game.Show.Slots;
        Assert.All(slots, s => Assert.Equal(RunningOrder.SetMinutes, (s.End - s.Start).TotalMinutes));
        for (var i = 1; i < slots.Count; i++)
        {
            Assert.True(slots[i].Start >= slots[i - 1].End.AddMinutes(30), "The next set should start on the next hour after one ends, at the earliest.");
            Assert.Equal(0, slots[i].Start.Minute);
        }
    }

    [Fact]
    public void An_unbooked_second_stage_cannot_be_moved_and_nothing_moves_after_planning()
    {
        var game = Planning(second: false);
        Assert.False(game.MoveSet(RunningOrder.SecondBase, 1));
        Assert.True(game.ConfirmPlan());
        Assert.Throws<InvalidOperationException>(() => game.MoveSet(0, 1));
        Assert.False(game.CanMoveSet(0, 1));
    }

    [Fact]
    public void Bad_hours_from_a_save_are_ignored()
    {
        var game = Planning();
        Assert.False(game.Show.SetHours(new[] { 14, 15, 18, 20 }));
        Assert.False(game.Show.SetHours(new[] { 11, 14, 18, 20 }));
        Assert.False(game.Show.SetHours(new[] { 14, 16, 18, 22 }));
        Assert.Equal(new[] { 14, 16, 18, 20 }, game.Show.Hours);
        Assert.True(game.Show.SetHours(new[] { 12, 15, 17, 21 }));
    }

    [Fact]
    public void Moved_times_survive_a_save_and_load()
    {
        var game = Planning();
        game.MoveSet(RunningOrder.SecondBase, -2);
        game.MoveSet(3, 1);
        var loaded = FestivalGame.Load(game.Save());
        Assert.Equal(new[] { 14, 16, 18, 21 }, loaded.Show.Hours);
        Assert.Equal(new[] { 12, 16, 18, 20 }, loaded.SecondShow.Hours);
        Assert.Equal(loaded.Day.AddHours(21), loaded.Show.Slots[^1].Start);
    }

    [Fact]
    public void Two_stages_at_different_times_play_through_a_whole_day()
    {
        var game = Planning();
        for (var i = 0; i < RunningOrder.SetsAStage; i++)
        {
            game.MoveSet(RunningOrder.SecondBase + i, -1);
        }

        Assert.True(game.ConfirmPlan());
        var f = game.Field.Field;
        Assert.True(game.BuildStage(StageKind.HiredStage, f.X + 30, f.Y + 6, Facing.South).Ok);
        Assert.True(game.BuildStage(StageKind.MainStage, f.Right - 90, f.Y + 6, Facing.South).Ok);
        var thirteen = game.Day.AddHours(13.25);
        Assert.NotNull(game.SecondShow.Playing(thirteen));
        Assert.Null(game.Show.Playing(thirteen));
        var lines = OnNow.Lines(game, thirteen);
        Assert.Equal("Next", lines[0].Kicker);
        Assert.Equal("On now", lines[1].Kicker);
    }
}
