using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

/// <summary>The food, drink and merchandise stalls: their menus, the prices the player sets, and what people walk away with.</summary>
public class StallMenuTests
{
    private static readonly DateTime Day = GameClock.DefaultStart.Date;

    private static readonly StallKind[] Selling =
    {
        StallKind.HotDogs, StallKind.Burgers, StallKind.Fries, StallKind.Pizza, StallKind.Noodles, StallKind.IceCream,
        StallKind.Bar, StallKind.Coffee, StallKind.Slush, StallKind.Cocktails,
        StallKind.Ponchos, StallKind.BucketHats, StallKind.Merchandise, StallKind.PhoneCharging,
        StallKind.VipLounge,
    };

    private static void RunUntil(FestivalGame game, double hour)
    {
        var until = Day.AddHours(hour);
        while (game.Now < until)
        {
            game.Clock.Step();
        }
    }

    /// <summary>
    /// A village day with something to eat, something to drink and somewhere to go, every price
    /// multiplied by the same amount. A hot dog cart, a burger van, a bar, a coffee cart, an ice
    /// cream van and a merchandise tent, with toilets but no free tap.
    /// </summary>
    private static FestivalGame PricedDay(double times, double untilHour = 19, int seed = 8, WeatherKind weather = WeatherKind.Fine)
    {
        var game = new FestivalGame("Prices", seed: seed);
        game.SetWeather(DayWeather.Of(weather, 3));
        var f = game.Field.Field;
        var x = f.Right - 16;
        Assert.True(game.BuildStall(StallKind.Burgers, x, f.Y + 8, Facing.West).Ok);
        Assert.True(game.BuildStall(StallKind.Bar, x, f.Y + 24, Facing.West).Ok);
        Assert.True(game.BuildStall(StallKind.Toilets, x, f.Y + 40, Facing.West).Ok);
        Assert.True(game.BuildStall(StallKind.Toilets, x, f.Y + 50, Facing.West).Ok);
        Assert.True(game.BuildStall(StallKind.HotDogs, f.X + 8, f.Y + 10, Facing.East).Ok);
        Assert.True(game.BuildStall(StallKind.Coffee, f.X + 8, f.Y + 22, Facing.East).Ok);
        Assert.True(game.BuildStall(StallKind.IceCream, f.X + 8, f.Y + 34, Facing.East).Ok);
        Assert.True(game.BuildStall(StallKind.Merchandise, f.X + 8, f.Y + 50, Facing.East).Ok);
        for (var s = 0; s < game.Site.Stalls.Count; s++)
        {
            var menu = Catalogue.MenuOf(game.Site.Stalls[s].Kind);
            for (var i = 0; i < menu.Count; i++)
            {
                game.Site.SetPrice(s, i, (int)Math.Round(menu[i].UsualPrice * times));
            }
        }

        RunUntil(game, untilHour);
        return game;
    }

    private static int Sold(FestivalGame game, StallKind? kind = null) =>
        Enumerable.Range(0, game.Site.Stalls.Count).Where(i => kind is null || game.Site.Stalls[i].Kind == kind).Sum(i => game.SalesOf(i).Items);

    private static readonly Lazy<FestivalGame> Usual = new(() => PricedDay(1));
    private static readonly Lazy<FestivalGame> Cheap = new(() => PricedDay(0.5));
    private static readonly Lazy<FestivalGame> Dear = new(() => PricedDay(1.8));

