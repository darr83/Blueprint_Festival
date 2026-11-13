using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

public class SiteTests
{
    private static readonly DateTime Day = GameClock.DefaultStart.Date;

    private static void RunUntil(FestivalGame game, double hour)
    {
        var until = Day.AddHours(hour);
        while (game.Now < until)
        {
            game.Clock.Step();
        }
    }

    /// <summary>Puts a village gate in the middle of the gap in the hedge, with fence closing the rest.</summary>
    private static void CloseTheGap(FestivalGame game, GateKind kind = GateKind.VillageGate)
    {
        var gap = game.Field.Gap;
        var y = gap.Y + 1;
        Assert.True(game.BuildFence(FenceKind.Rope, new TilePoint(gap.X, y), new TilePoint(gap.Right - 1, y)).Ok);
        var width = Catalogue.Of(kind).Width;
        Assert.True(game.BuildGate(kind, gap.X + ((gap.Width - width) / 2), y, false).Ok);
    }

    [Fact]
    public void Everything_in_the_catalogue_has_a_name_a_size_and_a_price()
    {
        Assert.Equal(3, Catalogue.Stages.Count);
        Assert.Equal(4, Catalogue.Fences.Count);
        Assert.Equal(2, Catalogue.Gates.Count);
        Assert.All(Enum.GetValues<StageKind>(), k => Assert.Equal(k, Catalogue.Of(k).Kind));
        Assert.All(Enum.GetValues<FenceKind>(), k => Assert.Equal(k, Catalogue.Of(k).Kind));
        Assert.All(Enum.GetValues<GateKind>(), k => Assert.Equal(k, Catalogue.Of(k).Kind));
        Assert.All(Catalogue.Stages, s => Assert.True(s.Cost > 0 && s.Width > 0 && s.Depth > 0 && s.Name.Length > 0));
        Assert.All(Catalogue.Gates, g => Assert.True(g.Cost > 0 && g.Lanes >= 1 && g.SecondsPerPerson > 0 && g.Width <= 12));
        Assert.True(Catalogue.Stages.Select(s => s.Cost).SequenceEqual(Catalogue.Stages.Select(s => s.Cost).OrderBy(c => c)));
    }

    [Fact]
    public void A_first_festival_can_afford_any_one_stage_a_gate_and_some_fence()
    {
        foreach (var stage in Catalogue.Stages)
        {
            Assert.True(stage.Cost + Catalogue.Of(GateKind.VillageGate).Cost + SiteLayout.FenceCost(FenceKind.Rope, 100) <= FestivalGame.StartingMoney);
        }
    }

    [Fact]
    public void A_stage_costs_money_blocks_the_ground_and_gives_the_money_back_when_removed()
    {
        var game = new FestivalGame();
        var f = game.Field.Field;
        var result = game.BuildStage(StageKind.Bandstand, f.X + 40, f.Y + 6, Facing.South);
        Assert.True(result.Ok);
        Assert.Equal(400, result.Cost);
        Assert.Equal(FestivalGame.StartingMoney - 400, game.Money);
        Assert.False(game.Field.IsWalkable(f.X + 41, f.Y + 7));
        Assert.Equal(new TileRect(f.X + 40, f.Y + 6, 12, 11), game.Site.Stages[0].Rect);

        Assert.Equal(400, game.Remove(new TilePoint(f.X + 45, f.Y + 10)));
        Assert.Equal(FestivalGame.StartingMoney, game.Money);
        Assert.True(game.Field.IsWalkable(f.X + 41, f.Y + 7));
        Assert.Empty(game.Site.Stages);
    }

    [Fact]
    public void A_stage_turned_side_on_swaps_its_width_and_depth()
    {
        var stage = new PlacedStage(StageKind.FarmTrailer, 10, 10, Facing.East);
        Assert.Equal(new TileRect(10, 10, 6, 16), stage.Rect);
    }

    [Fact]
    public void Things_cannot_be_built_on_the_hedge_the_lane_or_each_other()
    {
        var game = new FestivalGame();
        var f = game.Field.Field;
        Assert.False(game.CheckStage(StageKind.Bandstand, f.X - 2, f.Y + 6, Facing.South).Ok);
        Assert.False(game.CheckStage(StageKind.Bandstand, game.Field.Lane.X + 5, game.Field.Lane.Y, Facing.South).Ok);
        Assert.False(game.CheckFence(FenceKind.Rope, new TilePoint(f.X - 1, f.Y + 5), new TilePoint(f.X + 5, f.Y + 5)).Ok);

        Assert.True(game.BuildStage(StageKind.Bandstand, f.X + 40, f.Y + 6, Facing.South).Ok);
        Assert.False(game.CheckStage(StageKind.FarmTrailer, f.X + 45, f.Y + 8, Facing.South).Ok);
        Assert.False(game.CheckFence(FenceKind.Rope, new TilePoint(f.X + 30, f.Y + 8), new TilePoint(f.X + 60, f.Y + 8)).Ok);
        Assert.False(game.CheckGate(GateKind.VillageGate, f.X + 38, f.Y + 8, false).Ok);
        Assert.NotNull(game.CheckStage(StageKind.FarmTrailer, f.X + 45, f.Y + 8, Facing.South).Problem);
    }

