using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

public class YearTests
{
    /// <summary>A sensible little site: gate in the gap, bandstand, food, drink and toilets.</summary>
    private static void BuildSite(FestivalGame game)
    {
        var gap = game.Field.Gap;
        var y = gap.Y + 1;
        Assert.True(game.BuildFence(FenceKind.Rope, new TilePoint(gap.X, y), new TilePoint(gap.Right - 1, y)).Ok);
        Assert.True(game.BuildGate(GateKind.VillageGate, gap.X, y, false).Ok);
        var f = game.Field.Field;
        Assert.True(game.BuildStage(StageKind.Bandstand, f.X + 50, f.Y + 6, Facing.South).Ok);
        Amenities.Add(game);
    }

    private static void RunTheDay(FestivalGame game)
    {
        var guard = 0;
        while (game.Phase == Phase.Day)
        {
            game.Clock.Step();
            Assert.True(++guard < 70000, "The day never ended.");
        }
    }

    private static FestivalGame AfterTheDay(string name = "Larkmoor Festival")
    {
        var game = FestivalGame.NewFestival(name);
        Assert.True(game.ConfirmPlan());
        BuildSite(game);
        game.OpenGates();
        RunTheDay(game);
        return game;
    }

    [Fact]
    public void A_new_festival_opens_on_the_planning_screen_with_the_clock_stopped()
    {
        var game = FestivalGame.NewFestival("Larkmoor Festival");

        Assert.Equal(Phase.Plan, game.Phase);
        Assert.Equal(1, game.Year);
        Assert.Equal(FestivalGame.FirstLicence, game.Licence);
        Assert.Equal(FestivalGame.StartingMoney, game.Money);
        Assert.Equal(new DateTime(2027, 7, 17, 8, 0, 0), game.Now);
        Assert.Equal(0, game.Advance(10));
        Assert.False(game.BuildStage(StageKind.Bandstand, game.Field.Field.X + 50, game.Field.Field.Y + 6, Facing.South).Ok);
    }

    [Fact]
    public void The_festival_is_on_the_third_Saturday_of_July_every_year()
    {
        for (var year = 1; year <= 12; year++)
        {
            var day = FestivalGame.FestivalDay(year);
            Assert.Equal(DayOfWeek.Saturday, day.DayOfWeek);
            Assert.Equal(7, day.Month);
            Assert.InRange(day.Day, 15, 21);
            Assert.Equal(2026 + year, day.Year);
        }
    }

    [Fact]
    public void A_dearer_ticket_brings_fewer_people_and_a_bigger_headliner_brings_more()
    {
        var game = FestivalGame.NewFestival();

        var expected = FestivalGame.TicketPrices.Select(game.ExpectedAt).ToList();
        Assert.Equal(expected.OrderByDescending(e => e), expected);
        Assert.True(expected[0] > expected[^1]);
        Assert.All(expected, e => Assert.InRange(e, 10, game.Licence));

        game.ChooseHeadliner(0);
        var small = game.ExpectedAt(8);
        game.ChooseHeadliner(2);
        Assert.True(game.ExpectedAt(8) > small);
        Assert.Throws<ArgumentOutOfRangeException>(() => game.ExpectedAt(7));
    }

    [Fact]
    public void Three_made_up_headliners_are_on_offer_and_better_known_ones_cost_more()
    {
        var game = FestivalGame.NewFestival();

        Assert.Equal(3, game.Headliners.Count);
        Assert.Equal(game.Headliners.OrderBy(h => h.Stars).ToList(), game.Headliners);
        Assert.Equal(game.Headliners.OrderBy(h => h.Fee).ToList(), game.Headliners);
        Assert.Equal(3, game.Headliners.Select(h => h.Stars).Distinct().Count());
        Assert.All(game.Headliners, h => Assert.InRange(h.Stars, 2, 5));
        var names = game.Headliners.Select(h => h.Act.Name).Concat(game.Show.Slots.Take(6).Select(s => s.Act.Name)).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());

