using BlueprintFestival.Simulation.Sales;
using BlueprintFestival.Simulation.Show;
using Xunit;

namespace BlueprintFestival.Simulation.Tests;

public class TicketSalesTests
{
    private static FestivalGame Year(int licence = 1500, int bank = 60000, int? happy = 80) =>
        FestivalGame.StartYear("Larkmoor Festival", new YearSetup(4, licence, bank, false, happy));

    private static FestivalGame Selling(int price, params string[] adverts)
    {
        var game = Year();
        game.SetTicketPrice(price);
        Assert.True(game.BookEvent());
        foreach (var advert in adverts)
        {
            game.SetAdvert(advert, true);
        }

        return game;
    }

    private static int SellSixMonths(FestivalGame game)
    {
        for (var month = 0; month < FestivalGame.SalesMonths.Count; month++)
        {
            Assert.NotNull(game.RunSales());
        }

        return game.TicketsSold;
    }

    [Fact]
    public void Any_artist_can_be_booked_for_any_set_and_sets_can_be_left_empty()
    {
        var game = Year();
        Assert.Equal(4, game.Show.Slots.Count);
        Assert.Equal(4, game.LineUp.Count());

        // A five star act wants one of the last two sets of a stage.
        Assert.False(game.BookAct(0, "The Velvet Otters"));
        Assert.True(game.BookAct(2, "The Velvet Otters"));
        Assert.Equal("The Velvet Otters", game.Show.At(2)!.Name);
        Assert.Equal(2, game.SetOf("The Velvet Otters"));
        Assert.Equal(game.LineUp.Sum(s => game.FeeOf(s.Act.Rating)), game.ActFees);
        Assert.Equal(game.FeeOf(5), 750 * 1500 / 200);
        Assert.True(game.FeeOf(5) > game.FeeOf(4) && game.FeeOf(4) > game.FeeOf(3) && game.FeeOf(2) > game.FeeOf(1));

        // Nobody plays twice: booking someone who is already on swaps the two sets over.
        var last = game.Show.At(3)!.Name;
        Assert.True(game.BookAct(3, "The Velvet Otters"));
        Assert.Equal("The Velvet Otters", game.Show.At(3)!.Name);
        Assert.Equal(last, game.Show.At(2)!.Name);
        Assert.Equal(game.LineUp.Count(), game.LineUp.Select(s => s.Act.Name).Distinct().Count());
        Assert.True(game.BookAct(2, "The Velvet Otters"));
        Assert.Equal(last, game.Show.At(3)!.Name);

        // An empty set costs nothing, and the last act booked on the main stage closes the night.
        var before = game.ActFees;
        Assert.True(game.BookAct(3, null));
        Assert.Null(game.Show.At(3));
        Assert.True(game.ActFees < before);
        Assert.True(game.Show.Slots[^1].Headliner);
        Assert.Equal("The Velvet Otters", game.Show.Slots[^1].Act.Name);
        Assert.Equal(5, game.HeadlineStars);

        Assert.False(game.BookAct(0, "Nobody At All"));
        Assert.False(game.BookAct(7, "Tobias Pell"));
        Assert.False(game.BookAct(RunningOrder.SecondBase, "Tobias Pell"));

        // A second stage starts empty and takes four more.
        game.AddEmptySecondStage();
        Assert.True(game.SecondStageBooked);
        Assert.Empty(game.SecondShow.Slots);
        Assert.True(game.BookAct(RunningOrder.SecondBase + 1, "Tobias Pell"));
        Assert.Equal(RunningOrder.SecondBase + 1, game.SetOf("Tobias Pell"));
        Assert.True(game.SecondShow.At(RunningOrder.SecondBase + 1)!.Draw < ActGenerator.DrawOf(Roster.Named("Tobias Pell")!, false));
        Assert.Equal(game.LineUp.Count(), game.LineUp.Select(s => s.Act.Name).Distinct().Count());

        // The whole bill can be emptied; then there is nothing to pay and no headliner.
        foreach (var slot in game.LineUp.ToList())
        {
            Assert.True(game.BookAct(slot.Index, null));
        }

        Assert.Equal(0, game.ActFees);
        Assert.Equal(0, game.HeadlineStars);
    }

