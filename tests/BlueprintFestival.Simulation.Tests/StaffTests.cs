using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

public class StaffTests
{
    private static TilePoint GateTile(FestivalGame game) => new(game.Field.Gap.X + 1, game.Field.Gap.Y + 1);

    /// <summary>A planned festival on its build day with a gate, a trailer stage, food, drink and toilets, and no staff yet.</summary>
    private static FestivalGame BuildDay(string name = "Larkmoor Festival")
    {
        var game = FestivalGame.NewFestival(name);
        game.ChooseHeadliner(0);
        Assert.True(game.ConfirmPlan());
        var gap = game.Field.Gap;
        var y = gap.Y + 1;
        Assert.True(game.BuildFence(FenceKind.Rope, new TilePoint(gap.X, y), new TilePoint(gap.Right - 1, y)).Ok);
        Assert.True(game.BuildGate(GateKind.VillageGate, gap.X, y, false).Ok);
        var f = game.Field.Field;
        Assert.True(game.BuildStage(StageKind.FarmTrailer, f.X + 50, f.Y + 6, Facing.South).Ok);
        Amenities.Add(game);
        return game;
    }

    private static void RunTheDay(FestivalGame game)
    {
        game.OpenGates();
        var guard = 0;
        while (game.Phase == Phase.Day)
        {
            game.Clock.Step();
            Assert.True(++guard < 70000, "The day never ended.");
        }
    }

    [Fact]
    public void There_are_three_kinds_of_staff_each_with_a_wage()
    {
        Assert.Equal(3, Catalogue.Staff.Count);
        Assert.All(Enum.GetValues<StaffKind>(), k => Assert.Equal(k, Catalogue.Of(k).Kind));
        Assert.All(Catalogue.Staff, s => Assert.True(s.Wage > 0 && s.Name.Length > 0 && s.About.Length > 0));
        Assert.Null(Catalogue.Of(StallKind.FirstAidTent).Serves);
    }

    [Fact]
    public void Ticket_staff_go_on_a_gate_one_to_a_lane_and_are_paid_for_the_day()
    {
        var game = BuildDay();
        var before = game.Money;
        var field = new TilePoint(game.Field.Field.X + 40, game.Field.Field.Y + 40);

        Assert.False(game.HireStaff(StaffKind.TicketStaff, field).Ok);
        Assert.Equal(0, game.OpenLanes(0));
        Assert.True(game.HireStaff(StaffKind.TicketStaff, GateTile(game)).Ok);
        Assert.Equal(1, game.OpenLanes(0));
        Assert.Equal(before - 40, game.Money);
        Assert.Contains(game.Spending, l => l.What == "Staff wages" && l.Amount == 40);

        // A village gate has one lane, so there is no room for a second.
        Assert.False(game.HireStaff(StaffKind.TicketStaff, GateTile(game)).Ok);
        Assert.Single(game.Staff);

        // Sent home on the build day, the wage comes back. Once the gates are open it does not.
        Assert.Equal(40, game.DismissStaff(game.Staff[0]));
        Assert.Equal(before, game.Money);
        Assert.True(game.HireStaff(StaffKind.TicketStaff, GateTile(game)).Ok);
        game.OpenGates();
        Assert.Equal(0, game.DismissStaff(game.Staff[0]));
        Assert.Empty(game.Staff);
        Assert.Equal(before - 40, game.Money);
    }

    [Fact]
    public void Security_and_first_aiders_are_posted_on_clear_ground_one_to_a_tile()
    {
        var game = BuildDay();
        var f = game.Field.Field;
        var spot = new TilePoint(f.X + 40, f.Y + 40);

        Assert.True(game.HireStaff(StaffKind.Security, spot).Ok);
        Assert.False(game.HireStaff(StaffKind.FirstAider, spot).Ok);
        Assert.False(game.HireStaff(StaffKind.Security, new TilePoint(f.X + 52, f.Y + 8)).Ok);
        Assert.False(game.HireStaff(StaffKind.Security, new TilePoint(f.X - 3, f.Y + 8)).Ok);
        Assert.Same(game.Staff[0], game.StaffAt(spot));
        Assert.Equal(StaffState.AtPost, game.Staff[0].State);
        Assert.Equal(FieldMap.CentreOf(spot), (game.Staff[0].X, game.Staff[0].Y));
    }

    [Fact]
    public void With_nobody_on_the_gate_nobody_gets_in_and_people_give_up_and_go_home()
    {
        var game = BuildDay();
        game.OpenGates();
        while (game.Now < game.Day.AddHours(11))
        {
            game.Clock.Step();
        }

        Assert.Equal(0, game.Crowd.ThroughGates);
        Assert.True(game.Crowd.WentHomeUnhappy > 20, $"Only {game.Crowd.WentHomeUnhappy} gave up.");
        Assert.DoesNotContain(game.Income, l => l.What == "Tickets");

        // Someone is put on the gate at last, and the people still waiting come in.
        Assert.True(game.HireStaff(StaffKind.TicketStaff, GateTile(game)).Ok);
        while (game.Now < game.Day.AddHours(12))
        {
            game.Clock.Step();
        }

        Assert.True(game.Crowd.ThroughGates > 0);
    }

    [Fact]
    public void A_festival_with_nothing_to_plan_needs_no_staff()
    {
        var game = new FestivalGame();
        var gap = game.Field.Gap;
        game.BuildFence(FenceKind.Rope, new TilePoint(gap.X, gap.Y + 1), new TilePoint(gap.Right - 1, gap.Y + 1));
        game.BuildGate(GateKind.VillageGate, gap.X, gap.Y + 1, false);
        Amenities.Add(game);
        while (game.Now < game.Day.AddHours(18))
        {
            game.Clock.Step();
        }

        Assert.False(game.NeedsStaff);
        Assert.True(game.Crowd.ThroughGates > 50);
        Assert.Empty(game.Incidents);
        Assert.Equal(0, game.Untreated + game.TroubleIgnored);
        Assert.Null(game.Save().Staff);
    }

