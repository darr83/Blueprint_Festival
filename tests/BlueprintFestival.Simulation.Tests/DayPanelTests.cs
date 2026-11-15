using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

/// <summary>The event day panel, "The day so far": the day's log, the happiness line, and the report it shows.</summary>
public class DayPanelTests
{
    private static TilePoint GateTile(FestivalGame game) => new(game.Field.Gap.X + 1, game.Field.Gap.Y + 1);

    /// <summary>A small festival with a gate, a stage, food, drink, toilets, a first aid tent and staff.</summary>
    private static FestivalGame StaffedDay(bool firstAid = true)
    {
        var game = FestivalGame.NewFestival("Larkmoor Festival");
        game.ChooseHeadliner(0);
        Assert.True(game.ConfirmPlan());
        var gap = game.Field.Gap;
        var y = gap.Y + 1;
        Assert.True(game.BuildFence(FenceKind.Rope, new TilePoint(gap.X, y), new TilePoint(gap.Right - 1, y)).Ok);
        Assert.True(game.BuildGate(GateKind.VillageGate, gap.X, y, false).Ok);
        var f = game.Field.Field;
        Assert.True(game.BuildStage(StageKind.FarmTrailer, f.X + 50, f.Y + 6, Facing.South).Ok);
        Amenities.Add(game);
        Assert.True(game.HireStaff(StaffKind.TicketStaff, GateTile(game)).Ok);
        if (firstAid)
        {
            Assert.True(game.BuildStall(StallKind.FirstAidTent, f.X + 30, f.Y + 6, Facing.South).Ok);
            Assert.True(game.HireStaff(StaffKind.FirstAider, new TilePoint(f.X + 34, f.Y + 14)).Ok);
            Assert.True(game.HireStaff(StaffKind.FirstAider, new TilePoint(f.X + 70, f.Y + 40)).Ok);
            Assert.True(game.HireStaff(StaffKind.Security, new TilePoint(f.X + 56, f.Y + 20)).Ok);
            Assert.True(game.HireStaff(StaffKind.Security, new TilePoint(f.X + 90, f.Y + 30)).Ok);
        }

        return game;
    }

    private static void RunUntil(FestivalGame game, DateTime when)
    {
        while (game.Now < when && game.Phase == Phase.Day)
        {
            game.Clock.Step();
        }
    }

    [Fact]
    public void Before_the_gates_open_the_log_is_empty_and_nothing_needs_a_look_but_the_gates()
    {
        var game = StaffedDay();
        var report = DayReport.Of(game);
        Assert.Empty(report.Log);
        Assert.Empty(report.MoodLine);
        Assert.Equal(0, report.Incidents);

        game.StartDay();
        RunUntil(game, game.Day.AddHours(9.5));
        Assert.Contains(DayReport.Of(game).WorthALook, w => w.Contains("gates are still shut"));
        game.OpenGates();
        var opened = Assert.Single(game.DayLog);
        Assert.Equal(DayNoteKind.Gates, opened.Kind);
        Assert.DoesNotContain(DayReport.Of(game).WorthALook, w => w.Contains("gates"));
    }

    [Fact]
    public void Every_incident_is_logged_with_its_time_and_place_and_how_it_ended()
    {
        var game = StaffedDay();
        game.OpenGates();
        var sawOnTheWay = false;
        var guard = 0;
        while (game.Phase == Phase.Day)
        {
            game.Clock.Step();
            sawOnTheWay |= game.DayLog.Any(n => n.Outcome == "First aider on the way");
            Assert.True(++guard < 70000, "The day never ended.");
        }

        var ill = game.DayLog.Where(n => n.Kind == DayNoteKind.Ill).ToList();
        var trouble = game.DayLog.Where(n => n.Kind is DayNoteKind.Trouble or DayNoteKind.LostChild).ToList();
        Assert.True(sawOnTheWay, "The log should say when a first-aider sets off.");
        Assert.True(ill.Count >= 2, $"Only {ill.Count} taken ill.");
        Assert.All(ill, n => Assert.StartsWith("Someone taken ill", n.What));
        Assert.All(ill, n => Assert.True(n.Incident >= 0));
        Assert.All(ill.Concat(trouble), n => Assert.NotEqual(DayTone.Bad, n.Tone));
        Assert.Equal(game.Treated, ill.Count(n => n.Tone == DayTone.Good));
        Assert.Equal(game.TroubleSorted, trouble.Count(n => n.Tone == DayTone.Good));
        Assert.Contains(ill, n => n.What.Contains(" by the ") || n.What.Contains(" at the front of ") || n.What.EndsWith(" on the field"));

        // Newest first, and every line has a time on the day.
        Assert.Equal(game.DayLog.OrderByDescending(n => n.At).ThenByDescending(n => n.Id).Select(n => n.Id), game.DayLog.Select(n => n.Id));
        Assert.All(game.DayLog, n => Assert.Equal(game.Day.Date, n.At.Date));
    }

