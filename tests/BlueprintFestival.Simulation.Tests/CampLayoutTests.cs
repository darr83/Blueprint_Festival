using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

public class CampLayoutTests
{
    /// <summary>A festival of 5,000 on the estate, with the showground taken for camping, on its build day.</summary>
    internal static FestivalGame BuildDay()
    {
        var plan = new FieldPlan(new[] { new FieldChoice("home", FieldUse.Music), new FieldChoice("ten", FieldUse.Food), new FieldChoice("show", FieldUse.Camping), new FieldChoice("glebe", FieldUse.CarPark) });
        var game = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(6, 5000, 90000)).WithFields(plan)!;
        game.HireBuses(FestivalGame.MostBuses);
        Assert.True(game.CampingBooked);
        Assert.True(game.ConfirmPlan());
        Assert.Equal(Phase.Build, game.Phase);
        return game;
    }

    /// <summary>The grass of the camping field.</summary>
    private static TileRect CampOf(FestivalGame game) => game.Field.CampFields.Single();

    /// <summary>Fire lanes up and across the camp, 40 m apart, close enough together for every tent.</summary>
    internal static void LayFireLanes(FestivalGame game)
    {
        var camp = CampOf(game);
        for (var x = camp.X + 80; x < camp.Right - 40; x += 80)
        {
            Assert.True(game.MarkCamp(new TilePoint(x, camp.Y), new TilePoint(x + 5, camp.Bottom - 1), CampZone.FireLane).Ok);
        }

        for (var y = camp.Y + 80; y < camp.Bottom - 40; y += 80)
        {
            Assert.True(game.MarkCamp(new TilePoint(camp.X, y), new TilePoint(camp.Right - 1, y + 5), CampZone.FireLane).Ok);
        }
    }

    /// <summary>A fence across the way in with turnstiles in it, and ticket staff on every lane.</summary>
    private static void OpenAWayIn(FestivalGame game)
    {
        var gap = game.Field.Gap;
        var vertical = gap.Height > gap.Width;
        var row = Catalogue.Of(GateKind.TurnstileRow);
        var (from, to) = vertical ? (new TilePoint(gap.X + 1, gap.Y), new TilePoint(gap.X + 1, gap.Bottom - 1)) : (new TilePoint(gap.X, gap.Y + 1), new TilePoint(gap.Right - 1, gap.Y + 1));
        Assert.True(game.BuildFence(FenceKind.SteelMesh, from, to).Ok);
        var length = vertical ? gap.Height : gap.Width;
        for (var along = 0; along + row.Width <= length; along += row.Width)
        {
            var (x, y) = vertical ? (from.X, gap.Y + along) : (gap.X + along, from.Y);
            Assert.True(game.BuildGate(GateKind.TurnstileRow, x, y, vertical).Ok);
            for (var lane = 0; lane < row.Lanes; lane++)
            {
                var step = (lane * row.Width / row.Lanes) + 1;
                Assert.True(game.HireStaff(StaffKind.TicketStaff, vertical ? new TilePoint(x, y + step) : new TilePoint(x + step, y)).Ok);
            }
        }
    }

    private static TilePoint Free(FestivalGame game, StallKind kind, int x, int y)
    {
        for (var dy = 0; dy < 40; dy += 2)
        {
            for (var dx = 0; dx < 40; dx += 2)
            {
                if (game.Site.CheckStall(kind, x + dx, y + dy, Facing.South).Ok)
                {
                    return new TilePoint(x + dx, y + dy);
                }
            }
        }

        throw new InvalidOperationException($"No room for the {kind} near {x}, {y}.");
    }

    [Fact]
    public void Only_camping_ground_can_be_marked_and_marks_can_be_cleared()
    {
        var game = BuildDay();
        var field = game.Field.Fields.First(f => f.Use == FieldUse.Music).Grass;
        var camp = CampOf(game);

        Assert.False(game.MarkCamp(new TilePoint(field.X + 10, field.Y + 10), new TilePoint(field.X + 30, field.Y + 30), CampZone.Quiet).Ok);
        Assert.Empty(game.Site.CampZones);

        var from = new TilePoint(camp.X + 20, camp.Y + 20);
        var to = new TilePoint(camp.X + 59, camp.Y + 49);
        Assert.True(game.MarkCamp(from, to, CampZone.Family).Ok);
        Assert.Equal(40 * 30, game.Site.CampZones.Count);
        Assert.Equal(CampZone.Family, game.Site.CampZoneAt(new TilePoint(camp.X + 30, camp.Y + 30)));
        Assert.Equal(CampZone.General, game.Site.CampZoneAt(new TilePoint(camp.X + 70, camp.Y + 30)));
        Assert.Equal(0, game.Spending.Where(l => l.What.Contains("amp", StringComparison.Ordinal) && l.What != "Camping field").Sum(l => l.Amount));

        Assert.True(game.MarkCamp(from, to, CampZone.General).Ok);
        Assert.Empty(game.Site.CampZones);
        Assert.False(game.MarkCamp(from, to, CampZone.General).Ok);
    }

    [Fact]
    public void Nothing_is_built_and_no_tent_goes_up_on_a_fire_lane()
    {
        var game = BuildDay();
        var camp = CampOf(game);
        LayFireLanes(game);
        var lane = new TilePoint(camp.X + 82, camp.Y + 40);
        Assert.True(game.Site.IsFireLane(lane));

        var stall = game.CheckStall(StallKind.WaterPoint, lane.X, lane.Y, Facing.South);
        Assert.False(stall.Ok);
        Assert.Equal("Keep the fire lane clear.", stall.Problem);

        game.StartDay();
        var pitched = game.Crowd.Parties.Where(p => p.Pitch is not null).Select(p => p.Pitch!.Value).ToList();
        Assert.True(pitched.Count > 250, $"Only {pitched.Count} tents.");
        foreach (var p in pitched)
        {
            for (var dx = -3; dx <= 3; dx++)
            {
                for (var dy = -3; dy <= 3; dy++)
                {
                    Assert.False(game.Site.IsFireLane(new TilePoint(p.X + dx, p.Y + dy)), $"A tent at {p} is on a fire lane.");
                }
            }
        }
    }

    [Fact]
    public void The_fire_officer_fines_a_camp_without_fire_lanes_and_passes_one_with_them()
    {
        var without = BuildDay();
        Assert.Contains(without.CampChecks(), c => c.State == CampCheckState.NotMet && c.Text.Contains("fire lane", StringComparison.Ordinal));
        without.StartDay();
        Assert.Contains(without.Spending, l => l.What == "Fire officer's fine" && l.Amount == CampLayout.FireFine);
        Assert.Contains(without.DayLog, n => n.Kind == DayNoteKind.Camp && n.Tone == DayTone.Bad);

        var with = BuildDay();
        LayFireLanes(with);
        Assert.Contains(with.CampChecks(), c => c.State == CampCheckState.Met && c.Text.StartsWith("Every tent within", StringComparison.Ordinal));
        with.StartDay();
        Assert.DoesNotContain(with.Spending, l => l.What == "Fire officer's fine");
        Assert.Contains(with.DayLog, n => n.Kind == DayNoteKind.Camp && n.Tone == DayTone.Good);
    }

    [Fact]
    public void Families_pitch_in_family_camping_and_quiet_campers_in_quiet_camping()
    {
        var game = BuildDay();
        var camp = CampOf(game);
        var half = camp.Y + (camp.Height / 2);
        Assert.True(game.MarkCamp(new TilePoint(camp.X, camp.Y), new TilePoint(camp.Right - 1, half - 1), CampZone.Family).Ok);
        Assert.True(game.MarkCamp(new TilePoint(camp.X, half), new TilePoint(camp.Right - 1, camp.Bottom - 1), CampZone.Quiet).Ok);
        game.StartDay();

        var campers = game.Crowd.Parties.Where(p => p.Pitch is not null).ToList();
        var families = campers.Where(p => Crowd.WishOf(p) == CampZone.Family).ToList();
        var quiet = campers.Where(p => Crowd.WishOf(p) == CampZone.Quiet).ToList();
        Assert.NotEmpty(families);
        Assert.NotEmpty(quiet);

        // The family area holds more pitches than there are families, so every family gets one, and nobody else does until the families are in.
        var inFamily = families.Count(p => game.Site.CampZoneAt(p.Pitch!.Value) == CampZone.Family);
        var familyPitches = game.Crowd.OpenPitches().Count(p => p.Zone == CampZone.Family);
        Assert.Equal(Math.Min(families.Count, familyPitches), inFamily);
        Assert.True(quiet.Count(p => game.Site.CampZoneAt(p.Pitch!.Value) == CampZone.Quiet) > quiet.Count / 2);
    }

    [Fact]
    public void Accessible_pitches_have_room_round_them_and_campers_who_need_one_are_unhappy_without_it()
    {
        var game = BuildDay();
        var camp = CampOf(game);
        var needs = game.Crowd.Parties.First(p => p.Pitch is not null && Crowd.WishOf(p) == CampZone.Accessible);
        Assert.True(game.Crowd.CampFeel(needs) <= -Crowd.NoAccess);

        Assert.True(game.MarkCamp(new TilePoint(camp.X + 6, camp.Y + 6), new TilePoint(camp.X + 80, camp.Y + 80), CampZone.Accessible).Ok);
        var roomy = game.Crowd.OpenPitches().Where(p => p.Zone == CampZone.Accessible).Select(p => p.Pitch).ToList();
        Assert.True(roomy.Count >= 10, $"Only {roomy.Count} accessible pitches.");
        for (var i = 0; i < roomy.Count; i++)
        {
            for (var j = i + 1; j < roomy.Count; j++)
            {
                var (dx, dy) = (roomy[i].X - roomy[j].X, roomy[i].Y - roomy[j].Y);
                Assert.True((dx * dx) + (dy * dy) >= CampLayout.AccessibleSpacing * CampLayout.AccessibleSpacing);
            }
        }

        game.StartDay();
        Assert.Equal(CampZone.Accessible, game.Site.CampZoneAt(needs.Pitch!.Value));
        Assert.True(game.Crowd.CampFeel(needs) > -Crowd.NoAccess);
    }

    [Fact]
    public void Toilets_and_water_near_a_tent_spare_its_campers_a_grumble()
    {
        var game = BuildDay();
        game.StartDay();
        var party = game.Crowd.Parties.First(p => p.Pitch is not null && Crowd.WishOf(p) == CampZone.General);
        var bare = game.Crowd.CampFeel(party);
        Assert.Equal(-(Crowd.NoToiletsNear + Crowd.NoWaterNear), bare, 6);

        var pitch = party.Pitch!.Value;
        var loo = Free(game, StallKind.ToiletBlock, pitch.X + 8, pitch.Y + 8);
        Assert.True(game.Site.AddStall(StallKind.ToiletBlock, loo.X, loo.Y, Facing.South).Ok);
        var tap = Free(game, StallKind.WaterPoint, pitch.X - 30, pitch.Y + 8);
        Assert.True(game.Site.AddStall(StallKind.WaterPoint, tap.X, tap.Y, Facing.South).Ok);
        Assert.Equal(0, game.Crowd.CampFeel(party), 6);
    }

    [Fact]
    public void Showers_the_info_point_and_the_camp_shop_only_go_in_a_camping_field()
    {
        var game = BuildDay();
        var field = game.Field.Fields.First(f => f.Use == FieldUse.Music).Grass;
        var camp = CampOf(game);
        foreach (var kind in new[] { StallKind.ShowerBlock, StallKind.CampInfo, StallKind.CampShop })
        {
            Assert.Equal(StallGroup.Camping, Catalogue.GroupOf(kind));
            Assert.NotEmpty(Catalogue.MenuOf(kind));
            Assert.False(game.CheckStall(kind, field.X + 40, field.Y + 40, Facing.South).Ok);
            var at = Free(game, kind, camp.X + 20, camp.Y + 20);
            Assert.True(game.BuildStall(kind, at.X, at.Y, Facing.South).Ok);
        }

        var checks = game.CampChecks();
        Assert.Contains(checks, c => c.Text.StartsWith("Showers: 6 for", StringComparison.Ordinal));
    }

    [Fact]
    public void Camp_marks_are_saved_and_loaded()
    {
        var game = BuildDay();
        var camp = CampOf(game);
        LayFireLanes(game);
        Assert.True(game.MarkCamp(new TilePoint(camp.X + 6, camp.Y + 6), new TilePoint(camp.X + 40, camp.Y + 40), CampZone.Quiet).Ok);

        var loaded = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));
        Assert.Equal(game.Site.CampZones.Count, loaded.Site.CampZones.Count);
        Assert.All(game.Site.CampZones, z => Assert.Equal(z.Value, loaded.Site.CampZoneAt(z.Key)));
    }

    [Fact]
    public void Campers_use_the_showers_the_lockers_and_the_shop()
    {
        var game = BuildDay();
        var camp = CampOf(game);
        OpenAWayIn(game);
        foreach (var (kind, x, y) in new[] { (StallKind.ShowerBlock, 20, 20), (StallKind.ShowerBlock, camp.Width - 60, camp.Height - 40), (StallKind.CampInfo, camp.Width / 2, 30), (StallKind.CampShop, camp.Width / 2, camp.Height - 40) })
        {
            var at = Free(game, kind, camp.X + x, camp.Y + y);
            Assert.True(game.BuildStall(kind, at.X, at.Y, Facing.South).Ok);
        }

        game.OpenGates();
        while (game.Now.TimeOfDay < TimeSpan.FromHours(20))
        {
            game.Clock.Step();
        }

        var campers = game.Crowd.Parties.Where(p => p.Camper && p.Ticketed == true).ToList();
        Assert.True(campers.Count(p => p.Showered) > campers.Count / 10, $"{campers.Count(p => p.Showered)} of {campers.Count} parties showered.");
        Assert.True(campers.Count(p => p.Lockered) > campers.Count / 10, $"{campers.Count(p => p.Lockered)} took a locker.");
        Assert.True(campers.Count(p => p.CampShopped) > campers.Count / 10, $"{campers.Count(p => p.CampShopped)} went to the shop.");
        Assert.DoesNotContain(game.Crowd.Parties, p => !p.Camper && (p.Showered || p.Lockered || p.CampShopped));
        Assert.Contains(game.Income, l => l.What == FestivalGame.TakingsLine(StallKind.ShowerBlock) && l.Amount > 0);
    }
}
