using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

public class MedicalTests
{
    [Fact]
    public void A_medical_tent_has_six_beds_and_gets_people_back_on_their_feet_in_half_the_time()
    {
        var aid = Catalogue.Of(StallKind.FirstAidTent);
        var medical = Catalogue.Of(StallKind.MedicalTent);
        Assert.Equal(2, aid.Servers);
        Assert.Equal(6, medical.Servers);
        Assert.Equal(aid.SecondsPerPerson / 2, medical.SecondsPerPerson);
        Assert.True(medical.Cost > aid.Cost && medical.Width > aid.Width);
        Assert.Null(medical.Serves);
        Assert.True(FestivalGame.HasBeds(StallKind.FirstAidTent) && FestivalGame.HasBeds(StallKind.MedicalTent));
        Assert.False(FestivalGame.HasBeds(StallKind.Bar));

        // Every bed is a place of its own inside the tent, whichever way the tent faces.
        foreach (var facing in Enum.GetValues<Facing>())
        {
            foreach (var kind in new[] { StallKind.FirstAidTent, StallKind.MedicalTent })
            {
                var game = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(2, 400, 20000));
                Assert.True(game.ConfirmPlan());
                var f = game.Field.Field;
                Assert.True(game.BuildStall(kind, f.X + 40, f.Y + 30, facing).Ok);
                var tent = game.Site.Stalls[0];
                var places = Enumerable.Range(0, Catalogue.Of(kind).Servers).Select(bed => FestivalGame.BedPlace(tent, bed)).ToList();
                Assert.Equal(places.Count, places.Distinct().Count());
                Assert.All(places, p => Assert.True(tent.Rect.Contains(FieldMap.TileAt(p.X, p.Y)), $"{kind} facing {facing}: bed at {p} is outside {tent.Rect}."));
                Assert.Equal(places.Count, game.FreeBeds);
            }
        }
    }

    [Fact]
    public void Every_tent_on_the_site_is_used_and_nobody_shares_a_bed()
    {
        var game = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(4, 1500, 60000, false, 80));
        game.ChooseHeadliner(0);
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

        Assert.True(game.BuildStage(StageKind.HiredStage, f.X + (f.Width / 2) - 6, f.Y + 6, Facing.South).Ok);
        var kinds = new[] { StallKind.FoodVan, StallKind.Bar, StallKind.ToiletBlock, StallKind.WaterPoint };
        for (var i = 0; i < 12; i++)
        {
            var east = i % 2 == 0;
            var x = east ? f.Right - 16 - (i / 6 * 40) : f.X + 4 + (i / 6 * 40);
            Assert.True(game.BuildStall(kinds[i % 4], x, f.Y + 44 + (i / 2 % 3 * 30), east ? Facing.West : Facing.East).Ok, $"Stall {i} would not fit.");
        }

        // A first aid tent at one end of the field and a medical tent at the other.
        Assert.True(game.BuildStall(StallKind.FirstAidTent, f.X + 20, f.Y + 6, Facing.South).Ok);
        Assert.True(game.BuildStall(StallKind.MedicalTent, f.Right - 60, f.Y + 6, Facing.South).Ok);
        var tents = game.Site.Stalls.Select((s, i) => (s, i)).Where(t => FestivalGame.HasBeds(t.s.Kind)).Select(t => t.i).ToList();
        Assert.Equal(8, game.FreeBeds);
        for (var i = 0; i < 6; i++)
        {
            Assert.True(game.HireStaff(StaffKind.FirstAider, new TilePoint(f.X + 60 + (i * 17), f.Y + 50 + (i % 2 * 40))).Ok, $"First-aider {i} could not be posted.");
        }

        game.OpenGates();
        var used = new HashSet<int>();
        var fewestFree = 8;
        var guard = 0;
        while (game.Phase == Phase.Day)
        {
            game.Clock.Step();
            if (game.Clock.TickCount % 30 == 0)
            {
                var kept = game.Incidents.Where(i => i.Tent >= 0).Select(i => (i.Tent, i.Bed)).ToList();
                Assert.Equal(kept.Count, kept.Distinct().Count());
                Assert.All(kept, k => Assert.True(tents.Contains(k.Tent) && k.Bed < Catalogue.Of(game.Site.Stalls[k.Tent].Kind).Servers));
                used.UnionWith(kept.Select(k => k.Tent));
                fewestFree = Math.Min(fewestFree, game.FreeBeds);
                Assert.Equal(8 - kept.Count, game.FreeBeds);
            }

            Assert.True(++guard < 70000, "The day never ended.");
        }

        Assert.Equal(2, used.Count);
        Assert.True(fewestFree < 8);
        Assert.True(game.Treated > 10, $"{game.Treated} treated.");
        Assert.Equal(8, game.FreeBeds);
    }

    [Fact]
    public void Someone_resting_in_a_bed_is_still_in_it_after_saving_and_loading()
    {
        var game = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(3, 800, 30000, false, 80));
        game.ChooseHeadliner(0);
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

        Assert.True(game.BuildStall(StallKind.MedicalTent, f.X + 60, f.Y + 6, Facing.South).Ok);
        for (var i = 0; i < 5; i++)
        {
            Assert.True(game.HireStaff(StaffKind.FirstAider, new TilePoint(f.X + 30 + (i * 31), f.Y + 50)).Ok);
        }

        game.OpenGates();
        var guard = 0;
        while (!game.Incidents.Any(i => i.Resting && i.Tent >= 0))
        {
            game.Clock.Step();
            Assert.True(++guard < 60000 && game.Phase == Phase.Day, "Nobody was ever put to bed.");
        }

        var loaded = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));
        Assert.Equal(game.Incidents.Select(i => (i.Id, i.Tent, i.Bed, i.Resting, i.Left)), loaded.Incidents.Select(i => (i.Id, i.Tent, i.Bed, i.Resting, i.Left)));
        Assert.Equal(game.FreeBeds, loaded.FreeBeds);
    }
}