    [Fact]
    public void Nothing_is_built_without_the_money_for_it()
    {
        var game = new FestivalGame();
        var f = game.Field.Field;
        Assert.True(game.BuildStage(StageKind.HiredStage, f.X + 10, f.Y + 6, Facing.South).Ok);
        Assert.Equal(500, game.Money);
        var second = game.BuildStage(StageKind.HiredStage, f.X + 50, f.Y + 6, Facing.South);
        Assert.False(second.Ok);
        Assert.Contains("£1,500", second.Problem);
        Assert.Single(game.Site.Stages);
        Assert.Equal(500, game.Money);
    }

    [Fact]
    public void Fence_is_priced_by_the_metre_and_only_new_fence_is_charged()
    {
        var game = new FestivalGame();
        var f = game.Field.Field;
        var first = game.BuildFence(FenceKind.SteelMesh, new TilePoint(f.X + 10, f.Y + 20), new TilePoint(f.X + 29, f.Y + 20));
        Assert.Equal(80, first.Cost); // 20 tiles is 10 m at £8
        var overlap = game.BuildFence(FenceKind.SteelMesh, new TilePoint(f.X + 20, f.Y + 20), new TilePoint(f.X + 39, f.Y + 20));
        Assert.Equal(40, overlap.Cost);
        Assert.Equal(30, game.Site.FenceTiles);
        Assert.Equal(FestivalGame.StartingMoney - 120, game.Money);
        Assert.False(game.CheckFence(FenceKind.Rope, new TilePoint(f.X + 12, f.Y + 20), new TilePoint(f.X + 15, f.Y + 20)).Ok);
        Assert.False(game.Field.IsWalkable(f.X + 15, f.Y + 20));

        Assert.Equal(120, game.Remove(new TilePoint(f.X + 15, f.Y + 20)));
        Assert.Equal(0, game.Site.FenceTiles);
        Assert.Equal(FestivalGame.StartingMoney, game.Money);
    }

    [Fact]
    public void A_dragged_fence_follows_the_longer_way()
    {
        Assert.Equal(11, SiteLayout.Line(new TilePoint(0, 0), new TilePoint(10, 3)).Count());
        Assert.All(SiteLayout.Line(new TilePoint(0, 0), new TilePoint(10, 3)), p => Assert.Equal(0, p.Y));
        Assert.All(SiteLayout.Line(new TilePoint(5, 9), new TilePoint(4, 2)), p => Assert.Equal(5, p.X));
        Assert.Single(SiteLayout.Line(new TilePoint(5, 9), new TilePoint(5, 9)));
    }

    [Fact]
    public void Fence_is_grouped_into_straight_runs_that_cover_every_tile_once()
    {
        var game = new FestivalGame();
        var f = game.Field.Field;
        game.BuildFence(FenceKind.Paling, new TilePoint(f.X + 10, f.Y + 10), new TilePoint(f.X + 20, f.Y + 10));
        game.BuildFence(FenceKind.Paling, new TilePoint(f.X + 20, f.Y + 10), new TilePoint(f.X + 20, f.Y + 18));
        game.BuildFence(FenceKind.Rope, new TilePoint(f.X + 21, f.Y + 10), new TilePoint(f.X + 25, f.Y + 10));
        var runs = game.Site.FenceRuns();
        Assert.Equal(3, runs.Count);
        Assert.Equal(game.Site.FenceTiles, runs.Sum(r => r.Length));
        Assert.Contains(runs, r => r is { Kind: FenceKind.Paling, Vertical: true, Length: 8 });
        Assert.Contains(runs, r => r is { Kind: FenceKind.Rope, Vertical: false, Length: 5 });
    }

    [Fact]
    public void With_nothing_built_the_field_is_open_and_people_walk_in_unchecked()
    {
        var game = new FestivalGame(seed: 11);
        Assert.True(game.Site.OpenWayIn);
        Assert.False(game.Site.HasWorkingGate);
        RunUntil(game, 11);
        Assert.True(game.Crowd.WalkedInFree > 20);
        Assert.Equal(0, game.Crowd.ThroughGates);
        Assert.Equal(0, game.Crowd.Queuing);
    }

