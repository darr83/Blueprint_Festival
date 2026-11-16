using System.Text.Json;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;

namespace BlueprintFestival.Simulation.Tests;

/// <summary>The long weekend: two days from a licence of 3,000 and three from 5,000, a line-up for each day, and the morning between.</summary>
[Collection(BigDays.Name)]
public class DaysTests
{
    private static SaveData Reload(FestivalGame game) => JsonSerializer.Deserialize<SaveData>(JsonSerializer.Serialize(game.Save()))!;

    private static void RunDay(FestivalGame game)
    {
        if (game.Phase == Phase.Build || game.GatesShut)
        {
            game.OpenGates();
        }

        for (var guard = 0; game.Phase == Phase.Day && game.CampTime == CampTime.None; guard++)
        {
            game.Clock.Step();
            Assert.True(guard < 70000, "The day never ended.");
        }
    }

    [Fact]
    public void The_licence_sets_how_many_days_the_festival_runs()
    {
        Assert.Equal(1, FestivalGame.DaysFor(1500));
        Assert.Equal(2, FestivalGame.DaysFor(3000));
        Assert.Equal(3, FestivalGame.DaysFor(5000));

        var two = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(5, 3000, 90000));
        Assert.Equal(2, two.Days);
        Assert.Equal(DayOfWeek.Saturday, two.Day.DayOfWeek);
        Assert.Equal(new[] { "Saturday", "Sunday" }, Enumerable.Range(0, 2).Select(two.DayName));

