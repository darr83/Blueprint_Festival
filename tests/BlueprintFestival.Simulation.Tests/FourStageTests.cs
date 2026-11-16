using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

public class FourStageTests
{
    private static FestivalGame Planned() => FestivalGame.StartYear("Larkmoor Festival", new YearSetup(4, 1500, 90000, false, 80));

    /// <summary>
    /// A festival of 1,500 on its build day with all four stages booked (the seed's acts on each)
    /// and four stages put up: a bandstand and a farm trailer first, then a hired stage, then the main stage.
    /// </summary>
    private static FestivalGame Built()
    {
        var game = Planned();
        game.ChooseHeadliner(0);
        for (var n = 1; n < RunningOrder.MaxStages; n++)
        {
            game.Shows[n].Booked = true;
        }

        Assert.True(game.ConfirmPlan());
        var gap = game.Field.Gap;
        var y = gap.Y + 1;
        var f = game.Field.Field;
        Assert.True(game.BuildFence(FenceKind.SteelMesh, new TilePoint(gap.X, y), new TilePoint(gap.Right - 1, y)).Ok);
        Assert.True(game.BuildGate(GateKind.TurnstileRow, gap.X, y, false).Ok);
        for (var lane = 0; lane < 4; lane++)
        {
            Assert.True(game.HireStaff(StaffKind.TicketStaff, new TilePoint(gap.X + (lane * 6) + 1, y)).Ok);
        }

        Assert.True(game.BuildStage(StageKind.Bandstand, f.X + 4, f.Y + 6, Facing.South).Ok);
        Assert.True(game.BuildStage(StageKind.FarmTrailer, f.X + 60, f.Y + 6, Facing.South).Ok);
        Assert.True(game.BuildStage(StageKind.HiredStage, f.X + 30, f.Y + 30, Facing.South).Ok);
        Assert.True(game.BuildStage(StageKind.MainStage, f.Right - 90, f.Y + 6, Facing.South).Ok);
        var kinds = new[] { StallKind.Burgers, StallKind.Bar, StallKind.ToiletBlock, StallKind.WaterPoint };
        for (var i = 0; i < 6; i++)
        {
            var east = i % 2 == 0;
            var x = east ? f.Right - 16 : f.X + 4;
            Assert.True(game.BuildStall(kinds[i % 4], x, f.Y + 70 + (i / 2 * 24), east ? Facing.West : Facing.East).Ok, $"Stall {i} would not fit.");
        }

        return game;
    }

    private static int WatchingOn(FestivalGame game, int stage) =>
        game.Crowd.Parties.Where(p => p.State == PartyState.Watching && p.Watch >= 0 && RunningOrder.StageOf(p.Watch) == stage).Sum(p => p.Members.Count);

    [Fact]
    public void Up_to_three_more_stages_can_be_added_each_with_four_empty_sets()
    {
        var game = Planned();
        Assert.Equal(RunningOrder.MaxStages, game.Shows.Count);
        Assert.Equal(1, game.StagesBooked);
        Assert.Equal(1, game.AddEmptyStage());
        Assert.Equal(2, game.AddEmptyStage());
        Assert.Equal(3, game.AddEmptyStage());
        Assert.Equal(-1, game.AddEmptyStage());
        Assert.Equal(4, game.StagesBooked);
        Assert.All(game.Shows.Skip(1), s => Assert.Empty(s.Slots));

        // Each stage's sets are numbered from its own hundred, and any artist can go in any of them.
        Assert.Equal(new[] { 0, 100, 200, 300 }, game.Shows.Select(s => s.Base));
        Assert.True(game.BookAct(300, "DJ Haybale"));
        Assert.Equal("DJ Haybale", game.Shows[3].At(300)?.Name);
        Assert.Equal(game.Shows[3], game.ShowOf(301));
        Assert.True(game.MoveSet(300, -2));
        Assert.Equal(12, game.Shows[3].HourOf(0));
        Assert.Equal(14, game.Shows[2].HourOf(0));
        Assert.Contains(game.LineUp, s => s.Index == 300);
        Assert.Contains(300, game.LineUp.Select(s => s.Index));
    }

