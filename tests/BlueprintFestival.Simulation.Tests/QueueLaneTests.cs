using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

/// <summary>Queue lanes the player drags out, with barriers that go up by themselves, and ground the public are kept off.</summary>
public class QueueLaneTests
{
    private static readonly DateTime Day = GameClock.DefaultStart.Date;

    /// <summary>A tea table with a lane running straight out from its counter, sixteen tiles (8 m) long.</summary>
    private static (FestivalGame Game, TilePoint Front, TilePoint Back) WithLane(string name = "Lane End", int seed = 8)
    {
        var game = new FestivalGame(name, seed: seed);
        var f = game.Field.Field;
        Assert.True(game.BuildStall(StallKind.Fries, f.X + 40, f.Y + 20, Facing.South).Ok);
        var front = new TilePoint(f.X + 43, f.Y + 28);
        var back = new TilePoint(f.X + 43, f.Y + 43);
        Assert.True(game.BuildLane(front, back).Ok);
        return (game, front, back);
    }

    [Fact]
    public void Dragging_a_lane_puts_a_barrier_down_both_sides_and_leaves_the_ends_open()
    {
        var game = new FestivalGame(seed: 4);
        var f = game.Field.Field;
        var money = game.Money;
        var from = new TilePoint(f.X + 20, f.Y + 20);
        var to = new TilePoint(f.X + 31, f.Y + 20);
        var check = game.CheckLane(from, to);
        Assert.True(check.Ok);
        Assert.Equal(SiteLayout.LaneCost(12), check.Cost);
        Assert.Equal(36, check.Cost);
        Assert.True(game.BuildLane(from, to).Ok);
        Assert.Equal(money - 36, game.Money);

        var site = game.Site;
        Assert.Single(site.Lanes);
        Assert.Equal(12, site.Lanes[0].Count);
        for (var x = from.X; x <= to.X; x++)
        {
            Assert.True(site.IsLane(new TilePoint(x, from.Y)));
            Assert.Contains(new TilePoint(x, from.Y - 1), site.LaneBarriers);
            Assert.Contains(new TilePoint(x, from.Y + 1), site.LaneBarriers);
        }

        // Both ends are open, and nothing sticks out past them.
        Assert.Equal(24, site.LaneBarriers.Count);
        Assert.True(game.Field.IsWalkable(new TilePoint(from.X - 1, from.Y)));
        Assert.True(game.Field.IsWalkable(new TilePoint(to.X + 1, to.Y)));

        // Nobody walks through the barrier, or through the lane as a short cut.
        Assert.False(game.Field.IsWalkable(new TilePoint(from.X + 3, from.Y - 1)));
        Assert.False(game.Field.IsWalkable(new TilePoint(from.X + 3, from.Y)));

        // Nothing else can be built on it, and too short a lane is refused.
        Assert.False(game.CheckStall(StallKind.Bar, from.X, from.Y - 3, Facing.South).Ok);
        Assert.False(game.CheckFence(FenceKind.Rope, new TilePoint(from.X + 2, from.Y - 4), new TilePoint(from.X + 2, from.Y + 4)).Ok);
        Assert.False(game.CheckLane(new TilePoint(f.X + 20, f.Y + 40), new TilePoint(f.X + 21, f.Y + 40)).Ok);

        // Taking it down, from the lane or from its barrier, takes the lot and gives the money back.
        Assert.Equal(36, site.RefundAt(new TilePoint(from.X + 3, from.Y - 1)));
        Assert.Equal(36, game.Remove(new TilePoint(from.X + 5, from.Y)));
        Assert.Empty(site.Lanes);
        Assert.Empty(site.LaneBarriers);
        Assert.Equal(money, game.Money);
        Assert.True(game.Field.IsWalkable(new TilePoint(from.X + 3, from.Y)));
    }

    [Fact]
    public void A_lane_is_carried_on_round_corners_and_its_rows_share_the_barrier_between_them()
    {
        var (game, front, back) = WithLane();
        var site = game.Site;

        // Two tiles to the side, then back up beside the first row.
        Assert.True(game.BuildLane(back, new TilePoint(back.X + 2, back.Y)).Ok);
        Assert.True(game.BuildLane(new TilePoint(back.X + 2, back.Y), new TilePoint(back.X + 2, front.Y + 2)).Ok);
        Assert.Single(site.Lanes);
        Assert.Equal(16 + 2 + 13, site.Lanes[0].Count);
        Assert.Equal(front, site.Lanes[0][0]);
        Assert.Equal(new TilePoint(back.X + 2, front.Y + 2), site.Lanes[0][^1]);
        for (var i = 1; i < site.Lanes[0].Count; i++)
        {
            var (a, b) = (site.Lanes[0][i - 1], site.Lanes[0][i]);
            Assert.Equal(1, Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y));
        }