    [Fact]
    public void A_better_line_up_pulls_harder_and_is_worth_a_dearer_ticket()
    {
        var game = Year();
        foreach (var slot in game.LineUp.ToList())
        {
            game.BookAct(slot.Index, null);
        }

        var nobody = game.LineUpPull;
        game.BookAct(3, "Little Ferrets");
        var small = (game.LineUpPull, game.FairPrice);
        game.BookAct(3, "The Lantern Hearts");
        var big = (game.LineUpPull, game.FairPrice);
        Assert.True(small.LineUpPull > nobody);
        Assert.True(big.LineUpPull > small.LineUpPull + 0.3);
        Assert.True(big.FairPrice > small.FairPrice + 2);

        // Fans count as well as stars.
        game.BookAct(3, "The Velvet Otters");
        Assert.True(game.LineUpPull > big.LineUpPull);

        // Every other act adds a little, and four kinds of music bring a wider crowd.
        game.BookAct(3, "The Lantern Hearts");
        game.BookAct(0, "Odell Ashby");
        var withSupport = game.LineUpPull;
        Assert.True(withSupport > big.LineUpPull);
        game.BookAct(1, "Wren Hallows");
        game.BookAct(2, "Marnie Thrupp");
        var three = game.LineUpPull;
        game.BookAct(2, "Crooked Lantern");
        Assert.True(game.LineUpPull > three + 0.05);
    }

    [Fact]
    public void Booking_the_event_pays_the_acts_and_puts_tickets_on_sale_month_by_month()
    {
        var game = Year();
        var cost = game.PlanCost;
        Assert.Equal(game.ActFees + game.FieldRent, cost);
        Assert.Null(game.RunSales());
        Assert.True(game.BookEvent());
        Assert.Equal(Phase.Sell, game.Phase);
        Assert.True(game.SoldAhead);
        Assert.Equal(60000 - cost, game.Money);
        Assert.Equal("January", game.NextSalesTurn);
        Assert.Throws<InvalidOperationException>(() => game.BookAct(0, null));
        Assert.Throws<InvalidOperationException>(() => game.OpenGates());

        game.SetAdvert("posters", true);
        game.SetAdvert("localradio", true);
        Assert.Equal(2350, game.AdvertReach);
        Assert.Equal(205, game.AdvertCost);
        Assert.Throws<ArgumentOutOfRangeException>(() => game.SetAdvert("skywriting", true));

        var bank = game.Money;
        var guess = game.SalesForecast;
        var january = game.RunSales()!;
        Assert.Equal("January", january.When);
        Assert.InRange(january.Tickets, (int)(guess * 0.9), (int)(guess * 1.1) + 1);
        Assert.Equal(205, january.AdCost);
        Assert.Equal(bank - 205 + (january.Tickets * game.TicketPrice) + (january.Vips * game.VipTicketPrice), game.Money);
        Assert.Equal(january.Tickets + january.Vips, game.TicketsSold);
        Assert.Equal(january.Vips, game.VipTicketsSold);
        Assert.Equal("February", game.NextSalesTurn);
        Assert.Contains(game.Spending, l => l.What == "Advertising" && l.Amount == 205);
        Assert.Contains(game.Income, l => l.What == "Tickets" && l.Amount == january.Tickets * january.Price);

        // The same adverts sell more as July gets nearer.
        for (var month = 1; month < 6; month++)
        {
            Assert.Equal(Phase.Sell, game.Phase);
            Assert.NotNull(game.RunSales());
        }

        Assert.True(game.SalesLog[5].Tickets > game.SalesLog[0].Tickets);
        Assert.Equal(Phase.Build, game.Phase);
        Assert.Equal("Sunday", game.NextSalesTurn);
        Assert.Equal(Math.Max(10, game.TicketsSold), game.Crowd.Expected);
        Assert.InRange(game.TicketsSold, 1, game.Licence);
    }

    [Fact]
    public void Adverts_a_fair_price_and_a_good_line_up_all_sell_more_tickets()
    {
        var quiet = SellSixMonths(Selling(6));
        var advertised = SellSixMonths(Selling(6, "posters", "flyers", "paper", "social", "localradio"));
        Assert.True(advertised > quiet * 1.5, $"{advertised} against {quiet}");

        var dear = SellSixMonths(Selling(14, "posters", "flyers", "paper", "social", "localradio"));
        Assert.True(dear < advertised / 2, $"{dear} against {advertised}");

        // A cheap ticket with everything switched on sells out, and stops at the licence.
        var cheap = Selling(2, Adverts.All.Select(a => a.Id).ToArray());
        Assert.Equal(cheap.Licence, SellSixMonths(cheap));
        Assert.True(cheap.SoldOut);
        Assert.Equal(0, cheap.SalesForecast);
        Assert.Equal(0, cheap.AdvertCost);

        var plain = Year();
        Assert.True(plain.BookEvent());
        var starry = Year();
        starry.BookAct(3, "DJ Otter");
        Assert.True(starry.BookEvent());
        Assert.True(SellSixMonths(starry) > SellSixMonths(plain));

        // A festival people enjoyed last year sells more by word of mouth than one they did not.
        var liked = Year(happy: 95);
        var disliked = Year(happy: 30);
        Assert.True(liked.BookEvent() && disliked.BookEvent());
        Assert.True(SellSixMonths(liked) > SellSixMonths(disliked));
    }