    [Fact]
    public void Taking_a_stage_away_moves_the_ones_after_it_up_a_place()
    {
        var game = Planned();
        game.AddEmptyStage();
        game.AddEmptyStage();
        game.AddEmptyStage();
        Assert.True(game.BookAct(200, "DJ Haybale"));
        Assert.True(game.MoveSet(200, 1));
        var fees = game.ActFees;

        Assert.True(game.RemoveStage(1));
        Assert.Equal(3, game.StagesBooked);
        Assert.Equal("DJ Haybale", game.Shows[1].At(100)?.Name);
        Assert.Equal(15, game.Shows[1].HourOf(0));
        Assert.False(game.Shows[3].Booked);
        Assert.Empty(game.Shows[3].Slots);
        Assert.Equal(fees, game.ActFees);

        Assert.False(game.RemoveStage(0));
        Assert.False(game.RemoveStage(3));
        Assert.True(game.RemoveStage(1));
        Assert.True(game.RemoveStage(1));
        Assert.Equal(1, game.StagesBooked);
        Assert.Null(game.SetOf("DJ Haybale"));
    }

    [Fact]
    public void Four_stages_each_get_their_own_stage_and_crowd()
    {
        var game = Built();
        var order = RunningOrder.InOrder(game.Site).Select(s => s.Kind).ToList();
        Assert.Equal(new[] { StageKind.MainStage, StageKind.HiredStage, StageKind.Bandstand, StageKind.FarmTrailer }, order);
        Assert.Equal(order, game.Shows.Select(s => s.Stage!.Kind));

        game.OpenGates();
        var most = new int[RunningOrder.MaxStages];
        while (game.Now.TimeOfDay < TimeSpan.FromHours(15.2))
        {
            game.Clock.Step();
            if (game.Clock.TickCount % 120 == 0)
            {
                for (var i = 0; i < most.Length; i++)
                {
                    most[i] = Math.Max(most[i], WatchingOn(game, i));
                }
            }
        }

        Assert.All(game.Shows, s => Assert.NotNull(s.Playing(game.Now)));
        Assert.All(most, m => Assert.True(m > 0, $"Most watching each stage: {string.Join(", ", most)}."));

        // The top bar has a line for each stage, and the stage cameras go round all four.
        var lines = OnNow.Lines(game);
        Assert.Equal(new[] { "Stage 1", "Stage 2", "Stage 3", "Stage 4" }, lines.Select(l => l.Stage));
        Assert.All(lines, l => Assert.Equal("On now", l.Kicker));
        Assert.Equal(4, StageCameras.All(game).Count);
        Assert.Equal(game.Shows[0].Stage, StageCameras.After(game, game.Shows[3].Stage));

        // And the day so far lists every stage.
        Assert.Equal(4, DayReport.Of(game).Stages.Count);
    }

    [Fact]
    public void Four_stages_save_and_load_and_play_out_the_same()
    {
        var game = Built();
        Assert.True(game.Shows[3].SetHours(new[] { 13, 15, 17, 19 }));
        game.OpenGates();
        while (game.Now.TimeOfDay < TimeSpan.FromHours(14.6))
        {
            game.Clock.Step();
        }

        var loaded = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));
        Assert.Equal(4, loaded.StagesBooked);
        Assert.Equal(new[] { 13, 15, 17, 19 }, loaded.Shows[3].Hours);
        Assert.Equal(game.LineUp.Select(s => (s.Index, s.Act.Name, s.Start)), loaded.LineUp.Select(s => (s.Index, s.Act.Name, s.Start)));
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(WatchingOn(game, i), WatchingOn(loaded, i));
        }

        for (var i = 0; i < 1200; i++)
        {
            game.Clock.Step();
            loaded.Clock.Step();
        }

        Assert.Equal(game.Crowd.Watching, loaded.Crowd.Watching);
        Assert.Equal(WatchingOn(game, 3), WatchingOn(loaded, 3));
        Assert.Equal(game.Money, loaded.Money);
    }

    [Fact]
    public void An_older_save_with_two_stages_still_loads()
    {
        var game = Planned();
        game.BookSecondStage(true);
        var json = game.Save().ToJson();
        var loaded = FestivalGame.Load(SaveData.FromJson(json));
        Assert.Equal(2, loaded.StagesBooked);
        Assert.Equal(game.SecondShow.Slots.Select(s => s.Act.Name), loaded.SecondShow.Slots.Select(s => s.Act.Name));
    }
}
