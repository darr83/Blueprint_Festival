using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

/// <summary>The fairground: the rides, the prices the player sets on them, and the teddies won at hook a duck.</summary>
public class RideTests
{
    private static readonly DateTime Day = GameClock.DefaultStart.Date;

    private static readonly StallKind[] Fair = { StallKind.Carousel, StallKind.BigWheel, StallKind.Waltzer, StallKind.Twister, StallKind.Skylark, StallKind.HookADuck };

    private static void RunUntil(FestivalGame game, double hour)
    {
        var until = Day.AddHours(hour);
        while (game.Now < until)
        {
            game.Clock.Step();
        }
    }

    /// <summary>A village day with a carousel, a twister and hook a duck, every fairground price multiplied by the same amount.</summary>
    private static FestivalGame FairDay(double times, double untilHour = 18, int seed = 8)
    {
        var game = new FestivalGame("Fair", seed: seed);
        game.SetWeather(DayWeather.Of(WeatherKind.Fine, 3));
        var f = game.Field.Field;
        Assert.True(game.BuildStall(StallKind.Burgers, f.Right - 16, f.Y + 4, Facing.West).Ok);
        Assert.True(game.BuildStall(StallKind.Bar, f.Right - 16, f.Y + 20, Facing.West).Ok);
        Assert.True(game.BuildStall(StallKind.Toilets, f.Right - 12, f.Y + 36, Facing.West).Ok);
        Assert.True(game.BuildStall(StallKind.WaterPoint, f.Right - 8, f.Y + 48, Facing.West).Ok);
        Assert.True(game.BuildStall(StallKind.Carousel, f.X + 4, f.Y + 4, Facing.South).Ok);
        Assert.True(game.BuildStall(StallKind.Twister, f.X + 30, f.Y + 4, Facing.South).Ok);
        Assert.True(game.BuildStall(StallKind.HookADuck, f.X + 4, f.Y + 40, Facing.East).Ok);
        for (var s = 0; s < game.Site.Stalls.Count; s++)
        {
            var stall = game.Site.Stalls[s];
            if (Rides.IsFair(stall.Kind))
            {
                game.Site.SetPrice(s, 0, (int)Math.Round(Catalogue.MenuOf(stall.Kind)[0].UsualPrice * times));
            }
        }

        RunUntil(game, untilHour);
        return game;
    }

    private static int Goes(FestivalGame game, StallKind? kind = null) =>
        Enumerable.Range(0, game.Site.Stalls.Count).Where(i => kind is null ? Rides.IsFair(game.Site.Stalls[i].Kind) : game.Site.Stalls[i].Kind == kind).Sum(i => game.SalesOf(i).Items);

    private static readonly Lazy<FestivalGame> Usual = new(() => FairDay(1));

    [Fact]
    public void The_fairground_is_five_rides_and_hook_a_duck_each_with_one_price_to_set()
    {
        Assert.Equal(Fair, Catalogue.StallsIn(StallGroup.Fair).Select(s => s.Kind));
        foreach (var kind in Fair)
        {
            var info = Catalogue.Of(kind);
            Assert.Equal(kind, info.Kind);
            Assert.True(Rides.IsFair(kind) && Catalogue.Sells(kind) && Catalogue.HasQueue(kind));
            Assert.Equal(kind != StallKind.HookADuck, Rides.IsRide(kind));
            var item = Assert.Single(Catalogue.MenuOf(kind));
            Assert.InRange(item.UsualPrice, 2, 4);
            Assert.True(info.Cost > 0 && info.Servers > 0 && info.About.Length > 10);
            Assert.Equal("Fairground", FestivalGame.TakingsLine(kind));
        }

        Assert.False(Rides.IsFair(StallKind.Burgers));
        Assert.Equal(0, Rides.Pull(StallKind.Skylark, hasChild: true));
        Assert.True(Rides.Pull(StallKind.Carousel, true) > Rides.Pull(StallKind.Carousel, false));
    }

