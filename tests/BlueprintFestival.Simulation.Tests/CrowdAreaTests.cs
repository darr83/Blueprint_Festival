using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

/// <summary>Crowd areas: pens the player draws in front of a stage, with a limit on how many get in.</summary>
public class CrowdAreaTests
{
    /// <summary>A plain field with a hired stage facing south and nothing else.</summary>
    private static FestivalGame Field()
    {
        var game = new FestivalGame(seed: 5);
        var f = game.Field.Field;
        Assert.True(game.BuildStage(StageKind.Bandstand, f.X + 50, f.Y + 14, Facing.South).Ok);
        return game;
    }

    /// <summary>The box for a pen across the front of the first stage, a metre out, this many tiles deep.</summary>
    private static (TilePoint From, TilePoint To) FrontBox(FestivalGame game, int deep = 16)
    {
        var r = game.Site.Stages[0].Rect;
        return (new TilePoint(r.X - 2, r.Bottom + 2), new TilePoint(r.Right + 1, r.Bottom + 1 + deep));
    }

    [Fact]
    public void A_drawn_area_gets_a_barrier_round_it_with_gaps_at_the_back_and_lets_in_two_a_square_metre()
    {
        var game = Field();
        var (from, to) = FrontBox(game);
        var money = game.Money;
        var check = game.BuildArea(AreaKind.FrontPen, from, to);
        Assert.True(check.Ok, check.Problem);

        var area = Assert.Single(game.Site.Areas);
        Assert.Equal(AreaKind.FrontPen, area.Kind);
        Assert.Equal(CrowdAreas.Suggested(area.InsideMetres), area.Limit);
        Assert.Equal((int)Math.Floor(area.InsideMetres * 2), area.Limit);

        // Barrier on every edge tile except the gaps, all paid for at the crowd barrier price.
        var gapTiles = area.Gaps.SelectMany(g => SiteLayout.GapTilesOf(area, g)).ToHashSet();
        var edge = Enumerable.Range(area.Rect.X, area.Rect.Width).SelectMany(x => Enumerable.Range(area.Rect.Y, area.Rect.Height).Select(y => new TilePoint(x, y))).Where(area.IsEdge).ToList();
        Assert.Equal(edge.Count - gapTiles.Count, game.Site.AreaBarriers.Count);
        Assert.Equal(SiteLayout.FenceCost(FenceKind.CrowdBarrier, game.Site.AreaBarriers.Count), money - game.Money);
        Assert.All(game.Site.AreaBarriers, p => Assert.False(game.Field.IsWalkable(p)));
        Assert.All(gapTiles, p => Assert.True(game.Field.IsWalkable(p)));

        // The stage faces south, so the gaps are on the south side, away from it.
        Assert.Equal(2, area.Gaps.Count);
        Assert.All(area.Gaps, g => Assert.Equal(area.Rect.Bottom - 1, g.Y));
        Assert.Equal(0, game.Site.AreaInside(new TilePoint(area.Rect.X + 3, area.Rect.Y + 3)));
        Assert.Equal(-1, game.Site.AreaInside(area.Rect.Y == 0 ? default : new TilePoint(area.Rect.X, area.Rect.Y)));
        Assert.Equal(0, game.Site.StageOfArea(area));
    }

    [Fact]
    public void Areas_must_be_big_enough_and_cannot_overlap_or_sit_on_the_stage()
    {
        var game = Field();
        var (from, to) = FrontBox(game);
        Assert.False(game.CheckArea(AreaKind.FrontPen, from, new TilePoint(from.X + 3, from.Y + 3)).Ok);
        Assert.True(game.BuildArea(AreaKind.FrontPen, from, to).Ok);
        Assert.False(game.CheckArea(AreaKind.Standing, new TilePoint(from.X + 4, from.Y + 4), new TilePoint(to.X + 10, to.Y + 10)).Ok);
        var r = game.Site.Stages[0].Rect;
        Assert.False(game.CheckArea(AreaKind.Standing, new TilePoint(r.X, r.Y), new TilePoint(r.X + 10, r.Y + 10)).Ok);
    }