    [Fact]
    public void First_aiders_go_to_people_taken_ill_and_a_staffed_day_leaves_nobody_untreated()
    {
        var game = BuildDay();
        var f = game.Field.Field;
        Assert.True(game.HireStaff(StaffKind.TicketStaff, GateTile(game)).Ok);
        Assert.True(game.BuildStall(StallKind.FirstAidTent, f.X + 30, f.Y + 6, Facing.South).Ok);
        Assert.True(game.HireStaff(StaffKind.FirstAider, new TilePoint(f.X + 34, f.Y + 14)).Ok);
        Assert.True(game.HireStaff(StaffKind.FirstAider, new TilePoint(f.X + 70, f.Y + 40)).Ok);
        Assert.True(game.HireStaff(StaffKind.Security, new TilePoint(f.X + 56, f.Y + 20)).Ok);
        Assert.True(game.HireStaff(StaffKind.Security, new TilePoint(f.X + 90, f.Y + 30)).Ok);

        game.OpenGates();
        var sawGoing = false;
        var sawResting = false;
        var sawUnwell = false;
        var guard = 0;
        while (game.Phase == Phase.Day)
        {
            game.Clock.Step();
            sawGoing |= game.Staff.Any(s => s.Kind == StaffKind.FirstAider && s.State == StaffState.Going);
            sawResting |= game.Incidents.Any(i => i.Resting);
            sawUnwell |= game.Crowd.People.Any(p => p.Unwell);
            Assert.True(++guard < 70000, "The day never ended.");
        }

        Assert.True(sawGoing && sawUnwell);
        Assert.InRange(game.Treated, 2, 20);
        Assert.Equal(0, game.Untreated);
        Assert.True(game.TroubleSorted >= 1);
        Assert.Equal(0, game.TroubleIgnored);
        Assert.Empty(game.Incidents);
        Assert.All(game.Staff, s => Assert.Equal(StaffState.AtPost, s.State));
        Assert.DoesNotContain(game.Crowd.Parties, p => p.State == PartyState.Unwell);

        game.PayScouts();
        var result = game.HandBack();
        Assert.Equal(game.Treated, result.Treated);
        Assert.Equal(0, result.Untreated);
        Assert.Equal(400, result.LicenceNext);
    }

    [Fact]
    public void With_no_first_aid_people_are_left_untreated_and_the_licence_does_not_grow()
    {
        var game = BuildDay();
        Assert.True(game.HireStaff(StaffKind.TicketStaff, GateTile(game)).Ok);
        RunTheDay(game);

        Assert.True(game.Untreated >= 2, $"Only {game.Untreated} untreated.");
        Assert.Equal(0, game.Treated);
        Assert.True(game.TroubleIgnored >= 1);
        Assert.Equal(0, game.TroubleSorted);
        Assert.True(game.Crowd.WentHomeUnhappy >= game.Untreated);

        game.PayScouts();
        var result = game.HandBack();
        Assert.Equal(game.Untreated, result.Untreated);
        Assert.Equal(result.LicenceNow, result.LicenceNext);
        Assert.Contains("first aid", result.Verdict);
    }

    [Fact]
    public void A_day_with_staff_saves_and_loads_and_plays_out_the_same()
    {
        var game = BuildDay();
        var f = game.Field.Field;
        Assert.True(game.HireStaff(StaffKind.TicketStaff, GateTile(game)).Ok);
        Assert.True(game.BuildStall(StallKind.FirstAidTent, f.X + 30, f.Y + 6, Facing.South).Ok);
        Assert.True(game.HireStaff(StaffKind.FirstAider, new TilePoint(f.X + 34, f.Y + 14)).Ok);
        Assert.True(game.HireStaff(StaffKind.Security, new TilePoint(f.X + 56, f.Y + 20)).Ok);
        game.OpenGates();

        // Stop at a moment when something is going on.
        var guard = 0;
        while (!(game.Incidents.Count > 0 && game.Staff.Any(s => s.State != StaffState.AtPost)) || game.Now < game.Day.AddHours(13))
        {
            game.Clock.Step();
            Assert.True(++guard < 50000, "Nothing ever happened.");
        }

        var loaded = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));

        Assert.Equal(game.Staff.Select(s => (s.Id, s.Kind, s.Tile, s.State, s.Incident)), loaded.Staff.Select(s => (s.Id, s.Kind, s.Tile, s.State, s.Incident)));
        Assert.Equal(game.Incidents.Select(i => (i.Id, i.Kind, i.Party, i.Staff, i.Left, i.Resting)), loaded.Incidents.Select(i => (i.Id, i.Kind, i.Party, i.Staff, i.Left, i.Resting)));
        Assert.Equal(game.OpenLanes(0), loaded.OpenLanes(0));
        for (var i = 0; i < 7200; i++)
        {
            game.Clock.Step();
            loaded.Clock.Step();
        }

        Assert.Equal((game.Treated, game.Untreated, game.TroubleSorted, game.TroubleIgnored), (loaded.Treated, loaded.Untreated, loaded.TroubleSorted, loaded.TroubleIgnored));
        Assert.Equal(game.Money, loaded.Money);
        Assert.Equal(game.Crowd.Happiness, loaded.Crowd.Happiness);
        Assert.Equal(game.Staff.Select(s => (s.State, Math.Round(s.X, 3), Math.Round(s.Y, 3))), loaded.Staff.Select(s => (s.State, Math.Round(s.X, 3), Math.Round(s.Y, 3))));
    }
}