    [Fact]
    public void There_are_six_food_stalls_four_drink_stalls_and_three_shops_each_with_a_menu()
    {
        Assert.Equal(new[] { StallKind.HotDogs, StallKind.Burgers, StallKind.Fries, StallKind.Pizza, StallKind.Noodles, StallKind.IceCream }, Catalogue.StallsIn(StallGroup.Food).Select(s => s.Kind));
        Assert.Equal(new[] { StallKind.Bar, StallKind.Coffee, StallKind.Slush, StallKind.Cocktails }, Catalogue.StallsIn(StallGroup.Drink).Select(s => s.Kind));
        Assert.Equal(new[] { StallKind.Ponchos, StallKind.BucketHats, StallKind.Merchandise, StallKind.PhoneCharging }, Catalogue.StallsIn(StallGroup.Other).Select(s => s.Kind));
        Assert.Equal(new[] { StallKind.Toilets, StallKind.ToiletBlock, StallKind.WaterPoint, StallKind.Bin }, Catalogue.StallsIn(StallGroup.Services).Select(s => s.Kind));

        foreach (var kind in Enum.GetValues<StallKind>())
        {
            var menu = Catalogue.MenuOf(kind);
            Assert.Equal(Selling.Contains(kind), Catalogue.Sells(kind));
            if (!Catalogue.Sells(kind))
            {
                Assert.Empty(menu);
                continue;
            }

            // A variety of things, each with a name of its own and a price, and something anyone can buy.
            Assert.InRange(menu.Count, 3, 4);
            Assert.Equal(menu.Count, menu.Select(m => m.Name).Distinct().Count());
            Assert.All(menu, m => Assert.True(m.UsualPrice >= 2 && m.Name.Length > 2));
            Assert.Contains(menu, m => !m.AdultsOnly && !m.ForChildren);
            Assert.True(Catalogue.HasQueue(kind));
            Assert.Equal(Catalogue.GroupOf(kind) == StallGroup.Food ? Need.Food : Catalogue.GroupOf(kind) is StallGroup.Drink or StallGroup.Vip ? Need.Drink : null, Catalogue.Of(kind).Serves);
        }

        // What is bought is carried or worn.
        Assert.All(Catalogue.MenuOf(StallKind.Bar), m => Assert.Contains(m.Gives, new[] { Carried.Pint, Carried.Cup }));
        Assert.All(Catalogue.MenuOf(StallKind.Coffee), m => Assert.Equal(Carried.Coffee, m.Gives));
        Assert.All(Catalogue.MenuOf(StallKind.Fries), m => Assert.Equal(Carried.Fries, m.Gives));
        Assert.All(Catalogue.MenuOf(StallKind.Merchandise), m => Assert.Equal(Carried.Bag, m.Gives));
        Assert.All(Catalogue.MenuOf(StallKind.Ponchos), m => Assert.Equal(Worn.Poncho, m.Wears));
        Assert.All(Catalogue.MenuOf(StallKind.BucketHats), m => Assert.Equal(Worn.BucketHat, m.Wears));
        Assert.All(Selling.Where(k => Catalogue.GroupOf(k) != StallGroup.Other), k => Assert.All(Catalogue.MenuOf(k), m => Assert.NotEqual(Carried.None, m.Gives)));
    }