    [Fact]
    public void A_pen_drawn_against_a_front_barrier_shares_it_and_its_own_front_edge_counts_as_one()
    {
        var game = Field();
        var r = game.Site.Stages[0].Rect;
        var (from, to) = FrontBox(game);
        Assert.Equal(0, game.BarrierDepth(game.Site.Stages[0]));
        Assert.True(game.BuildArea(AreaKind.FrontPen, from, to).Ok);
        Assert.True(game.BarrierDepth(game.Site.Stages[0]) > 0, "The pen's front edge should act as the stage's front barrier.");

        // Drawn over a crowd barrier already there, the pen leaves that fence where it is and does not pay for it again.
        var other = Field();
        Assert.True(other.BuildFence(FenceKind.CrowdBarrier, new TilePoint(r.X - 2, r.Bottom + 2), new TilePoint(r.Right + 1, r.Bottom + 2)).Ok);
        var before = other.Money;
        Assert.True(other.BuildArea(AreaKind.FrontPen, from, to).Ok);
        Assert.Equal(FenceKind.CrowdBarrier, other.Site.FenceAt(new TilePoint(r.X, r.Bottom + 2)));
        Assert.False(other.Site.IsAreaBarrier(new TilePoint(r.X, r.Bottom + 2)));
        Assert.True(before - other.Money < SiteLayout.FenceCost(FenceKind.CrowdBarrier, game.Site.AreaBarriers.Count));
    }

    [Fact]
    public void Gaps_can_be_added_and_closed_but_an_area_keeps_one_way_in()
    {
        var game = Field();
        var (from, to) = FrontBox(game);
        Assert.True(game.BuildArea(AreaKind.FrontPen, from, to).Ok);
        var area = game.Site.Areas[0];

        // A new gap half way down the east side.
        var side = new TilePoint(area.Rect.Right - 1, area.Rect.Y + (area.Rect.Height / 2));
        Assert.Null(game.ToggleAreaGap(side));
        Assert.Equal(3, game.Site.Areas[0].Gaps.Count);
        Assert.True(game.Field.IsWalkable(side));
        Assert.NotNull(game.ToggleAreaGap(new TilePoint(area.Rect.X, area.Rect.Y)));

        // Closing them all but one.
        foreach (var gap in game.Site.Areas[0].Gaps.ToList().Skip(1))
        {
            Assert.Null(game.ToggleAreaGap(gap));
        }

        Assert.Single(game.Site.Areas[0].Gaps);
        Assert.NotNull(game.ToggleAreaGap(game.Site.Areas[0].Gaps[0]));
        Assert.Single(game.Site.Areas[0].Gaps);
    }

    [Fact]
    public void The_limit_can_be_set_up_to_six_a_square_metre_and_taking_the_area_down_pays_the_barrier_back()
    {
        var game = Field();
        var (from, to) = FrontBox(game);
        var money = game.Money;
        Assert.True(game.BuildArea(AreaKind.FrontPen, from, to).Ok);
        var metres = game.Site.Areas[0].InsideMetres;
        Assert.Equal(40, game.SetAreaLimit(0, 40));
        Assert.Equal(CrowdAreas.Most(metres), game.SetAreaLimit(0, 100000));
        Assert.Equal(5, game.SetAreaLimit(0, 0));
        Assert.Null(game.SetAreaLimit(3, 10));
        Assert.Equal("Comfortable", CrowdAreas.Feel(CrowdAreas.Suggested(metres), metres));
        Assert.Equal("Busy", CrowdAreas.Feel(CrowdAreas.Suggested(metres) + 1, metres));
        Assert.Equal("Crush risk", CrowdAreas.Feel(CrowdAreas.CrushAt(metres) + 1, metres));

        game.Remove(new TilePoint(from.X + 4, from.Y + 4));
        Assert.Empty(game.Site.Areas);
        Assert.Empty(game.Site.AreaBarriers);
        Assert.Equal(money, game.Money);
    }

    [Fact]
    public void Areas_are_kept_in_the_save_with_their_limits_and_gaps()
    {
        var game = Field();
        var (from, to) = FrontBox(game);
        Assert.True(game.BuildArea(AreaKind.Vip, from, to).Ok);
        game.SetAreaLimit(0, 33);
        var save = game.Site.Save();

        var restored = new SiteLayout(game.Field);
        restored.Restore(save);
        var area = Assert.Single(restored.Areas);
        Assert.Equal(AreaKind.Vip, area.Kind);
        Assert.Equal(33, area.Limit);
        Assert.Equal(game.Site.Areas[0].Gaps, area.Gaps);
        Assert.Equal(game.Site.AreaBarriers.Count, restored.AreaBarriers.Count);
    }