        var three = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(6, 5000, 90000));
        Assert.Equal(3, three.Days);
        Assert.Equal(DayOfWeek.Friday, three.Day.DayOfWeek);
        Assert.Equal("Sunday", three.DayName(2));
        Assert.Equal(FestivalGame.MaxTicketPrice * 3, three.TopTicketPrice);

        // A quick festival with no year is always one day.
        Assert.Equal(1, new FestivalGame().Days);
    }

    [Fact]
    public void Every_day_has_its_own_line_up_and_the_fees_and_the_ticket_count_them_all()
    {
        var game = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(5, 3000, 90000) { EmptyBill = true });
        Assert.Empty(game.WholeBill);
        var top = RunningOrder.SetsAStage - 1;
        var big = Roster.Artists.First(a => a.Rating == 3);
        Assert.True(game.BookAct(top, big.Name));
        var oneDayFair = game.FairPrice;
        var oneDayFees = game.ActFees;

        game.ShowDayBill(1);
        Assert.Equal(1, game.BillDay);
        Assert.Empty(game.LineUp);
        Assert.Equal(oneDayFees, game.ActFees);

        // Another act on Sunday, and the same act can play both days.
        var other = Roster.Artists.First(a => a.Rating == 2);
        Assert.True(game.BookAct(top - 1, other.Name));
        Assert.True(game.BookAct(top, big.Name));
        Assert.Equal(3, game.WholeBill.Count());
        Assert.Equal(oneDayFees + game.FeeFor(other.Name) + game.FeeFor(big.Name), game.ActFees);
        Assert.True(game.FairPrice > oneDayFair, "Two days of music should be worth more than one.");
        Assert.Equal(big.Name, game.HeadlinerOf(0)?.Name);
        Assert.Equal(big.Name, game.HeadlinerOf(1)?.Name);

        game.ShowDayBill(0);
        Assert.Single(game.LineUp);
        Assert.Equal(big.Name, game.Show.At(top)?.Name);

        // A save keeps both days, and which one the planning screen was on.
        game.ShowDayBill(1);
        var loaded = FestivalGame.Load(Reload(game));
        Assert.Equal(1, loaded.BillDay);
        Assert.Equal(2, loaded.LineUp.Count());
        Assert.Equal(game.ActFees, loaded.ActFees);
        loaded.ShowDayBill(0);
        Assert.Equal(big.Name, loaded.Show.At(top)?.Name);
        Assert.Single(loaded.LineUp);

        // Taking a stage away takes it away on every day.
        var stage = game.AddEmptyStage();
        Assert.True(game.BookAct(RunningOrder.BaseOf(stage), Roster.Artists.First(a => a.Rating == 1).Name));
        game.ShowDayBill(0);
        Assert.True(game.RemoveStage(stage));
        game.ShowDayBill(1);
        Assert.Equal(2, game.LineUp.Count());
    }

    [Fact]
    public void A_two_day_festival_runs_day_after_day_with_a_morning_between()
    {
        var game = GrowthTests.Built(1500, days: 2);
        Assert.Equal(2, game.Days);
        var firstDay = game.Day;
        var acts = game.WholeBill.Count();
        Assert.True(game.BillOf(1).Any(), "The second day should have acts of its own.");
        RunDay(game);

        // Between the days: the clock stops on the next morning, before the gates open again.
        Assert.Equal(Phase.Between, game.Phase);
        Assert.Equal(firstDay.AddDays(1) + FestivalGame.GatesAgain, game.Now);
        Assert.True(game.CanBuild);
        Assert.Single(game.DayHappiness);
        Assert.Equal(0, game.Advance(5));
        var tickets = game.Income.Single(l => l.What == "Tickets").Amount;
        Assert.True(tickets > 0);
        var litter = game.Litter.Count;
        Assert.True(litter > 0, "The first day should leave litter.");
        Assert.True(game.LitterMood > 0);
        var before = game.Money;
        Assert.True(game.HireLitterCrew());
        Assert.Empty(game.Litter);
        Assert.Equal(before - game.Money, game.Spending.Single(l => l.What == "Overnight litter crew").Amount);
        Assert.False(game.HireLitterCrew());

        // Straw on the worn ground saves the grass, and people mind the mud less.
        Assert.True(game.WornPatches > 0, "A day's walking about should wear some of the field through.");
        var worn = game.GrassWorn;
        var mud = game.MudMood;
        Assert.Equal(Math.Max(50, game.WornPatches * FestivalGame.StrawAPatch), game.StrawCost);
        Assert.True(game.LayStraw());
        Assert.True(game.GrassWorn < worn);
        Assert.True(game.MudMood < mud || mud == 0);
        Assert.False(game.LayStraw());

        // It saves and loads between the days.
        var loaded = FestivalGame.Load(Reload(game));
        Assert.Equal(Phase.Between, loaded.Phase);
        Assert.Single(loaded.DayHappiness);
        Assert.True(loaded.LitterCrewHired);

        // The next morning: the next day's acts, a day later, with everyone paid again.
        var wages = game.Spending.Single(l => l.What == "Staff wages").Amount;
        game.OpenNextDay();
        Assert.Equal(Phase.Day, game.Phase);
        Assert.Equal(1, game.DayNumber);
        Assert.Equal(firstDay.AddDays(1), game.Day);
        Assert.True(game.GatesShut);
        Assert.Equal(wages + game.DayWages, game.Spending.Single(l => l.What == "Staff wages").Amount);
        Assert.Equal(1, game.BillDay);
        Assert.All(game.LineUp, s => Assert.Equal(game.Day, s.Start.Date));
        Assert.Equal(acts, game.WholeBill.Count());
        Assert.True(game.NextAct is not null);
        RunDay(game);

        // A weekend ticket is paid for once, and the tidy-up comes after the last day.
        Assert.Equal(Phase.Tidy, game.Phase);
        Assert.Equal(tickets, game.Income.Single(l => l.What == "Tickets").Amount);
        Assert.Equal(2, game.DayHappiness.Count);
        game.PayScouts();
        var result = game.HandBack();
        Assert.Equal(2, result.Days);
        Assert.Equal((int)Math.Round(game.DayHappiness.Average()), result.Happy);
    }

    [Fact]
    public void Campers_sleep_between_the_days_and_their_tents_stay_up()
    {
        var plan = new World.FieldPlan(new[]
        {
            new World.FieldChoice("home", World.FieldUse.Music), new World.FieldChoice("ten", World.FieldUse.Food),
            new World.FieldChoice("show", World.FieldUse.Camping), new World.FieldChoice("glebe", World.FieldUse.CarPark),
        });
        var game = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(6, 5000, 90000) { Days = 2 }).WithFields(plan)!;
        game.HireBuses(FestivalGame.MostBuses);
        Assert.True(game.ConfirmPlan());
        Assert.Equal(2, game.Days);
        CampLayoutTests.OpenAWayIn(game);
        CampLayoutTests.LayFireLanes(game);
        RunDay(game);
        Assert.True(game.CampTime == CampTime.Night, $"{game.Phase} {game.Now} {game.CampingBooked} {game.Crowd.InTents}");
        game.SkipCampNight();

        // No packing up: the campers stay for the second day.
        Assert.Equal(Phase.Between, game.Phase);
        Assert.Equal(CampTime.None, game.CampTime);
        Assert.Equal(0, game.CampersGoneHome);
        var tents = game.Crowd.TentsUp.Count();
        Assert.True(tents > 0);
        var camping = game.Income.Single(l => l.What == "Camping").Amount;

        game.OpenNextDay();
        Assert.Equal(tents, game.Crowd.TentsUp.Count());
        game.OpenGates();
        for (var i = 0; i < 4 * 3600 && game.Phase == Phase.Day; i++)
        {
            game.Clock.Step();
        }

        Assert.True(game.Crowd.CampersIn > 0, "Campers should be back in by midday.");
        Assert.Equal(camping, game.Income.Single(l => l.What == "Camping").Amount);
        Assert.Equal(tents, game.Crowd.TentsUp.Count());
    }
}
