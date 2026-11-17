using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

/// <summary>
/// Both stages fill from the front while acts are on at the same time, even a small act on the
/// second stage of a big festival (Darren, 17 Nov 2026: nobody was at stage 2 during a show).
/// </summary>
public class StageCrowdTests
{
    /// <summary>
    /// A festival of 3,000 with acts on both stages at the same hours: the main stage in the
    /// north-east, a hired stage in the north-west, stalls down both sides.
    /// </summary>
    private static FestivalGame Built()
    {
        var game = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(4, 3000, 90000, false, 80));
        game.ChooseHeadliner(0);
        game.BookSecondStage(true);
        Assert.True(game.ConfirmPlan());
        Assert.Equal(game.Shows[0].Hours, game.Shows[1].Hours);
        var gap = game.Field.Gap;
        var y = gap.Y + 1;
        var f = game.Field.Field;
        Assert.True(game.BuildFence(FenceKind.SteelMesh, new TilePoint(gap.X, y), new TilePoint(gap.Right - 1, y)).Ok);
        Assert.True(game.BuildGate(GateKind.TurnstileRow, gap.X, y, false).Ok);
        for (var lane = 0; lane < 4; lane++)
        {
            Assert.True(game.HireStaff(StaffKind.TicketStaff, new TilePoint(gap.X + (lane * 6) + 1, y)).Ok);
        }

        Assert.True(game.BuildStage(StageKind.HiredStage, f.X + 30, f.Y + 6, Facing.South).Ok);
        Assert.True(game.BuildStage(StageKind.MainStage, f.Right - 90, f.Y + 6, Facing.South).Ok);
        var kinds = new[] { StallKind.Burgers, StallKind.Bar, StallKind.ToiletBlock, StallKind.WaterPoint };
        for (var i = 0; i < 16; i++)
        {
            var east = i % 2 == 0;
            var x = east ? f.Right - 16 - (i / 8 * 40) : f.X + 4 + (i / 8 * 40);
            Assert.True(game.BuildStall(kinds[i % 4], x, f.Y + 60 + (i / 2 % 4 * 26), east ? Facing.West : Facing.East).Ok, $"Stall {i} would not fit.");
        }

        return game;
    }

    private static List<Party> WatchingOn(FestivalGame game, int stage) =>
        game.Crowd.Parties.Where(p => p.State == PartyState.Watching && p.Watch >= 0 && RunningOrder.StageOf(p.Watch) == stage).ToList();

    [Fact]
    public void The_second_stage_draws_a_crowd_that_stands_near_its_front_while_the_main_stage_is_on()
    {
        var game = Built();
        game.OpenGates();
        var second = game.Shows[1];
        var (fx, fy, _, _) = RunningOrder.Front(second.Stage!);
        var until = game.Day.AddHours(15.3);
        while (game.Now < until)
        {
            game.Clock.Step();
        }

        Assert.NotNull(game.Shows[0].Playing(game.Now));
        Assert.NotNull(second.Playing(game.Now));
        var main = WatchingOn(game, 0).Sum(p => p.Members.Count);
        var watching = WatchingOn(game, 1);
        var people = watching.Sum(p => p.Members.Count);
        var onSite = game.Crowd.OnSite;
        Assert.True(people * 8 >= onSite, $"{people} watching the second stage of {onSite} here ({main} at the main stage).");

        // They fill it from the front instead of dotting themselves over the whole field.
        var metres = watching.Select(p => Math.Sqrt(((p.X - fx) * (p.X - fx)) + ((p.Y - fy) * (p.Y - fy)))).OrderBy(m => m).ToList();
        var middle = metres[metres.Count / 2];
        Assert.True(middle < 18, $"Half the second stage's crowd is further back than {middle:0} m.");
        Assert.True(watching.Count(p => Math.Sqrt(((p.X - fx) * (p.X - fx)) + ((p.Y - fy) * (p.Y - fy))) < 10) * 4 >= watching.Count, "Too few right at the front.");
    }

    [Fact]
    public void An_audience_spreads_further_back_as_it_grows()
    {
        Assert.True(Crowd.AudienceReach(0.3, 50) < Crowd.AudienceReach(0.3, 500));
        Assert.True(Crowd.AudienceReach(0.3, 500) < Crowd.AudienceReach(0.3, 3000));

        // Even a small act's crowd stands within a stone's throw of the stage while there are only a few hundred of them.
        Assert.True(Crowd.AudienceReach(0.18, 300) < 20);
    }
}
