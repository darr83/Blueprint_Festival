using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;

namespace BlueprintFestival.Simulation.Tests;

public class StageCameraTests
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
    public void The_camera_films_the_stage_that_is_playing_and_the_main_stage_first()
    {
        var game = TwoStages();
        var main = game.Show.Stage!;
        var second = game.SecondShow.Stage!;

        var secondSets = game.SecondShow.Slots
            .SelectMany(s => Enumerable.Range(0, (int)(s.End - s.Start).TotalMinutes).Select(m => s.Start.AddMinutes(m)))
            .ToList();

        // Both playing at once: the main stage.
        Assert.Same(main, StageCameras.Pick(game, secondSets.First(t => game.Show.Playing(t) is not null)));

        // Only the second stage playing (the main stage's music was called off): the second stage.
        var later = secondSets[^1];
        game.Show.CallOff(later.AddMinutes(-30));
        Assert.Same(second, StageCameras.Pick(game, later));

        // Before the music starts: the main stage.
        Assert.Same(main, StageCameras.Pick(game, game.Day.AddHours(9)));

        // Once the second stage's music is over too, back to the main stage.
        Assert.Same(main, StageCameras.Pick(game, game.Day.AddHours(23.5)));
    }

    [Fact]
    public void With_no_music_booked_or_no_stage_there_is_still_a_sensible_answer()
    {
        var unbooked = TwoStages(book: false);
        Assert.Equal(StageKind.MainStage, StageCameras.Pick(unbooked, unbooked.Day.AddHours(9))!.Kind);

        var empty = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(4, 1500, 60000, false, 80));
        Assert.Null(StageCameras.Pick(empty, empty.Day.AddHours(14)));
    }

    [Theory]
    [InlineData(StageKind.FarmTrailer)]
    [InlineData(StageKind.Bandstand)]
    [InlineData(StageKind.HiredStage)]
    [InlineData(StageKind.MainStage)]
    public void The_front_cam_looks_back_at_the_band_and_the_back_cam_looks_out_at_the_crowd(StageKind kind)
    {
        foreach (var facing in new[] { Facing.South, Facing.North, Facing.East, Facing.West })
        {
            var stage = new PlacedStage(kind, 40, 40, facing);
            var (fx, fy, dx, dy) = SiteLayout.Front(stage.Rect, facing);
            var front = StageCameras.Of(stage, StageShot.Front);
            var back = StageCameras.Of(stage, StageShot.Back);

            // How far out from the front edge of the stage, towards the crowd.
            double Out(double x, double y) => ((x - fx) * dx) + ((y - fy) * dy);

            // The front cam stands out in the crowd, up above their heads, looking back at the deck.
            Assert.True(Out(front.EyeX, front.EyeY) > 2, $"{kind} {facing}: the front cam should stand out in front of the stage.");
            Assert.True(front.EyeHeight > 2, $"{kind} {facing}: the front cam should be above people's heads.");
            Assert.True(Out(front.LookX, front.LookY) < 0, $"{kind} {facing}: the front cam should look at the stage.");

            // The back cam stands on the stage, behind the band, and looks out over the crowd.
            Assert.True(stage.Rect.Contains(BlueprintFestival.Simulation.World.FieldMap.TileAt(back.EyeX, back.EyeY)), $"{kind} {facing}: the back cam should be on the stage.");
            Assert.True(Out(back.EyeX, back.EyeY) < 0 && Out(back.LookX, back.LookY) > 10, $"{kind} {facing}: the back cam should look out at the crowd.");
            Assert.True(back.EyeHeight > back.LookHeight, $"{kind} {facing}: the back cam should look down at the crowd.");

            // Both are straight down the middle of the stage.
            var midX = (stage.Rect.X + (stage.Rect.Width / 2.0)) * 0.5;
            var midY = (stage.Rect.Y + (stage.Rect.Height / 2.0)) * 0.5;
            Assert.Equal(dx == 0 ? midX : front.EyeX, front.EyeX, 3);
            Assert.Equal(dy == 0 ? midY : back.EyeY, back.EyeY, 3);
        }
    }
}