    [Fact]
    public void Prices_are_set_on_a_stall_before_the_gates_open_and_fixed_once_they_are()
    {
        var game = FestivalGame.NewFestival("Larkmoor Festival");
        game.ChooseHeadliner(0);
        Assert.False(game.CanSetPrices);
        Assert.True(game.ConfirmPlan());
        Assert.Equal(Phase.Build, game.Phase);
        Assert.True(game.CanSetPrices);

        var f = game.Field.Field;
        Assert.True(game.BuildStall(StallKind.Burgers, f.X + 20, f.Y + 20, Facing.South).Ok);
        Assert.True(game.BuildStall(StallKind.Toilets, f.X + 40, f.Y + 20, Facing.South).Ok);
        var van = game.Site.Stalls[0];
        Assert.Null(van.Prices);
        Assert.Equal(new[] { 6, 7, 6 }, Enumerable.Range(0, 3).Select(van.PriceOf));
        Assert.Equal(1, van.Dearness(0));

        var changed = 0;
        game.Site.PricesChanged += () => changed++;
        Assert.Equal(9, game.SetPrice(0, 0, 9));
        Assert.Equal(1, changed);
        Assert.Equal(new[] { 9, 7, 6 }, Enumerable.Range(0, 3).Select(game.Site.Stalls[0].PriceOf));
        Assert.Equal(1.5, game.Site.Stalls[0].Dearness(0));
        // It is still the same stall in the same place, and nothing was paid or refunded.
        Assert.Equal((van.Kind, van.X, van.Y, van.Facing), (game.Site.Stalls[0].Kind, game.Site.Stalls[0].X, game.Site.Stalls[0].Y, game.Site.Stalls[0].Facing));

        // Never free, never more than three times the usual price.
        Assert.Equal(1, game.SetPrice(0, 1, -4));
        Assert.Equal(18, game.SetPrice(0, 2, 500));
        Assert.Null(game.SetPrice(0, 3, 5));
        Assert.Null(game.SetPrice(5, 0, 5));
        Assert.Null(game.SetPrice(1, 0, 5));

        Assert.True(game.ResetPrices(0));
        Assert.Equal(new[] { 6, 7, 6 }, Enumerable.Range(0, 3).Select(game.Site.Stalls[0].PriceOf));
        Assert.False(game.ResetPrices(1));

        // The prices go into the save with the stall.
        Assert.Equal(4, game.SetPrice(0, 0, 4));
        var loaded = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));
        Assert.Equal(new[] { 4, 7, 6 }, Enumerable.Range(0, 3).Select(loaded.Site.Stalls[0].PriceOf));

        // On the morning itself they can still be changed, until the gates open.
        game.StartDay();
        Assert.True(game.GatesShut && game.CanSetPrices);
        Assert.Equal(5, game.SetPrice(0, 0, 5));
        game.OpenGates();
        Assert.False(game.CanSetPrices);
        Assert.Null(game.SetPrice(0, 0, 6));
        Assert.False(game.ResetPrices(0));
        Assert.Equal(5, game.Site.Stalls[0].PriceOf(0));
    }

    [Fact]
    public void Cheap_prices_sell_more_and_dear_prices_sell_less()
    {
        var (cheap, usual, dear) = (Cheap.Value, Usual.Value, Dear.Value);
        Assert.True(Sold(usual) > 150, $"At the usual prices {Sold(usual)} sold");
        Assert.True(Sold(cheap) > Sold(usual) * 1.05, $"Cheap {Sold(cheap)}, usual {Sold(usual)}");
        Assert.True(Sold(dear) < Sold(usual) * 0.75, $"Dear {Sold(dear)}, usual {Sold(usual)}");

        // Treats and souvenirs are what people most easily go without, or are most easily tempted by.
        Assert.True(Sold(cheap, StallKind.IceCream) > Sold(usual, StallKind.IceCream), $"Ice creams: cheap {Sold(cheap, StallKind.IceCream)}, usual {Sold(usual, StallKind.IceCream)}");
        Assert.True(Sold(dear, StallKind.Merchandise) < Sold(usual, StallKind.Merchandise), $"Merchandise: dear {Sold(dear, StallKind.Merchandise)}, usual {Sold(usual, StallKind.Merchandise)}");

        // Nobody is priced out at the usual prices or below. At nearly double, a good many go without.
        Assert.Equal(0, usual.PricedOut);
        Assert.Equal(0, cheap.PricedOut);
        Assert.True(dear.PricedOut > 30, $"Priced out {dear.PricedOut}");
        Assert.Equal(100, usual.WouldBuy(0));
        Assert.InRange(dear.WouldBuy(0), 30, 60);
        Assert.Equal(100, dear.WouldBuy(2));
    }

    [Fact]
    public void Dear_prices_take_more_on_each_sale_but_people_mind()
    {
        var (cheap, usual, dear) = (Cheap.Value, Usual.Value, Dear.Value);
        double Each(FestivalGame game) => (double)game.StallTakings / Sold(game);
        Assert.True(Each(dear) > Each(usual) * 1.5 && Each(usual) > Each(cheap) * 1.5, $"Each sale: cheap £{Each(cheap):0.00}, usual £{Each(usual):0.00}, dear £{Each(dear):0.00}");
        Assert.True(cheap.StallTakings < usual.StallTakings, $"Takings: cheap £{cheap.StallTakings}, usual £{usual.StallTakings}");

        Assert.True(cheap.Crowd.Happiness > usual.Crowd.Happiness, $"Happy: cheap {cheap.Crowd.Happiness}, usual {usual.Crowd.Happiness}");
        Assert.True(dear.Crowd.Happiness < usual.Crowd.Happiness - 3, $"Happy: dear {dear.Crowd.Happiness}, usual {usual.Crowd.Happiness}");

        // The festival's share is a quarter of what the stalls take, and what each party spent adds up to it.
        foreach (var game in new[] { cheap, usual, dear })
        {
            Assert.Equal(game.StallTakings, game.Crowd.Parties.Sum(p => p.Spent));
            Assert.True(game.TotalIncome >= game.StallTakings * 0.23 && game.TotalIncome <= game.StallTakings * 0.27, string.Join("; ", game.Income.Select(l => l.ToString())) + " takings " + game.StallTakings);
            Assert.Equal(game.ItemsSold, Sold(game));
        }

        Assert.Contains(usual.Income, l => l.What == "Food stalls");
        Assert.Contains(usual.Income, l => l.What == "Drink stalls");
        Assert.Contains(usual.Income, l => l.What == "Ponchos, hats and merchandise");
    }

    [Fact]
    public void Nobody_pays_more_than_they_will_stretch_to_and_children_are_not_sold_beer()
    {
        // A bar where the only thing a child may have, the soft drink, costs three times what it should.
        var game = new FestivalGame("Dear Pop", seed: 8);
        var f = game.Field.Field;
        Assert.True(game.BuildStall(StallKind.Bar, f.X + 40, f.Y + 20, Facing.South).Ok);
        Assert.Equal(6, game.Site.SetPrice(0, 3, 6));
        var soldTo = new List<(int Item, int Price)>();
        game.Crowd.Sold += (stall, item, price) => soldTo.Add((item, price));
        var young = new HashSet<int>(game.Crowd.Parties.SelectMany(p => p.Members).Where(m => m.Look.Age is AgeGroup.Child or AgeGroup.Teenager).Select(m => m.Id));
        Assert.True(young.Count > 10);
        var sawYoungHolding = false;
        while (game.Now < Day.AddHours(16))
        {
            game.Clock.Step();
            sawYoungHolding |= game.Crowd.People.Any(p => young.Contains(p.Id) && p.Holding != Carried.None);
        }

        Assert.True(soldTo.Count > 60, $"{soldTo.Count} drinks sold");
        Assert.DoesNotContain(soldTo, s => s.Item == 3);
        Assert.All(soldTo, s => Assert.Equal(game.Site.Stalls[0].PriceOf(s.Item), s.Price));
        Assert.False(sawYoungHolding, "Nobody under age should be holding a drink from this bar.");
        Assert.Equal(new[] { 0, 0, 0 }.Length + 1, game.SalesOf(0).Sold.Count);
        Assert.Equal(soldTo.Count, game.SalesOf(0).Items);
        Assert.Equal(soldTo.Sum(s => s.Price), game.SalesOf(0).Takings);

        // Every party has its own limit, spread from the usual price to well over double.
        var stretch = game.Crowd.Parties.Select(p => p.Stretch).ToList();
        Assert.All(stretch, s => Assert.InRange(s, 1, 2.4));
        Assert.True(stretch.Min() < 1.15 && stretch.Max() > 2.25 && Math.Abs(stretch.Average() - 1.7) < 0.12);
        Assert.Equal(stretch, new FestivalGame("Dear Pop", seed: 8).Crowd.Parties.Select(p => p.Stretch));
    }

    [Fact]
    public void People_walk_away_with_what_they_bought_in_their_hand_until_it_is_finished()
    {
        var game = new FestivalGame("In Hand", seed: 8);
        var f = game.Field.Field;
        Assert.True(game.BuildStall(StallKind.Fries, f.X + 30, f.Y + 20, Facing.South).Ok);
        Assert.True(game.BuildStall(StallKind.Coffee, f.X + 50, f.Y + 20, Facing.South).Ok);
        var seen = new HashSet<Carried>();
        Party? buyer = null;
        var boughtAt = default(DateTime);
        while (game.Now < Day.AddHours(15))
        {
            game.Clock.Step();
            foreach (var person in game.Crowd.People)
            {
                seen.Add(person.Holding);
            }

            if (buyer is null && game.Crowd.Parties.FirstOrDefault(p => p.Spent > 0) is { } first)
            {
                // The moment they are served, each of them has it in their hand.
                (buyer, boughtAt) = (first, game.Now);
                Assert.All(first.Members, m => Assert.Contains(m.Holding, new[] { Carried.Fries, Carried.Coffee }));
                Assert.InRange((first.HoldUntil - game.Now).TotalMinutes, 4, 18);
                Assert.Equal(game.Crowd.Untidy(first) ? first.HoldUntil : default, first.LitterAt);
            }

            if (buyer is not null && game.Now == buyer.HoldUntil && buyer.Spent <= 5 * buyer.Members.Count)
            {
                Assert.All(buyer.Members, m => Assert.Equal(Carried.None, m.Holding));
            }
        }

        Assert.NotNull(buyer);
        Assert.Equal(new[] { Carried.None, Carried.Fries, Carried.Coffee }.OrderBy(c => c), seen.OrderBy(c => c));

        // Fries are a snack: they take the edge off, where a burger is a meal.
        Assert.All(Catalogue.MenuOf(StallKind.Fries), m => Assert.InRange(m.Fills, 0.5, 0.85));
        Assert.All(Catalogue.MenuOf(StallKind.Burgers), m => Assert.Equal(1, m.Fills));
    }

    [Fact]
    public void Merchandise_goes_home_in_a_bag_and_hats_and_ponchos_are_worn()
    {
        var hot = PricedDay(1, 21, seed: 8, weather: WeatherKind.Hot);
        var bags = hot.Crowd.Parties.SelectMany(p => p.Members).Where(m => m.Holding == Carried.Bag).ToList();
        Assert.True(bags.Count >= 8, $"{bags.Count} carrying a bag");
        Assert.True(bags.Count <= Sold(hot, StallKind.Merchandise));
        Assert.DoesNotContain(bags, m => m.Look.Age == AgeGroup.Child);
        // A bag is kept all day, long after any food or drink is finished.
        Assert.Contains(hot.Crowd.Parties, p => p.BoughtMerch && p.HoldUntil == default && p.Members.Any(m => m.Holding == Carried.Bag));
        Assert.All(hot.Crowd.Parties.Where(p => p.Members.Any(m => m.Holding == Carried.Bag)), p => Assert.True(p.BoughtMerch));

        // A hat stall on a hot day: the hats sold are the hats on heads.
        var game = new FestivalGame("Hats", seed: 8);
        game.SetWeather(DayWeather.Of(WeatherKind.Hot, 3));
        var f = game.Field.Field;
        Assert.True(game.BuildStall(StallKind.BucketHats, f.X + 30, f.Y + 20, Facing.South).Ok);
        Assert.True(game.BuildStall(StallKind.Ponchos, f.X + 50, f.Y + 20, Facing.South).Ok);
        var before = game.Crowd.Parties.SelectMany(p => p.Members).ToDictionary(m => m.Id, m => m.Look);
        RunUntil(game, 19);
        var hats = game.Crowd.Parties.SelectMany(p => p.Members).Where(m => (m.Bought & 2) != 0).ToList();
        Assert.True(hats.Count >= 8, $"{hats.Count} hats");
        Assert.Equal(hats.Count, game.SalesOf(0).Items);
        Assert.All(hats, m =>
        {
            Assert.Equal(Hat.None, before[m.Id].Hat);
            Assert.Equal(Hat.Bucket, m.Look.Hat);
            Assert.Contains(m.Look.HatColour, Crowd.HatStallColours);
            // Only the hat is new: they are the same person underneath.
            Assert.Equal(before[m.Id] with { Hat = Hat.Bucket, HatColour = m.Look.HatColour }, m.Look);
        });
        Assert.All(game.Crowd.Parties.Where(p => p.Members.Any(m => (m.Bought & 2) != 0)), p => Assert.True(p.BoughtHats));
        Assert.Equal(game.Crowd.People.Count(p => p.Look.Hat == Hat.Bucket), game.Crowd.Parties.Where(p => p.IsOnSite).SelectMany(p => p.Members).Count(m => m.Look.Hat == Hat.Bucket));

        // No rain, no ponchos sold. Children's sizes only go to children.
        Assert.Equal(0, game.SalesOf(1).Items);
        var kidsHat = Catalogue.MenuOf(StallKind.BucketHats).ToList().FindIndex(m => m.ForChildren);
        Assert.True(game.SalesOf(0).Sold[kidsHat] <= hats.Count(m => m.Look.Age == AgeGroup.Child));
    }

    [Fact]
    public void Treats_sell_when_the_weather_suits_them()
    {
        var fine = PricedDay(1, 17, weather: WeatherKind.Fine);
        var hot = PricedDay(1, 17, weather: WeatherKind.Hot);
        var wet = PricedDay(1, 17, weather: WeatherKind.Rain);
        Assert.True(Sold(hot, StallKind.IceCream) > Sold(fine, StallKind.IceCream) * 1.3, $"Ice creams: hot {Sold(hot, StallKind.IceCream)}, fine {Sold(fine, StallKind.IceCream)}");
        Assert.True(Sold(wet, StallKind.IceCream) < Sold(fine, StallKind.IceCream), $"Ice creams: wet {Sold(wet, StallKind.IceCream)}, fine {Sold(fine, StallKind.IceCream)}");
        Assert.True(Sold(wet, StallKind.Coffee) > Sold(hot, StallKind.Coffee), $"Coffees: wet {Sold(wet, StallKind.Coffee)}, hot {Sold(hot, StallKind.Coffee)}");
        Assert.True(Sold(fine, StallKind.IceCream) > 10, $"{Sold(fine, StallKind.IceCream)} ice creams on a fine day");
        // Still, most of what a food stall sells is meals to hungry people.
        Assert.True(Sold(fine, StallKind.Burgers) + Sold(fine, StallKind.HotDogs) > Sold(fine, StallKind.IceCream), $"Meals {Sold(fine, StallKind.Burgers) + Sold(fine, StallKind.HotDogs)}, ice creams {Sold(fine, StallKind.IceCream)}");
    }

    [Fact]
    public void A_day_saved_with_food_in_hand_and_hats_on_heads_comes_back_the_same()
    {
        var game = PricedDay(1.2, 13.5, weather: WeatherKind.Hot);
        var f = game.Field.Field;
        Assert.True(game.BuildStall(StallKind.BucketHats, f.X + 40, f.Y + 62, Facing.North).Ok);
        RunUntil(game, 15.5);
        Assert.Contains(game.Crowd.People, p => p.Holding is not (Carried.None or Carried.Bag));
        Assert.Contains(game.Crowd.People, p => p.Holding == Carried.Bag);
        Assert.Contains(game.Crowd.Parties.SelectMany(p => p.Members), m => (m.Bought & 2) != 0);

        var loaded = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));
        Assert.Equal(game.Crowd.People.Select(p => (p.Id, p.Holding, p.Look.Hat, p.Look.HatColour)), loaded.Crowd.People.Select(p => (p.Id, p.Holding, p.Look.Hat, p.Look.HatColour)));
        Assert.Equal(game.Crowd.Parties.Select(p => (p.HoldUntil, p.BoughtHats, p.BoughtMerch, p.Stretch)), loaded.Crowd.Parties.Select(p => (p.HoldUntil, p.BoughtHats, p.BoughtMerch, p.Stretch)));
        Assert.Equal(game.StallTakings, loaded.StallTakings);
        Assert.Equal(game.ItemsSold, loaded.ItemsSold);
        Assert.Equal(game.PricedOut, loaded.PricedOut);
        for (var i = 0; i < game.Site.Stalls.Count; i++)
        {
            Assert.Equal(game.SalesOf(i).Sold, loaded.SalesOf(i).Sold);
            Assert.Equal(game.Site.Stalls[i].Prices ?? Array.Empty<int>(), loaded.Site.Stalls[i].Prices ?? Array.Empty<int>());
        }

        // Both carry on exactly alike.
        for (var i = 0; i < 3600; i++)
        {
            game.Clock.Step();
            loaded.Clock.Step();
        }

        Assert.Equal(game.Money, loaded.Money);
        Assert.Equal(game.ItemsSold, loaded.ItemsSold);
        Assert.Equal(game.Crowd.People.Select(p => (p.Id, p.X, p.Y, p.Holding)), loaded.Crowd.People.Select(p => (p.Id, p.X, p.Y, p.Holding)));
    }

    [Fact]
    public void A_free_tap_means_nobody_is_priced_out_of_a_drink()
    {
        static FestivalGame Bar(bool tap)
        {
            var game = new FestivalGame("Thirsty", seed: 8);
            var f = game.Field.Field;
            Assert.True(game.BuildStall(StallKind.Bar, f.X + 40, f.Y + 20, Facing.South).Ok);
            for (var i = 0; i < 4; i++)
            {
                game.Site.SetPrice(0, i, Catalogue.MenuOf(StallKind.Bar)[i].UsualPrice * 3);
            }

            if (tap)
            {
                Assert.True(game.BuildStall(StallKind.WaterPoint, f.X + 60, f.Y + 20, Facing.South).Ok);
            }

            while (game.Now < Day.AddHours(15))
            {
                game.Clock.Step();
            }

            return game;
        }

        var dry = Bar(false);
        var tapped = Bar(true);
        Assert.Equal(0, dry.SalesOf(0).Items);
        Assert.True(dry.PricedOut > 100, $"Priced out {dry.PricedOut}");
        Assert.Equal(0, dry.WouldBuy(0));
        Assert.Equal(0, tapped.PricedOut);
        Assert.True(tapped.Crowd.Content(Need.Drink) > dry.Crowd.Content(Need.Drink) + 30);
        Assert.True(tapped.Crowd.Happiness > dry.Crowd.Happiness);
    }
}