    [Fact]
    public void With_nobody_to_help_the_log_shows_people_left_untreated()
    {
        var game = StaffedDay(firstAid: false);
        game.OpenGates();
        RunUntil(game, game.Day.AddHours(24));

        var report = DayReport.Of(game);
        var bad = game.DayLog.Where(n => n.Kind == DayNoteKind.Ill && n.Tone == DayTone.Bad).ToList();
        Assert.Equal(game.Untreated, bad.Count);
        Assert.All(bad, n => Assert.Contains("Nobody came", n.Outcome));
        Assert.Equal(game.TroubleIgnored, game.DayLog.Count(n => n.Kind is DayNoteKind.Trouble or DayNoteKind.LostChild && n.Tone == DayTone.Bad));
        Assert.Equal(game.Untreated, report.Untreated);
    }

    [Fact]
    public void Happiness_is_written_down_every_quarter_of_an_hour_while_people_are_here()
    {
        var game = StaffedDay();
        game.OpenGates();
        RunUntil(game, game.Day.AddHours(16));

        var line = game.MoodLine;
        Assert.True(line.Count >= 20, $"Only {line.Count} readings.");
        Assert.All(line.Zip(line.Skip(1)), pair => Assert.Equal(TimeSpan.FromMinutes(15), pair.Second.At - pair.First.At));
        Assert.All(line, p => Assert.InRange(p.Happy, 0, 100));

        var report = DayReport.Of(game);
        Assert.Equal(game.Crowd.Happiness, report.Happy);
        var twoHoursAgo = line.Last(p => p.At <= game.Now.AddHours(-2));
        Assert.Equal(report.Happy - twoHoursAgo.Happy, report.HappyChange);
    }

    [Fact]
    public void The_running_order_shows_what_has_played_what_is_on_and_what_is_next()
    {
        var game = StaffedDay();
        game.OpenGates();
        var show = game.Show;
        var second = show.Slots[1];
        RunUntil(game, second.Start.AddMinutes(10));

        var stage = Assert.Single(DayReport.Of(game).Stages);
        Assert.Equal("The stage", stage.Name);
        Assert.Equal(show.Slots.Count, stage.Sets.Count);
        Assert.Equal(SetState.Played, stage.Sets[0].State);
        Assert.Equal(SetState.OnNow, stage.Sets[1].State);
        Assert.Equal(SetState.Next, stage.Sets[2].State);
        Assert.All(stage.Sets.Skip(3), s => Assert.Equal(SetState.Later, s.State));
        Assert.Equal(show.Slots.Select(s => s.Act.Name), stage.Sets.Select(s => s.Act));
        Assert.True(stage.Sets.Last().Headliner);
        Assert.True(stage.Watching > 0, "People should be watching the act on now.");
        Assert.Equal(game.Crowd.Watching, stage.Watching);
    }

    [Fact]
    public void A_crush_is_logged_with_the_choice_the_player_made()
    {
        var game = SafetyTests.BuildDay(bigName: true);
        var warned = false;
        game.CrushWarning += (_, _) => warned = true;
        game.OpenGates();
        while (!warned)
        {
            game.Clock.Step();
            Assert.True(game.Phase == Phase.Day, "No crush built all day.");
        }

        var crush = game.DayLog.First(n => n.Kind == DayNoteKind.Crush);
        Assert.Equal(DayTone.Open, crush.Tone);
        Assert.Equal("Waiting for your choice", crush.Outcome);
        Assert.Contains("the stage", crush.What);
        Assert.Contains(DayReport.Of(game).WorthALook, w => w.StartsWith("Crush risk") || w.StartsWith("Getting packed"));

        var act = game.Show.Playing(game.Now)!;
        Assert.True(game.Choose(CrushChoice.CancelAct));
        Assert.Equal($"You cancelled {act.Act.Name}", crush.Outcome);
        var stage = Assert.Single(DayReport.Of(game).Stages);
        Assert.Equal(SetState.Cancelled, stage.Sets.Single(s => s.Act == act.Act.Name && s.Start == act.Start).State);
    }