        var again = FestivalGame.NewFestival();
        Assert.Equal(game.Headliners.Select(h => h.Act.Name), again.Headliners.Select(h => h.Act.Name));
    }

    [Fact]
    public void Confirming_the_plan_pays_the_acts_books_the_headliner_and_starts_the_build_day()
    {
        var game = FestivalGame.NewFestival();
        game.ChooseTicket(8);
        game.ChooseHeadliner(2);
        var fees = game.ActFees;
        var guess = game.ExpectedAt(8);

        Assert.True(game.ConfirmPlan());

        Assert.Equal(Phase.Build, game.Phase);
        Assert.Equal(FestivalGame.StartingMoney - fees, game.Money);
        Assert.Contains(game.Spending, l => l.What == "Act fees" && l.Amount == fees);
        Assert.Equal(game.Headliners[2].Act, game.Show.Slots[^1].Act);
        Assert.InRange(game.Crowd.Expected, (int)(guess * 0.9), Math.Min(game.Licence, (int)(guess * 1.06) + 1));
        Assert.Throws<InvalidOperationException>(() => game.ChooseTicket(3));

        // The clock waits until the gates are opened.
        Assert.Equal(0, game.Advance(10));
        BuildSite(game);
        game.OpenGates();
        Assert.Equal(Phase.Day, game.Phase);
        Assert.True(game.Advance(10) > 0);
    }

    [Fact]
    public void The_ticket_price_chosen_is_what_people_pay_at_the_gate()
    {
        var game = FestivalGame.NewFestival();
        game.ChooseTicket(12);
        Assert.True(game.ConfirmPlan());
        BuildSite(game);
        game.OpenGates();
        while (game.Crowd.ThroughGates == 0)
        {
            game.Clock.Step();
        }

        Assert.Equal(game.Crowd.ThroughGates * 12, game.Income.Single(l => l.What == "Tickets").Amount);
    }

    [Fact]
    public void A_whole_year_runs_from_plan_to_results_and_a_good_one_doubles_the_licence()
    {
        var game = AfterTheDay();

        Assert.Equal(Phase.Tidy, game.Phase);
        Assert.Equal(game.Day.AddDays(1).AddHours(6), game.Now);
        Assert.Equal(0, game.Crowd.OnSite);
        Assert.True(game.LitterAtStart > 20, $"Only {game.LitterAtStart} pieces of litter.");
        Assert.Equal(game.LitterAtStart, game.Litter.Count);
        Assert.All(game.Litter, l => Assert.True(game.Field.Field.Contains(FieldMap.TileAt(l.X, l.Y))));
        Assert.InRange(game.GrassWorn, 1, 60);
        Assert.Equal(0, game.LitterCleared);
        Assert.False(game.BuildStall(StallKind.WaterPoint, game.Field.Field.X + 20, game.Field.Field.Y + 20, Facing.South).Ok);

        // Pick some by hand, then pay the scouts for the rest.
        var first = game.Litter[0];
        var picked = game.PickLitter(first.X, first.Y);
        Assert.True(picked >= 1);
        Assert.Equal(game.LitterAtStart - picked, game.Litter.Count);
        var fee = game.ScoutsFee;
        var before = game.Money;
        Assert.True(game.PayScouts());
        Assert.Equal(before - fee, game.Money);
        Assert.Empty(game.Litter);
        Assert.Equal(100, game.LitterCleared);

        var result = game.HandBack();
        Assert.Equal(Phase.Results, game.Phase);
        Assert.Equal(game.Result, result);
        Assert.Equal(game.Crowd.Arrived, result.Came);
        Assert.True(result.Happy >= FestivalGame.HappyEnough, $"Only {result.Happy}% happy.");
        Assert.True(result.Profit > 0, $"Profit {result.Profit}.");
        Assert.InRange(result.Profit, 200, 1500);
        Assert.Equal(400, result.LicenceNext);
        Assert.False(result.Loan);

        var next = game.NextYear();
        Assert.Equal(Phase.Plan, next.Phase);
        Assert.Equal(2, next.Year);
        Assert.Equal(400, next.Licence);
        Assert.Equal(game.Money, next.Money);
        Assert.Equal(new DateTime(2028, 7, 15, 8, 0, 0), next.Now);
        Assert.Empty(next.Site.Stages);
        Assert.Empty(next.Income);
        Assert.NotEqual(game.Seed, next.Seed);
        Assert.True(next.ActFees > game.ActFees);
    }

    [Fact]
    public void A_field_left_covered_in_litter_does_not_grow()
    {
        var game = AfterTheDay();

        var result = game.HandBack();

        Assert.Equal(0, result.LitterCleared);
        Assert.Equal(result.LicenceNow, result.LicenceNext);
        Assert.Contains("field", result.Verdict);
    }

    [Fact]
    public void Running_out_of_money_brings_one_loan_and_the_second_time_closes_the_festival()
    {
        // A festival with nothing built: nobody pays, nobody stays, and the fees are gone.
        var game = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(1, 200, 500));
        game.ChooseHeadliner(0);
        Assert.True(game.ConfirmPlan());
        game.OpenGates();
        RunTheDay(game);
        var result = game.HandBack();

        Assert.True(result.Loan);
        Assert.False(result.Closed);
        Assert.Equal(Phase.Results, game.Phase);
        Assert.True(game.BailedOut);
        Assert.Equal(result.LicenceNow, result.LicenceNext);
        Assert.Equal(500 - game.TotalSpending + game.TotalIncome + FestivalGame.LoanAmount, game.Money);

        var second = game.NextYear();
        Assert.True(second.BailedOut);
        second.ChooseHeadliner(2);
        Assert.True(second.ConfirmPlan());
        second.OpenGates();
        RunTheDay(second);
        var last = second.HandBack();

        Assert.True(last.Closed);
        Assert.Equal(Phase.Closed, second.Phase);
        Assert.Throws<InvalidOperationException>(() => second.NextYear());
    }

    [Fact]
    public void The_plan_cannot_be_confirmed_without_the_money_for_the_acts()
    {
        var game = FestivalGame.StartYear(null, new YearSetup(1, 200, 100));

        Assert.False(game.ConfirmPlan());
        Assert.Equal(Phase.Plan, game.Phase);
        Assert.Equal(100, game.Money);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Every_part_of_the_year_saves_and_loads(int stage)
    {
        var game = FestivalGame.NewFestival("Larkmoor Festival");
        game.ChooseTicket(8);
        game.ChooseHeadliner(2);
        if (stage >= 1)
        {
            Assert.True(game.ConfirmPlan());
            BuildSite(game);
        }

        if (stage >= 2)
        {
            game.OpenGates();
            while (game.Now < game.Day.AddHours(15))
            {
                game.Clock.Step();
            }
        }

        if (stage >= 3)
        {
            RunTheDay(game);
            game.PickLitter(game.Litter[0].X, game.Litter[0].Y);
        }

        var loaded = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));

        Assert.Equal(game.Phase, loaded.Phase);
        Assert.Equal(game.Year, loaded.Year);
        Assert.Equal(game.Now, loaded.Now);
        Assert.Equal(game.Day, loaded.Day);
        Assert.Equal(game.Money, loaded.Money);
        Assert.Equal(8, loaded.TicketPrice);
        Assert.Equal(2, loaded.HeadlinerPicked);
        Assert.Equal(game.Show.Slots[^1].Act.Name, loaded.Show.Slots[^1].Act.Name);
        Assert.Equal(game.Crowd.Expected, loaded.Crowd.Expected);
        Assert.Equal(game.Crowd.Arrived, loaded.Crowd.Arrived);
        Assert.Equal(game.Litter, loaded.Litter);
        Assert.Equal(game.LitterAtStart, loaded.LitterAtStart);
        Assert.Equal(game.GrassWorn, loaded.GrassWorn);
        Assert.Equal(game.Site.Stalls.Count, loaded.Site.Stalls.Count);

        if (stage == 2)
        {
            // The same day plays out after loading as it would have.
            for (var i = 0; i < 3600; i++)
            {
                game.Clock.Step();
                loaded.Clock.Step();
            }

            Assert.Equal(game.Money, loaded.Money);
            Assert.Equal(game.Litter, loaded.Litter);
            Assert.Equal(game.Crowd.Happiness, loaded.Crowd.Happiness);
        }
    }

    [Fact]
    public void A_festival_with_nothing_to_plan_still_opens_on_the_morning_and_never_ends_itself()
    {
        var game = new FestivalGame();

        Assert.Equal(Phase.Day, game.Phase);
        Assert.False(game.Planned);
        Assert.Null(game.Save().Year);
        Assert.Empty(game.Headliners);
    }

    [Fact]
    public void Last_years_happiness_changes_how_many_come()
    {
        var loved = FestivalGame.StartYear(null, new YearSetup(2, 400, 3000, false, 90));
        var hated = FestivalGame.StartYear(null, new YearSetup(2, 400, 3000, false, 30));

        Assert.True(loved.ExpectedAt(5) > hated.ExpectedAt(5));
    }
}