    [Fact]
    public void The_player_sets_the_price_of_a_ride_before_the_gates_open()
    {
        var game = new FestivalGame("Fair", seed: 3);
        var f = game.Field.Field;
        var before = game.Money;
        Assert.True(game.BuildStall(StallKind.Carousel, f.X + 4, f.Y + 4, Facing.South).Ok);
        Assert.Equal(before - Catalogue.Of(StallKind.Carousel).Cost, game.Money);
        Assert.Equal(2, game.Site.Stalls[0].PriceOf(0));
        Assert.Equal(5, game.Site.SetPrice(0, 0, 5));
        Assert.Equal(5, game.Site.Stalls[0].PriceOf(0));
        Assert.Equal(6, game.Site.SetPrice(0, 0, 50));
        Assert.Equal(1, game.Site.SetPrice(0, 0, 0));

        // It comes down again for its money.
        Assert.Equal(Catalogue.Of(StallKind.Carousel).Cost, game.Remove(new TilePoint(f.X + 6, f.Y + 6)));
        Assert.Empty(game.Site.Stalls);
    }

    [Fact]
    public void The_fairground_cannot_go_in_a_camping_field()
    {
        // The old single field with its camping field next door, and a festival planned on the estate map.
        var plan = new FieldPlan(new[] { new FieldChoice("home", FieldUse.Music), new FieldChoice("far", FieldUse.Food), new FieldChoice("glebe", FieldUse.Camping) });
        foreach (var map in new[] { FieldMap.ForLicence(5000), FieldMap.ForPlan(plan, 5000) })
        {
            var site = new SiteLayout(map);
            TilePoint Middle(FieldUse use)
            {
                var tiles = Enumerable.Range(0, map.Width).SelectMany(x => Enumerable.Range(0, map.Height).Select(y => new TilePoint(x, y))).Where(p => map.UseAt(p) == use).ToList();
                Assert.NotEmpty(tiles);
                return new TilePoint((int)tiles.Average(p => p.X) - 16, (int)tiles.Average(p => p.Y) - 16);
            }

            var camp = Middle(FieldUse.Camping);
            var music = Middle(FieldUse.Music);
            foreach (var kind in Fair)
            {
                var check = site.CheckStall(kind, camp.X, camp.Y, Facing.South);
                Assert.False(check.Ok);
                Assert.Contains("camping", check.Problem);
                Assert.True(site.CheckStall(kind, music.X, music.Y, Facing.South).Ok, $"{kind} should fit in the music field.");
            }

            // It is only the fairground that is kept out for being a fairground.
            Assert.DoesNotContain("fairground", site.CheckStall(StallKind.Coffee, camp.X, camp.Y, Facing.South).Problem ?? "");
            if (map.FromPlan)
            {
                var food = Middle(FieldUse.Food);
                Assert.All(Fair, kind => Assert.True(site.CheckStall(kind, food.X, food.Y, Facing.South).Ok, $"{kind} should fit in the food field."));
            }
        }
    }

    [Fact]
    public void A_ride_stands_to_load_then_runs_and_everyone_gets_off_together()
    {
        var ride = new PlacedStall(StallKind.Carousel, 10, 20, Facing.South);
        var cycle = Rides.CycleSeconds(StallKind.Carousel);
        Assert.Equal(Rides.BoardingSeconds + Rides.RunSeconds(StallKind.Carousel), cycle);
        var boarding = 0;
        for (var s = 0; s < cycle; s++)
        {
            var turn = Rides.TurnAt(ride, Day.AddHours(12).AddSeconds(s));
            boarding += turn.Boarding ? 1 : 0;
            Assert.InRange(turn.Left, 1, cycle);
            Assert.Equal(turn.Boarding, turn.Running == 0 && turn.Left > Rides.RunSeconds(StallKind.Carousel));
        }

        Assert.Equal(Rides.BoardingSeconds, boarding);

        // On the day: nobody gets on while it is going round, and it empties when it stops.
        var game = FairDay(1, 11);
        var carousel = game.Site.Stalls.ToList().FindIndex(s => s.Kind == StallKind.Carousel);
        var placed = game.Site.Stalls[carousel];
        var most = 0;
        var last = int.MaxValue;
        var until = Day.AddHours(15);
        while (game.Now < until)
        {
            game.Clock.Step();
            var riding = game.Crowd.Riding(carousel);
            var turn = Rides.TurnAt(placed, game.Now);
            if (!turn.Boarding)
            {
                // The last few on are still sitting down as it starts.
                Assert.True(riding <= last || turn.Running <= 45, $"{riding} riding after {last} at {game.Now:T}, {turn.Running} s into the ride.");
            }

            Assert.InRange(riding, 0, Catalogue.Of(StallKind.Carousel).Servers * Rides.SeatsEach);
            Assert.All(game.Crowd.People.Where(p => p.Ride == carousel), p => Assert.InRange(p.RideSeat, 0, (Catalogue.Of(StallKind.Carousel).Servers * Rides.SeatsEach) - 1));
            most = Math.Max(most, riding);
            last = riding;
        }

        Assert.True(most >= 2, "Somebody should have been on the carousel.");
        Assert.Equal(game.Crowd.People.Count(p => p.Ride == carousel), game.Crowd.Riding(carousel));
    }

