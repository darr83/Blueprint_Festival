using System.Text.Json;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

public class CampKindsTests
{
    /// <summary>Marks the three dearer kinds of ground in strips along the camp's north edge, clear of the fire lanes.</summary>
    private static void MarkExtras(FestivalGame game)
    {
        var camp = CampLayoutTests.CampOf(game);
        Assert.True(game.MarkCamp(new TilePoint(camp.X + 2, camp.Y + 2), new TilePoint(camp.X + 70, camp.Y + 40), CampZone.ReadyPitched).Ok);
        Assert.True(game.MarkCamp(new TilePoint(camp.X + 90, camp.Y + 2), new TilePoint(camp.X + 150, camp.Y + 40), CampZone.Campervan).Ok);
        Assert.True(game.MarkCamp(new TilePoint(camp.X + 170, camp.Y + 2), new TilePoint(camp.X + 230, camp.Y + 40), CampZone.Glamping).Ok);
    }

    [Fact]
    public void Each_way_to_camp_has_its_own_price_and_dearer_means_fewer_buyers()
    {
        foreach (var kind in CampLayout.Extras)
        {
            Assert.True(CampLayout.ShareAt(kind, CampLayout.UsualPrice(kind) / 2) > CampLayout.ShareAt(kind, CampLayout.UsualPrice(kind)));
            Assert.True(CampLayout.ShareAt(kind, CampLayout.UsualPrice(kind) * 2) < CampLayout.ShareAt(kind, CampLayout.UsualPrice(kind)));
            Assert.Equal(CampLayout.UsualShare(kind), CampLayout.ShareAt(kind, CampLayout.UsualPrice(kind)), 6);
        }

        var game = CampLayoutTests.BuildDay();
        var before = game.Wanting(CampKind.Glamping);
        Assert.True(before > 0);
        game.SetCampKindPrice(CampKind.Glamping, 400);
        Assert.Equal(400, game.CampKindPrice(CampKind.Glamping));
        Assert.True(game.Wanting(CampKind.Glamping) < before);
        game.SetCampKindPrice(CampKind.Glamping, 9999);
        Assert.Equal(CampLayout.DearestPrice(CampKind.Glamping), game.CampKindPrice(CampKind.Glamping));
    }

    [Fact]
    public void Without_ground_marked_for_them_everyone_brings_their_own_tent()
    {
        var game = CampLayoutTests.BuildDay();
        Assert.True(game.Wanting(CampKind.ReadyPitched) > 0);
        foreach (var kind in CampLayout.Extras)
        {
            Assert.Equal(0, game.Booked(kind));
            Assert.Equal(0, game.ExtraPitches(kind));
        }

        Assert.Contains(game.CampChecks(), c => c.Text.StartsWith("Ready pitched:", StringComparison.Ordinal) && c.Text.Contains("Mark ground"));
    }

    [Fact]
    public void Buyers_pitch_on_their_own_ground_pay_for_it_and_the_ground_is_set_up_on_the_day()
    {
        var game = CampLayoutTests.BuildDay();
        CampLayoutTests.OpenAWayIn(game);
        CampLayoutTests.LayFireLanes(game);
        MarkExtras(game);
        var plan = game.Crowd.PlanPitches();
        foreach (var kind in CampLayout.Extras)
        {
            var zone = CampLayout.ZoneFor(kind);
            Assert.True(game.ExtraPitches(kind) > 0, $"No room marked for {kind}.");
            Assert.True(game.Booked(kind) > 0, $"Nobody booked {kind}.");
            // Only buyers pitch on ground kept for a dearer way to camp.
            Assert.All(plan.Where(p => game.Site.CampZoneAt(p.Pitch) == zone), p => Assert.Equal(kind, game.Crowd.BoughtKind(p.Party)));
        }

        Assert.Contains(game.CampChecks(), c => c.Text.StartsWith("Glamping:", StringComparison.Ordinal) && c.Text.Contains("booked"));
        var bells = game.ExtraPitches(CampKind.Glamping);
        game.OpenGates();
        Assert.Contains(game.Spending, l => l.What == "Bell tents for glamping" && l.Amount == bells * CampLayout.SetUpCost(CampKind.Glamping));
        Assert.Contains(game.Spending, l => l.What == "Hardstanding for campervans");
        Assert.Contains(game.Spending, l => l.What == "Tents put up for ready pitched");

        // Ready-pitched and bell tents stand before anyone arrives; campervans drive in.
        Assert.Contains(game.Crowd.TentsUp, t => t.Kind == CampKind.Glamping);
        Assert.DoesNotContain(game.Crowd.TentsUp, t => t.Kind == CampKind.Campervan);

        // Their ground is fixed once the gates are open.
        var camp = CampLayoutTests.CampOf(game);
        Assert.False(game.MarkCamp(new TilePoint(camp.X + 2, camp.Y + 2), new TilePoint(camp.X + 10, camp.Y + 10), CampZone.General).Ok);

        while (game.Now.TimeOfDay < TimeSpan.FromHours(15))
        {
            game.Clock.Step();
        }

        foreach (var kind in CampLayout.Extras)
        {
            var arrived = game.Crowd.Parties.Count(p => p.Camper && p.Ticketed == true && game.Crowd.KindOf(p) == kind);
            Assert.True(arrived > 0, $"No {kind} guests arrived.");
            Assert.Contains(game.Income, l => l.What == FestivalGame.TakingsFor(kind) && l.Amount == arrived * game.CampKindPrice(kind));
        }

        Assert.Contains(game.Crowd.TentsUp, t => t.Kind == CampKind.Campervan);
    }

    [Fact]
    public void Camping_prices_are_saved_and_loaded()
    {
        var game = CampLayoutTests.BuildDay();
        game.SetCampKindPrice(CampKind.ReadyPitched, 70);
        game.SetCampKindPrice(CampKind.Campervan, 25);
        var loaded = FestivalGame.Load(JsonSerializer.Deserialize<SaveData>(JsonSerializer.Serialize(game.Save()))!);
        Assert.Equal(70, loaded.CampKindPrice(CampKind.ReadyPitched));
        Assert.Equal(25, loaded.CampKindPrice(CampKind.Campervan));
        Assert.Equal(CampLayout.UsualPrice(CampKind.Glamping), loaded.CampKindPrice(CampKind.Glamping));
        Assert.Equal(game.Wanting(CampKind.Campervan), loaded.Wanting(CampKind.Campervan));
    }
}