    [Fact]
    public void Only_VIP_guests_can_walk_into_a_VIP_area()
    {
        var game = Field();
        var (from, to) = FrontBox(game);
        Assert.True(game.BuildArea(AreaKind.Vip, from, to).Ok);
        var inside = new TilePoint(from.X + 4, from.Y + 4);
        Assert.False(game.Field.IsWalkable(inside));
        Assert.True(game.Field.IsWalkableForVip(inside));
    }

    /// <summary>The biggest name on the farm trailer with a small pen in front: how full does the pen get?</summary>
    private static (int Most, int MostWaiting, bool Filled) BigNameWithPen(bool security, int limit)
    {
        // As the safety tests' big day, but with the first aid money spent on the pen and its security instead.
        var game = FestivalGame.NewFestival("Larkmoor Festival");
        game.ChooseHeadliner(game.Headliners.ToList().FindIndex(h => h.Stars == game.Headliners.Max(x => x.Stars)));
        Assert.True(game.ConfirmPlan());
        var gateGap = game.Field.Gap;
        var gy = gateGap.Y + 1;
        Assert.True(game.BuildFence(FenceKind.Rope, new TilePoint(gateGap.X, gy), new TilePoint(gateGap.Right - 1, gy)).Ok);
        Assert.True(game.BuildGate(GateKind.VillageGate, gateGap.X, gy, false).Ok);
        var f = game.Field.Field;
        Assert.True(game.BuildStage(StageKind.FarmTrailer, f.X + 50, f.Y + 6, Facing.South).Ok);
        Amenities.Add(game);
        Assert.True(game.HireStaff(StaffKind.TicketStaff, new TilePoint(gateGap.X + 1, gy)).Ok);
        var r = game.Site.Stages[0].Rect;
        var from = new TilePoint(r.X - 2, r.Bottom + 2);
        var to = new TilePoint(r.Right + 1, r.Bottom + 17);
        Assert.True(game.BuildArea(AreaKind.FrontPen, from, to).Ok);
        game.SetAreaLimit(0, limit);
        if (security)
        {
            foreach (var gap in game.Site.Areas[0].Gaps)
            {
                var hired = game.HireStaff(StaffKind.Security, new TilePoint(gap.X, gap.Y + 2));
                Assert.True(hired.Ok, hired.Problem + $" money {game.Money}");
            }
        }

        var filled = false;
        game.AreaFull += _ => filled = true;
        var most = 0;
        var waiting = 0;
        game.OpenGates();
        var guard = 0;
        while (game.Phase == Phase.Day)
        {
            game.Clock.Step();
            if (game.Now.Second == 0)
            {
                var inside = game.Crowd.Parties.Where(p => p.State == PartyState.Watching && game.Site.AreaInside(FieldMap.TileAt(p.X, p.Y)) == 0).Sum(p => p.Members.Count);
                most = Math.Max(most, inside);
                waiting = Math.Max(waiting, game.Crowd.WaitingAt(0));
            }

            Assert.True(++guard < 70000, "The day never ended.");
        }

        return (most, waiting, filled);
    }

    [Fact]
    public void Security_on_the_gaps_keep_a_pen_to_its_limit_and_keen_fans_wait_outside()
    {
        var (most, waiting, filled) = BigNameWithPen(security: true, limit: 24);
        Assert.True(filled, "The pen should fill up for the biggest name.");
        Assert.True(most <= 24 + 4, $"No more than the limit should be let in, give or take a family: {most}.");
        Assert.True(most >= 16, $"The pen should fill nearly to its limit: {most}.");
        Assert.True(waiting > 0, "Some keen fans should wait by the gaps.");
    }

    [Fact]
    public void With_nobody_on_the_gaps_the_limit_is_not_kept()
    {
        var (most, _, filled) = BigNameWithPen(security: false, limit: 24);
        Assert.False(filled);
        Assert.True(most > 24 + 4, $"Without security more than the limit should pour in: {most}.");
    }
}