    [Fact]
    public void A_gate_on_its_own_is_walked_round_until_the_fence_is_closed()
    {
        var game = new FestivalGame();
        var gap = game.Field.Gap;
        Assert.True(game.BuildGate(GateKind.VillageGate, gap.X + 2, gap.Y + 1, false).Ok);
        Assert.False(game.Site.GateUses[0].InUse);
        Assert.True(game.Site.OpenWayIn);

        game.BuildFence(FenceKind.Rope, new TilePoint(gap.X, gap.Y + 1), new TilePoint(gap.X + 1, gap.Y + 1));
        game.BuildFence(FenceKind.Rope, new TilePoint(gap.X + 10, gap.Y + 1), new TilePoint(gap.X + 11, gap.Y + 1));
        Assert.True(game.Site.GateUses[0].InUse);
        Assert.Equal((0, 1), (game.Site.GateUses[0].OutX, game.Site.GateUses[0].OutY));
        Assert.False(game.Site.OpenWayIn);
        Assert.Equal(Side.Inside, game.Site.SideOf(new TilePoint(gap.X + 5, gap.Y - 10)));
        Assert.Equal(Side.Outside, game.Site.SideOf(game.Field.LaneStart));
    }

    [Fact]
    public void Putting_a_gate_in_a_fence_makes_the_opening_and_pays_back_the_fence_it_replaces()
    {
        var game = new FestivalGame();
        CloseTheGap(game);
        Assert.Equal(4, game.Site.FenceTiles);
        // 12 tiles of rope cost £6, the gate £40, and 8 tiles of rope (£4) came back.
        Assert.Equal(FestivalGame.StartingMoney - 6 - 40 + 4, game.Money);
        Assert.True(game.Site.HasWorkingGate);
    }

    [Theory]
    [InlineData(GateKind.VillageGate)]
    [InlineData(GateKind.ArchGate)]
    public void With_a_gate_everyone_queues_has_a_ticket_checked_and_ends_up_inside(GateKind kind)
    {
        var game = new FestivalGame(seed: 21);
        CloseTheGap(game);
        if (kind == GateKind.ArchGate)
        {
            game.Remove(new TilePoint(game.Field.Gap.X + 5, game.Field.Gap.Y + 1));
            game.Remove(new TilePoint(game.Field.Gap.X, game.Field.Gap.Y + 1));
            game.Remove(new TilePoint(game.Field.Gap.Right - 1, game.Field.Gap.Y + 1));
            Assert.True(game.BuildGate(kind, game.Field.Gap.X, game.Field.Gap.Y + 1, false).Ok);
            Assert.True(game.Site.GateUses[0].InUse);
        }

        var sawQueue = false;
        var until = Day.AddHours(13.5);
        while (game.Now < until)
        {
            game.Clock.Step();
            sawQueue |= game.Crowd.Queuing > 0;
            foreach (var party in game.Crowd.Parties.Where(p => p.State is PartyState.Standing or PartyState.Strolling))
            {
                Assert.True(party.Ticketed);
            }
        }

        Assert.True(sawQueue);
        Assert.Equal(0, game.Crowd.WalkedInFree);
        Assert.Equal(game.Crowd.Expected, game.Crowd.ThroughGates);
        Assert.Equal(0, game.Crowd.Queuing);
        Assert.All(game.Crowd.Parties.Where(p => p.State == PartyState.Standing), p => Assert.Equal(Side.Inside, game.Site.SideOf(FieldMap.TileAt(p.X, p.Y))));

        // And they all get home again through the gate.
        RunUntil(game, 23.9);
        Assert.Equal(0, game.Crowd.OnSite);
    }

    [Fact]
    public void A_slow_gate_builds_a_queue_and_two_lanes_share_it()
    {
        var village = new FestivalGame(seed: 21);
        CloseTheGap(village);
        var arch = new FestivalGame(seed: 21);
        var gap = arch.Field.Gap;
        Assert.True(arch.BuildGate(GateKind.ArchGate, gap.X, gap.Y + 1, false).Ok);

        var villageWorst = 0;
        var archWorst = 0;
        var bothLanes = false;
        var until = Day.AddHours(11);
        while (village.Now < until)
        {
            village.Clock.Step();
            arch.Clock.Step();
            villageWorst = Math.Max(villageWorst, village.Crowd.Queuing);
            archWorst = Math.Max(archWorst, arch.Crowd.Queuing);
            bothLanes |= arch.Crowd.QueueLength(0, 0) > 0 && arch.Crowd.QueueLength(0, 1) > 0;
        }

        Assert.True(villageWorst >= archWorst);
        Assert.True(bothLanes);
    }