    [Fact]
    public void Adverts_have_to_be_paid_for_and_the_price_can_change_every_month()
    {
        var game = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(1, 200, 2000));
        Assert.True(game.BookEvent());
        game.SetAdvert("nationaltv", true);
        Assert.Null(game.RunSales());
        Assert.Equal(0, game.SalesTurn);
        game.SetAdvert("nationaltv", false);
        game.SetAdvert("posters", true);

        game.SetTicketPrice(4);
        var first = game.RunSales()!;
        game.SetTicketPrice(500);
        Assert.Equal(FestivalGame.MaxTicketPrice, game.TicketPrice);
        var second = game.RunSales()!;
        Assert.Equal(4, first.Price);
        Assert.Equal(FestivalGame.MaxTicketPrice, second.Price);
        Assert.True(second.Tickets < first.Tickets);
        Assert.Equal((first.Tickets * 4) + (second.Tickets * FestivalGame.MaxTicketPrice), game.Income.Single(l => l.What == "Tickets").Amount);
        game.SetTicketPrice(0);
        Assert.Equal(1, game.TicketPrice);
    }

    [Fact]
    public void Build_week_sells_the_last_tickets_day_by_day_and_the_crowd_is_who_bought()
    {
        var game = Selling(9, "posters", "flyers", "paper");
        SellSixMonths(game);
        Assert.Equal(Phase.Build, game.Phase);
        var june = game.TicketsSold;
        Assert.Equal((game.AdvertCostAMonth + 5) / 6, game.AdvertCost);

        var sunday = game.RunSales()!;
        Assert.Equal("Sunday", sunday.When);
        Assert.Equal("Monday", game.NextSalesTurn);
        Assert.True(sunday.Tickets > 0 && sunday.Tickets < game.SalesLog[5].Tickets, $"{sunday.Tickets} on Sunday after {june} by June.");
        Assert.Equal(june + sunday.Tickets + sunday.Vips, game.TicketsSold);
        Assert.Equal(game.TicketsSold, game.Crowd.Expected);

        // The price can still change, and building goes on between days.
        game.SetTicketPrice(5);
        var f = game.Field.Field;
        Assert.True(game.BuildStage(Site.StageKind.HiredStage, f.X + 40, f.Y + 6, Site.Facing.South).Ok);
        Assert.NotNull(game.RunSales());

        // Opening the gates sells whatever days are left.
        game.OpenGates();
        Assert.Equal(Phase.Day, game.Phase);
        Assert.Null(game.NextSalesTurn);
        Assert.Equal(FestivalGame.SalesTurns, game.SalesLog.Count);
        Assert.Null(game.RunSales());
        Assert.Equal(game.TicketsSold, game.Crowd.Expected);
        Assert.Equal(game.SalesLog.Sum(l => l.Tickets + l.Vips), game.TicketsSold);

        // Tickets were paid for when they were bought, not again at the gate.
        var tickets = game.Income.Single(l => l.What == "Tickets").Amount;
        Assert.Equal(game.SalesLog.Sum(l => l.Tickets * l.Price), tickets);
        while (game.Now.TimeOfDay < TimeSpan.FromHours(12))
        {
            game.Clock.Step();
        }

        Assert.True(game.Crowd.Arrived > 0);
        Assert.Equal(tickets, game.Income.Single(l => l.What == "Tickets").Amount);
    }

    [Fact]
    public void A_year_that_is_selling_saves_and_loads_with_its_line_up_adverts_and_sales()
    {
        var game = Year();
        game.BookAct(3, "Hedgerow Howl");
        game.BookAct(1, null);
        game.AddEmptySecondStage();
        game.BookAct(RunningOrder.SecondBase + 2, "DJ Haybale");
        game.SetTicketPrice(9);

        var planning = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));
        Assert.Equal(Phase.Plan, planning.Phase);
        Assert.Equal(game.LineUp.Select(s => (s.Index, s.Act.Name)), planning.LineUp.Select(s => (s.Index, s.Act.Name)));
        Assert.Equal(9, planning.TicketPrice);
        Assert.Equal(game.ActFees, planning.ActFees);

        Assert.True(game.BookEvent());
        game.SetAdvert("paper", true);
        game.SetAdvert("bus", true);
        game.RunSales();
        game.RunSales();
        var selling = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));
        Assert.Equal(Phase.Sell, selling.Phase);
        Assert.True(selling.SoldAhead);
        Assert.Equal((game.SalesTurn, game.TicketsSold, game.Money), (selling.SalesTurn, selling.TicketsSold, selling.Money));
        Assert.True(selling.AdvertOn("paper") && selling.AdvertOn("bus") && !selling.AdvertOn("posters"));
        Assert.Equal(game.SalesLog, selling.SalesLog);
        Assert.Equal(game.LineUp.Select(s => (s.Index, s.Act.Name, s.Act.Draw)), selling.LineUp.Select(s => (s.Index, s.Act.Name, s.Act.Draw)));
        Assert.Equal(game.LineUpPull, selling.LineUpPull, 6);

        // Both carry on to the same year.
        foreach (var each in new[] { game, selling })
        {
            while (each.Phase == Phase.Sell)
            {
                Assert.NotNull(each.RunSales());
            }

            each.RunSales();
        }

        Assert.Equal(game.TicketsSold, selling.TicketsSold);
        var week = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));
        Assert.Equal(Phase.Build, week.Phase);
        Assert.Equal("Monday", week.NextSalesTurn);
        Assert.Equal(game.Crowd.Expected, week.Crowd.Expected);
    }

    [Fact]
    public void A_dear_ticket_costs_far_more_sales_than_a_cheap_one_gains()
    {
        var fair = (int)Math.Round(Year().FairPrice);
        var atFair = SellSixMonths(Selling(fair, "posters", "flyers", "localradio"));
        var dear = SellSixMonths(Selling((int)Math.Round(fair * 1.3), "posters", "flyers", "localradio"));
        var cheap = SellSixMonths(Selling((int)Math.Round(fair * 0.7), "posters", "flyers", "localradio"));

        // Thirty per cent over the odds loses about half the buyers.
        Assert.InRange(dear, atFair * 0.35, atFair * 0.65);
        Assert.True(cheap > atFair);
    }

    [Fact]
    public void The_last_tickets_are_the_hardest_to_sell()
    {
        var game = Selling((int)Math.Round(Year().FairPrice), "posters", "flyers", "localradio");
        Assert.Equal(1, game.Keenness, 3);
        SellSixMonths(game);
        Assert.True(game.TicketsSold > 0 && !game.SoldOut);
        Assert.InRange(game.Keenness, FestivalGame.LastToBuy, 0.999);
        Assert.Equal(1 - ((1 - FestivalGame.LastToBuy) * game.TicketsSold / game.Licence), game.Keenness, 6);
    }

    [Fact]
    public void People_arrive_in_a_better_mood_for_a_bargain_and_a_worse_one_for_a_dear_ticket()
    {
        double Arriving(double times)
        {
            var game = Year();
            game.SetTicketPrice((int)Math.Round(game.FairPrice * times));
            Assert.True(game.BookEvent());
            game.SetAdvert("localradio", true);
            SellSixMonths(game);
            Assert.True(game.TicketsSold > 0);
            Assert.Equal((game.TicketsSold - game.VipTicketsSold) * game.TicketPrice, game.TicketTakings);
            return game.ArrivalMood;
        }

        var fair = Arriving(1);
        Assert.InRange(fair, People.Crowd.StartingMood - 0.03, People.Crowd.StartingMood + 0.03);
        Assert.True(Arriving(1.4) < fair - 0.08);
        Assert.True(Arriving(0.7) > fair + 0.04);
        Assert.True(Arriving(3) >= People.Crowd.StartingMood - 0.2501);

        // The quick path, paid at the gate, arrives as it always did.
        Assert.Equal(People.Crowd.StartingMood, Year().ArrivalMood);
    }

    [Fact]
    public void A_better_act_lifts_the_mood_more()
    {
        Assert.True(People.Crowd.Enjoyed(5) > People.Crowd.Enjoyed(3) && People.Crowd.Enjoyed(3) > People.Crowd.Enjoyed(1));
        Assert.True(People.Crowd.Enjoyed(5) >= 3 * People.Crowd.Enjoyed(1));
        Assert.Equal(People.Crowd.Enjoyed(5), People.Crowd.Enjoyed(9));
    }

    [Fact]
    public void The_council_wants_more_in_the_bank_before_a_bigger_field()
    {
        Assert.Equal(FestivalGame.NeedsFor(400), FestivalGame.NeedsToGrowTo(400));
        Assert.Equal(4000, FestivalGame.NeedsToGrowTo(800));
        Assert.Equal(25000, FestivalGame.NeedsToGrowTo(5000));
        Assert.True(FestivalGame.NeedsToGrowTo(1500) > FestivalGame.NeedsFor(1500));
    }
}