    [Fact]
    public void People_pay_to_ride_and_the_festival_keeps_its_share()
    {
        var game = Usual.Value;
        Assert.True(Goes(game, StallKind.Carousel) > 0, "Nobody rode the carousel.");
        Assert.True(Goes(game, StallKind.Twister) > 0, "Nobody rode the twister.");
        Assert.True(Goes(game, StallKind.HookADuck) > 0, "Nobody hooked a duck.");
        var takings = Enumerable.Range(0, game.Site.Stalls.Count).Where(i => Rides.IsFair(game.Site.Stalls[i].Kind)).Sum(i => game.SalesOf(i).Takings);
        Assert.True(takings > 0);
        Assert.True(game.Crowd.Parties.Sum(p => p.FairGoes) > 0);
        Assert.Contains(game.Crowd.Parties, p => p.FairGoes > 0 && p.Spent > 0);
    }

    [Fact]
    public void Hook_a_duck_wins_some_people_a_big_teddy_to_carry_all_day()
    {
        var game = Usual.Value;
        var goes = Goes(game, StallKind.HookADuck);
        var teddies = game.Crowd.TeddiesWon;
        Assert.True(teddies > 0, $"{goes} goes and no teddy.");
        Assert.True(teddies < goes, "Not every go wins.");
        Assert.Contains(game.Crowd.People, p => p.Holding == Carried.Teddy);

        // Still in their arms hours later, whatever else they buy.
        var holders = game.Crowd.Parties.SelectMany(p => p.Members).Where(m => m.Holding == Carried.Teddy).Select(m => m.Id).ToList();
        var later = FairDay(1, 20);
        Assert.All(holders, id => Assert.Equal(Carried.Teddy, later.Crowd.Parties.SelectMany(p => p.Members).First(m => m.Id == id).Holding));
        Assert.True(Crowd.Keeps(Carried.Teddy) && Crowd.Keeps(Carried.Bag) && !Crowd.Keeps(Carried.Burger));
    }

    [Fact]
    public void Dear_rides_carry_fewer_people_than_cheap_ones()
    {
        var cheap = FairDay(0.5);
        var dear = FairDay(2.5);
        Assert.True(Goes(cheap) > Goes(Usual.Value) && Goes(Usual.Value) > Goes(dear), $"cheap {Goes(cheap)}, usual {Goes(Usual.Value)}, dear {Goes(dear)}");
    }

    [Fact]
    public void A_day_saved_with_people_on_a_ride_carries_on_the_same()
    {
        var game = FairDay(1, 12);
        var until = Day.AddHours(16);
        while (game.Now < until && !game.Crowd.People.Any(p => p.Ride >= 0))
        {
            game.Clock.Step();
        }

        Assert.Contains(game.Crowd.People, p => p.Ride >= 0);
        var loaded = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));
        Assert.Equal(game.Crowd.People.Select(p => (p.Id, p.Ride, p.RideSeat, p.Holding)), loaded.Crowd.People.Select(p => (p.Id, p.Ride, p.RideSeat, p.Holding)));
        Assert.Equal(game.Crowd.Parties.Select(p => p.FairGoes), loaded.Crowd.Parties.Select(p => p.FairGoes));
        for (var i = 0; i < 3600; i++)
        {
            game.Clock.Step();
            loaded.Clock.Step();
        }

        Assert.Equal(game.Money, loaded.Money);
        Assert.Equal(Goes(game), Goes(loaded));
        Assert.Equal(game.Crowd.People.Select(p => (p.Id, p.X, p.Y, p.Ride, p.Holding)), loaded.Crowd.People.Select(p => (p.Id, p.X, p.Y, p.Ride, p.Holding)));
    }
}