        // One line of barrier stands between the two rows.
        for (var y = front.Y + 3; y < back.Y; y++)
        {
            Assert.Contains(new TilePoint(back.X + 1, y), site.LaneBarriers);
        }

        // A row hard up against another has no room for a barrier.
        var other = new FestivalGame(seed: 4);
        var f = other.Field.Field;
        Assert.True(other.BuildLane(new TilePoint(f.X + 20, f.Y + 20), new TilePoint(f.X + 30, f.Y + 20)).Ok);
        var tight = other.CheckLane(new TilePoint(f.X + 20, f.Y + 21), new TilePoint(f.X + 30, f.Y + 21));
        Assert.False(tight.Ok);
        Assert.True(other.CheckLane(new TilePoint(f.X + 20, f.Y + 22), new TilePoint(f.X + 30, f.Y + 22)).Ok);
    }

    [Fact]
    public void A_stalls_queue_forms_in_the_lane_laid_in_front_of_it()
    {
        var (game, front, back) = WithLane();
        var line = game.Crowd.StallLine(0)!;

        // The places run down the middle of the lane from the counter end, a step apart, then on past the open end.
        Assert.Equal(10, line.Fenced);
        Assert.Equal(FieldMap.CentreOf(front), line.Places[0]);
        for (var i = 0; i < line.Fenced; i++)
        {
            Assert.True(game.Site.IsLane(FieldMap.TileAt(line.Places[i].X, line.Places[i].Y)));
        }

        Assert.True(line.Places.Count > line.Fenced + 20);
        for (var i = line.Fenced; i < line.Places.Count; i++)
        {
            Assert.True(game.Field.IsWalkable(FieldMap.TileAt(line.Places[i].X, line.Places[i].Y)));
        }

        Assert.Equal(FieldMap.CentreOf(new TilePoint(back.X, back.Y + 1)), line.Mouth);

        var inLane = 0;
        var served = false;
        while (game.Now < Day.AddHours(17))
        {
            game.Clock.Step();
            served |= game.Crowd.BeingServed(0) > 0;
            foreach (var party in game.Crowd.Parties)
            {
                if (!party.IsOnSite)
                {
                    continue;
                }

                var at = FieldMap.TileAt(party.X, party.Y);
                if (party.State == PartyState.AtStall && party.Server >= 0)
                {
                    // Stepping up from the front of the lane to be served.
                }
                else if (party.State == PartyState.AtStall && party.InLine)
                {
                    // Queuing: in the lane, or in the tail beyond its open end. Never the wrong side of the barrier.
                    inLane += game.Site.IsLane(at) ? 1 : 0;
                    Assert.DoesNotContain(at, game.Site.LaneBarriers);
                }
                else
                {
                    Assert.False(game.Site.IsLane(at), $"party {party.Id} ({party.State}) was in the lane without queuing");
                }
            }
        }

        Assert.True(served);
        Assert.True(inLane > 500, $"people were seen queuing in the lane {inLane} times");
        Assert.True(game.TotalIncome > 0);
    }

    [Fact]
    public void A_lane_too_far_from_any_stall_or_gate_is_left_alone()
    {
        var game = new FestivalGame(seed: 8);
        var f = game.Field.Field;
        Assert.True(game.BuildStall(StallKind.Fries, f.X + 40, f.Y + 20, Facing.South).Ok);
        Assert.True(game.BuildLane(new TilePoint(f.X + 70, f.Y + 50), new TilePoint(f.X + 84, f.Y + 50)).Ok);
        var line = game.Crowd.StallLine(0)!;
        Assert.Equal(0, line.Fenced);
        Assert.Null(line.Mouth);
        Assert.True(line.Places.Count >= 60);
    }

    [Fact]
    public void A_gates_queue_forms_in_the_lane_laid_outside_it()
    {
        var game = new FestivalGame(seed: 21);
        var gap = game.Field.Gap;
        var y = gap.Y + 1;
        Assert.True(game.BuildFence(FenceKind.Rope, new TilePoint(gap.X, y), new TilePoint(gap.Right - 1, y)).Ok);
        var width = Catalogue.Of(GateKind.VillageGate).Width;
        var gx = gap.X + ((gap.Width - width) / 2);
        Assert.True(game.BuildGate(GateKind.VillageGate, gx, y, false).Ok);
        var use = game.Site.GateUses[0];
        Assert.True(use.InUse);
        Assert.Equal(1, use.OutY);

        // A lane straight out from the middle of the gate and down the track, the way people arrive.
        var mid = gx + (width / 2);
        var front = new TilePoint(mid, y + 2);
        var back = new TilePoint(mid, y + 13);
        var check = game.BuildLane(front, back);
        Assert.True(check.Ok, check.Problem);
        var line = game.Crowd.GateLine(0, 0)!;
        Assert.True(line.Fenced >= 2);
        Assert.Equal(FieldMap.CentreOf(front), line.Places[0]);

        var inLane = 0;
        while (game.Now < Day.AddHours(12))
        {
            game.Clock.Step();
            inLane += game.Crowd.Parties.Count(p => p.State == PartyState.Queuing && p.InLine && game.Site.IsLane(FieldMap.TileAt(p.X, p.Y)));
        }

        Assert.True(inLane > 100, $"people were seen queuing in the lane {inLane} times");
        Assert.True(game.Crowd.ThroughGates >= 20, $"{game.Crowd.ThroughGates} came through");
        Assert.Equal(0, game.Crowd.WalkedInFree);
    }

    [Fact]
    public void The_public_keep_off_ground_marked_no_customers_and_staff_do_not()
    {
        var game = new FestivalGame("Keep Off", seed: 8);
        var f = game.Field.Field;
        var money = game.Money;
        var from = new TilePoint(f.X + 30, f.Y + 30);
        var to = new TilePoint(f.X + 59, f.Y + 49);
        Assert.True(game.KeepOut(from, to, true).Ok);
        Assert.Equal(money, game.Money);
        Assert.Equal(30 * 20, game.Site.KeepOut.Count);
        Assert.True(game.Site.IsKeptClear(new TilePoint(f.X + 40, f.Y + 40)));
        Assert.False(game.Field.IsWalkable(new TilePoint(f.X + 40, f.Y + 40)));
        Assert.True(game.Field.IsWalkableForStaff(new TilePoint(f.X + 40, f.Y + 40)));
        Assert.False(game.CheckKeepOut(from, to, true).Ok);

        // The public walk round it; staff walk straight across.
        var left = new TilePoint(f.X + 25, f.Y + 40);
        var right = new TilePoint(f.X + 64, f.Y + 40);
        var round = new PathFinder(game.Field).FindPath(left, right)!;
        Assert.True(round.Count > 2);
        Assert.Equal(2, new PathFinder(game.Field, staff: true).FindPath(left, right)!.Count);
        Assert.True(game.HireStaff(StaffKind.Security, new TilePoint(f.X + 40, f.Y + 40)).Ok);

        var seen = 0;
        while (game.Now < Day.AddHours(15))
        {
            game.Clock.Step();
            foreach (var party in game.Crowd.Parties)
            {
                if (party.IsOnSite)
                {
                    seen++;
                    Assert.False(game.Site.IsKeptClear(FieldMap.TileAt(party.X, party.Y)), $"party {party.Id} ({party.State}) was on ground kept clear");
                }
            }
        }

        Assert.True(seen > 1000);

        // Something built on marked ground takes the mark's place; the rest can be cleared again.
        Assert.True(game.BuildStall(StallKind.Bar, from.X, from.Y, Facing.South).Ok);
        Assert.False(game.Site.IsKeptClear(from));
        Assert.True(game.KeepOut(from, to, false).Ok);
        Assert.Empty(game.Site.KeepOut);
        Assert.True(game.Field.IsWalkable(new TilePoint(f.X + 40, f.Y + 40)));
    }

    [Fact]
    public void Lanes_and_ground_kept_clear_survive_saving_and_loading()
    {
        var (game, front, back) = WithLane("Saved", 8);
        var f = game.Field.Field;
        Assert.True(game.BuildLane(back, new TilePoint(back.X + 2, back.Y)).Ok);
        Assert.True(game.KeepOut(new TilePoint(f.X + 10, f.Y + 10), new TilePoint(f.X + 15, f.Y + 13), true).Ok);
        while (game.Now < Day.AddHours(17) && game.Crowd.StallLine(0)!.Standing < 5)
        {
            game.Clock.Step();
        }

        Assert.True(game.Crowd.StallLine(0)!.Standing >= 5);
        var loaded = FestivalGame.Load(game.Save());
        Assert.Equal(game.Site.Lanes[0], loaded.Site.Lanes[0]);
        Assert.Equal(game.Site.LaneBarriers.OrderBy(p => p.X).ThenBy(p => p.Y), loaded.Site.LaneBarriers.OrderBy(p => p.X).ThenBy(p => p.Y));
        Assert.Equal(24, loaded.Site.KeepOut.Count);
        Assert.Equal(game.Crowd.StallLine(0)!.Fenced, loaded.Crowd.StallLine(0)!.Fenced);
        Assert.Equal(game.Crowd.StallLine(0)!.Standing, loaded.Crowd.StallLine(0)!.Standing);
        var before = game.Crowd.People.OrderBy(p => p.Id).ToList();
        var after = loaded.Crowd.People.OrderBy(p => p.Id).ToList();
        Assert.Equal(before.Count, after.Count);
        for (var i = 0; i < before.Count; i++)
        {
            Assert.Equal(before[i].X, after[i].X, 6);
            Assert.Equal(before[i].Y, after[i].Y, 6);
        }
    }
}
