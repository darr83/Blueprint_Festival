using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.Sound;

namespace BlueprintFestival.Simulation.Tests;

public class TwoStageTests
{
    /// <summary>A festival of 1,500 with a hired stage on the left and the main stage on the right, both facing south.</summary>
    private static FestivalGame TwoStages(bool book = true)
    {
        var game = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(4, 1500, 60000, false, 80));
        game.ChooseHeadliner(0);
        game.BookSecondStage(book);
        Assert.True(game.ConfirmPlan());
        var f = game.Field.Field;
        Assert.True(game.BuildStage(StageKind.HiredStage, f.X + 30, f.Y + 6, Facing.South).Ok);
        Assert.True(game.BuildStage(StageKind.MainStage, f.Right - 90, f.Y + 6, Facing.South).Ok);
        return game;
    }

    [Fact]
    public void The_top_bar_has_a_line_for_each_stage_with_the_main_stage_as_stage_1()
    {
        var game = TwoStages();
        var during = game.Show.Slots[0].Start.AddMinutes(10);
        var lines = OnNow.Lines(game, during);

        Assert.Equal(2, lines.Count);
        Assert.Equal("Stage 1", lines[0].Stage);
        Assert.Equal("Stage 2", lines[1].Stage);
        Assert.Equal("On now", lines[0].Kicker);
        Assert.Equal(game.Show.Playing(during)!.Act.Name, lines[0].Name);
        Assert.Equal(game.SecondShow.Playing(during)!.Act.Name, lines[1].Name);
        Assert.NotEqual(lines[0].Name, lines[1].Name);
        Assert.StartsWith("Stage 2 · On now: ", lines[1].Text, StringComparison.Ordinal);

        // Before the music starts, both say who is on first.
        var morning = OnNow.Lines(game, game.Day.AddHours(9));
        Assert.All(morning, l => Assert.Equal("Next", l.Kicker));
    }

    [Fact]
    public void One_stage_is_one_line_with_no_stage_name()
    {
        var game = TwoStages(book: false);
        var lines = OnNow.Lines(game, game.Show.Slots[0].Start.AddMinutes(10));
        Assert.Single(lines);
        Assert.Equal("", lines[0].Stage);
        Assert.Equal("On now", lines[0].Kicker);
        Assert.DoesNotContain("Stage", lines[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_stage_that_was_called_off_says_so_while_the_other_carries_on()
    {
        var game = TwoStages();
        var during = game.Show.Slots[1].Start.AddMinutes(10);
        game.Show.CallOff(during.AddMinutes(-5));
        var lines = OnNow.Lines(game, during);
        Assert.Equal("The music has been called off", lines[0].Name);
        Assert.Equal("On now", lines[1].Kicker);
    }

    [Fact]
    public void Zoomed_right_out_every_stage_is_heard_faintly_and_about_as_loud()
    {
        var stages = new List<(double X, double Y)> { (40, 20), (200, 20) };
        var levels = StageMix.Levels(120, 60, StageMix.FurthestView, stages);
        Assert.All(levels, l => Assert.InRange(l, 0.2, 0.5));
        Assert.True(Math.Abs(levels[0] - levels[1]) < 0.05);
    }

    [Fact]
    public void Zooming_in_on_one_stage_makes_it_the_main_sound()
    {
        var stages = new List<(double X, double Y)> { (40, 20), (200, 20) };
        var close = StageMix.Levels(40, 26, 30, stages);
        Assert.True(close[0] > 0.8, $"The stage looked at should be loud, not {close[0]:0.00}.");
        Assert.True(close[1] < 0.1, $"The far stage should be faint, not {close[1]:0.00}.");
        Assert.True(close[1] >= StageMix.Faintest, "A stage that is playing is never quite silent.");
        Assert.Equal(0, StageMix.Loudest(close));

        // And the other way round over the other stage.
        Assert.Equal(1, StageMix.Loudest(StageMix.Levels(200, 26, 30, stages)));

        // Zooming in steadily on a stage turns it up and the other down.
        var last = (Near: 0.0, Far: 1.0);
        foreach (var size in new[] { 260.0, 150, 80, 40, 15 })
        {
            var levels = StageMix.Levels(41, 22, size, stages);
            Assert.True(levels[0] >= last.Near - 1e-9 && levels[1] <= last.Far + 1e-9, $"At a view of {size} m the mix went the wrong way.");
            last = (levels[0], levels[1]);
        }
    }

    [Fact]
    public void A_stage_camera_hears_the_stage_it_films()
    {
        var levels = StageMix.Filming(1, 2);
        Assert.Equal(1, levels[1]);
        Assert.Equal(StageMix.OffCamera, levels[0]);
        Assert.Equal(1, StageMix.Loudest(levels));
    }

    [Fact]
    public void The_stage_cameras_go_round_every_stage_and_back_to_the_first()
    {
        var game = TwoStages();
        var all = StageCameras.All(game);
        Assert.Equal(2, all.Count);
        Assert.Same(game.Show.Stage, all[0]);
        Assert.Equal("Stage 1", StageCameras.Name(game, all[0]));
        Assert.Equal("Stage 2", StageCameras.Name(game, all[1]));

        Assert.Equal(all[1], StageCameras.After(game, all[0]));
        Assert.Equal(all[0], StageCameras.After(game, all[1]));
        Assert.Equal(all[0], StageCameras.After(game, null));

        // A stage picked by the player is kept even while the other stage is playing.
        var during = game.Show.Slots[0].Start.AddMinutes(10);
        Assert.Same(game.Show.Stage, StageCameras.Pick(game, during));
        Assert.Equal(all[1], StageCameras.Pick(game, all[1]));

        // A stage that is no longer on the site falls back to the usual choice.
        Assert.Equal(StageCameras.Pick(game), StageCameras.Pick(game, new PlacedStage(StageKind.Bandstand, 1, 1, Facing.South)));
    }

    [Fact]
    public void With_no_stage_the_cameras_have_nowhere_to_go()
    {
        var empty = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(4, 1500, 60000, false, 80));
        Assert.Null(StageCameras.After(empty, null));
        Assert.Empty(OnNow.Lines(empty).Where(l => l.Stage.Length > 0));
    }
}
