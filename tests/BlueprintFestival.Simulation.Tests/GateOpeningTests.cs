using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

/// <summary>The day starts at eight with the gates shut; people queue outside until the player opens them.</summary>
public class GateOpeningTests
{
    /// <summary>
    /// A planned festival on its build day: a village gate in the middle of the gap with a queue
    /// lane laid straight out from it, a stage, food, drink and toilets, and ticket staff on the gate.
    /// </summary>
    private static FestivalGame BuildDay(int ticketStaff = 1, bool lane = true, string name = "Larkmoor Festival")
    {
        var game = FestivalGame.NewFestival(name);
        game.ChooseHeadliner(0);
        Assert.True(game.ConfirmPlan());
        var gap = game.Field.Gap;
        var y = gap.Y + 1;
        Assert.True(game.BuildFence(FenceKind.Rope, new TilePoint(gap.X, y), new TilePoint(gap.Right - 1, y)).Ok);
        var width = Catalogue.Of(GateKind.VillageGate).Width;
        var gx = gap.X + ((gap.Width - width) / 2);
        Assert.True(game.BuildGate(GateKind.VillageGate, gx, y, false).Ok);
        var f = game.Field.Field;
        Assert.True(game.BuildStage(StageKind.FarmTrailer, f.X + 50, f.Y + 6, Facing.South).Ok);
        Amenities.Add(game);
        if (lane)
        {
            var out_ = game.Site.GateUses[0].OutY;
            var mid = gx + (width / 2);
            var check = game.BuildLane(new TilePoint(mid, y + (2 * out_)), new TilePoint(mid, y + (13 * out_)));
            Assert.True(check.Ok, check.Problem);
            Assert.True(game.Crowd.GateLine(0, 0)!.Fenced >= 2);
        }

        for (var i = 0; i < ticketStaff; i++)
        {
            Assert.True(game.HireStaff(StaffKind.TicketStaff, new TilePoint(gx + 1, y)).Ok);
        }

        return game;
    }

    private static void RunUntil(FestivalGame game, double hour)
    {
        while (game.Phase == Phase.Day && game.Now.TimeOfDay < TimeSpan.FromHours(hour))
        {
            game.Clock.Step();
        }
    }

    [Fact]
    public void The_day_starts_at_eight_with_the_gates_shut_and_people_queue_in_the_lane_outside()
    {
        var game = BuildDay();
        game.StartDay();
        Assert.Equal(Phase.Day, game.Phase);
        Assert.True(game.GatesShut);
        Assert.Null(game.GatesOpenedAt);
        Assert.Equal(TimeSpan.FromHours(8), game.Now.TimeOfDay);

        var inLane = 0;
        var tickets = 0;
        while (game.Now.TimeOfDay < TimeSpan.FromHours(9))
        {
            game.Clock.Step();
            inLane += game.Crowd.Parties.Count(p => p.State == PartyState.Queuing && p.InLine && game.Site.IsLane(FieldMap.TileAt(p.X, p.Y)));
            tickets += game.Crowd.People.Count(p => p.Ticket);
        }

        // Nobody is let in, nobody is shown holding up a ticket, and the queue stands in the barriers.
        Assert.Equal(0, game.Crowd.ThroughGates);
        Assert.Equal(0, tickets);
        Assert.True(game.Crowd.Queuing >= 10, $"{game.Crowd.Queuing} queuing outside");
        Assert.True(inLane > 1000, $"people were seen queuing in the lane {inLane} times");

        // Opened, ticket staff start checking straight away.
        game.OpenGates();
        Assert.False(game.GatesShut);
        Assert.Equal(game.Now, game.GatesOpenedAt);
        RunUntil(game, 9.25);
        Assert.True(game.Crowd.ThroughGates >= 10, $"{game.Crowd.ThroughGates} came through");
        Assert.Equal(0, game.Crowd.WalkedInFree);
    }

    [Fact]
    public void A_second_person_checking_tickets_on_the_lane_lets_people_in_much_faster()
    {
        int ThroughIn20Minutes(int staff)
        {
            var game = BuildDay(staff);
            game.StartDay();
            Assert.Equal(staff, game.Checkers(0, 0));
            RunUntil(game, 10);
            Assert.True(game.Crowd.Queuing >= 60, $"{game.Crowd.Queuing} queuing outside by ten");
            game.OpenGates();
            RunUntil(game, 10 + (20 / 60.0));
            return game.Crowd.ThroughGates;
        }

        var one = ThroughIn20Minutes(1);
        var two = ThroughIn20Minutes(2);

        // A village gate checks one ticket every 30 seconds: about 40 people in 20 minutes. A second
        // checker halves the checking; the shuffle up to the table between parties stays the same.
        Assert.InRange(one, 30, 45);
        Assert.True(two >= one * 1.5, $"one checker let {one} in, two let {two} in");
    }

    [Fact]
    public void People_left_outside_shut_gates_for_hours_get_fed_up_and_go_home()
    {
        var shut = BuildDay();
        shut.StartDay();
        RunUntil(shut, 11.5);
        Assert.True(shut.GatesShut);
        Assert.Equal(0, shut.Crowd.ThroughGates);
        var fedUp = shut.Crowd.Parties.Where(p => p.LeftUnhappy).Sum(p => p.Members.Count);
        Assert.True(fedUp > 0, "nobody gave up waiting");

        var open = BuildDay();
        open.StartDay();
        open.OpenGates();
        RunUntil(open, 11.5);
        Assert.Equal(0, open.Crowd.Parties.Where(p => p.LeftUnhappy).Sum(p => p.Members.Count));
        Assert.True(open.Crowd.ThroughGates > 50);
    }

    [Fact]
    public void Opening_the_gates_on_the_build_day_starts_the_day_with_them_open()
    {
        var game = BuildDay();
        game.OpenGates();
        Assert.Equal(Phase.Day, game.Phase);
        Assert.False(game.GatesShut);
        Assert.Equal(TimeSpan.FromHours(8), game.GatesOpenedAt!.Value.TimeOfDay);
    }

    [Fact]
    public void The_gates_stay_shut_through_saving_and_loading()
    {
        var game = BuildDay();
        game.StartDay();
        RunUntil(game, 8.5);
        var queuing = game.Crowd.Queuing;
        var loaded = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));
        Assert.True(loaded.GatesShut);
        Assert.Equal(queuing, loaded.Crowd.Queuing);
        RunUntil(loaded, 8.75);
        Assert.Equal(0, loaded.Crowd.ThroughGates);

        loaded.OpenGates();
        var again = FestivalGame.Load(SaveData.FromJson(loaded.Save().ToJson()));
        Assert.False(again.GatesShut);
        Assert.Equal(loaded.GatesOpenedAt, again.GatesOpenedAt);
    }
}