    [Fact]
    public void People_in_a_queue_stand_in_a_line_outside_the_gate_facing_it()
    {
        var game = new FestivalGame(seed: 21);
        CloseTheGap(game);
        var gap = game.Field.Gap;
        var gateY = (gap.Y + 1.5) * FieldMap.TileMetres;
        var checkedSome = false;
        var until = Day.AddHours(11);
        while (game.Now < until)
        {
            game.Clock.Step();
            foreach (var party in game.Crowd.Parties.Where(p => p.State == PartyState.Queuing && !p.IsWalking))
            {
                checkedSome = true;
                Assert.True(party.Y > gateY);
                Assert.InRange(party.X, gap.X * 0.5, gap.Right * 0.5);
                Assert.Equal(Math.PI, Math.Abs(party.Heading), 3);
            }
        }

        Assert.True(checkedSome);
    }

    [Fact]
    public void If_the_way_in_is_fenced_shut_people_wait_outside_then_come_in_when_a_gate_opens()
    {
        var game = new FestivalGame(seed: 21);
        var gap = game.Field.Gap;
        game.BuildFence(FenceKind.Paling, new TilePoint(gap.X, gap.Y + 1), new TilePoint(gap.Right - 1, gap.Y + 1));
        Assert.False(game.Site.OpenWayIn);
        RunUntil(game, 10);
        Assert.True(game.Crowd.Queuing > 10);
        Assert.Equal(0, game.Crowd.InField);

        Assert.True(game.BuildGate(GateKind.VillageGate, gap.X + 2, gap.Y + 1, false).Ok);
        RunUntil(game, 11.5);
        Assert.True(game.Crowd.ThroughGates > 10);
        Assert.True(game.Crowd.InField > 10);
    }

    [Fact]
    public void Building_on_top_of_people_moves_them_aside_and_nobody_ends_up_stuck_in_a_stage()
    {
        var game = new FestivalGame(seed: 11);
        RunUntil(game, 12);
        var f = game.Field.Field;
        Assert.True(game.BuildStage(StageKind.HiredStage, f.X + 30, f.Y + 20, Facing.South).Ok);
        Assert.All(game.Crowd.Parties.Where(p => p.IsOnSite), p => Assert.True(game.Field.IsWalkable(FieldMap.TileAt(p.X, p.Y))));
        RunUntil(game, 14);
        Assert.All(game.Crowd.Parties.Where(p => p.IsOnSite), p => Assert.True(game.Field.IsWalkable(FieldMap.TileAt(p.X, p.Y))));
        RunUntil(game, 23.9);
        Assert.Equal(0, game.Crowd.OnSite);
    }

    [Fact]
    public void A_save_keeps_what_was_built_the_money_and_the_queue()
    {
        var game = new FestivalGame(seed: 21);
        CloseTheGap(game);
        var f = game.Field.Field;
        game.BuildStage(StageKind.FarmTrailer, f.X + 50, f.Y + 8, Facing.West);
        while (game.Crowd.Queuing < 4)
        {
            game.Clock.Step();
        }

        var copy = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));
        Assert.Equal(game.Money, copy.Money);
        Assert.Equal(game.Site.Stages, copy.Site.Stages);
        Assert.Equal(game.Site.Gates, copy.Site.Gates);
        Assert.Equal(game.Site.FenceRuns(), copy.Site.FenceRuns());
        Assert.Equal(game.Crowd.Queuing, copy.Crowd.Queuing);

        RunUntil(game, 14);
        RunUntil(copy, 14);
        Assert.Equal(game.Crowd.ThroughGates, copy.Crowd.ThroughGates);
        Assert.Equal(
            game.Crowd.Parties.Select(p => (p.State, Math.Round(p.X, 6), Math.Round(p.Y, 6))),
            copy.Crowd.Parties.Select(p => (p.State, Math.Round(p.X, 6), Math.Round(p.Y, 6))));
    }

    [Fact]
    public void An_older_save_with_nothing_built_still_loads_with_the_starting_money()
    {
        var game = new FestivalGame(seed: 3);
        var old = game.Save() with { Version = 2, Site = null, Money = null };
        var loaded = FestivalGame.Load(SaveData.FromJson(old.ToJson()));
        Assert.Equal(FestivalGame.StartingMoney, loaded.Money);
        Assert.Empty(loaded.Site.Stages);
        Assert.True(loaded.Site.OpenWayIn);
    }
}