    [Fact]
    public void Money_is_grouped_simply_and_adds_up_to_the_takings()
    {
        var game = StaffedDay();
        game.OpenGates();
        RunUntil(game, game.Day.AddHours(15));

        var report = DayReport.Of(game);
        Assert.Equal(game.TotalIncome, report.TotalIn);
        Assert.Equal(game.TotalSpending, report.TotalOut);
        Assert.Equal(game.TotalIncome - game.TotalSpending, report.UpSoFar);
        Assert.Equal(game.Money, report.Bank);
        Assert.Contains(report.Out, l => l.What == "Acts");
        Assert.Contains(report.Out, l => l.What == "Staff");
        Assert.True(report.Out.Count <= 6, "Spending should fold into a few lines.");
        Assert.Equal(report.In.OrderByDescending(l => l.Amount), report.In);
        Assert.Equal("Field", DayReport.SpendingGroup("Field rent and licence"));
        Assert.Equal("Kit and building", DayReport.SpendingGroup("Stalls and toilets"));
    }

    [Fact]
    public void First_aid_beds_and_injuries_are_counted()
    {
        var game = StaffedDay();
        var report = DayReport.Of(game);
        Assert.Equal(Catalogue.Of(StallKind.FirstAidTent).Servers, report.Beds);
        Assert.Equal(0, report.BedsUsed);
        Assert.Equal(0, report.Hurt);
    }

    [Fact]
    public void The_log_and_the_happiness_line_survive_a_save()
    {
        var game = StaffedDay();
        game.OpenGates();
        var guard = 0;
        while (game.DayLog.Count(n => n.IsIncident) < 2 || game.MoodLine.Count < 4)
        {
            game.Clock.Step();
            Assert.True(++guard < 60000, "Nothing happened.");
        }

        var loaded = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));
        Assert.Equal(game.DayLog.Select(n => (n.Id, n.At, n.Kind, n.What, n.Outcome, n.Tone, n.Incident)), loaded.DayLog.Select(n => (n.Id, n.At, n.Kind, n.What, n.Outcome, n.Tone, n.Incident)));
        Assert.Equal(game.MoodLine, loaded.MoodLine);

        for (var i = 0; i < 3600; i++)
        {
            game.Clock.Step();
            loaded.Clock.Step();
        }

        Assert.Equal(game.DayLog.Select(n => (n.Id, n.Outcome, n.Tone)), loaded.DayLog.Select(n => (n.Id, n.Outcome, n.Tone)));
    }

    [Fact]
    public void Running_short_of_toilets_is_worth_a_look()
    {
        var game = FestivalGame.NewFestival("Larkmoor Festival");
        game.ChooseHeadliner(0);
        Assert.True(game.ConfirmPlan());
        var gap = game.Field.Gap;
        Assert.True(game.BuildFence(FenceKind.Rope, new TilePoint(gap.X, gap.Y + 1), new TilePoint(gap.Right - 1, gap.Y + 1)).Ok);
        Assert.True(game.BuildGate(GateKind.VillageGate, gap.X, gap.Y + 1, false).Ok);
        var f = game.Field.Field;
        Assert.True(game.BuildStage(StageKind.FarmTrailer, f.X + 50, f.Y + 6, Facing.South).Ok);
        Assert.True(game.HireStaff(StaffKind.TicketStaff, GateTile(game)).Ok);
        game.OpenGates();
        RunUntil(game, game.Day.AddHours(16));

        var worries = DayReport.Of(game).WorthALook;
        Assert.Contains(worries, w => w.StartsWith("Toilets"));
        Assert.Contains(worries, w => w.StartsWith("Drinks"));
    }
}
