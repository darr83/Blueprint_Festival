using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

public class NeedsTests
{
    private static readonly DateTime Day = GameClock.DefaultStart.Date;

    private static void RunUntil(FestivalGame game, double hour)
    {
        var until = Day.AddHours(hour);
        while (game.Now < until)
        {
            game.Clock.Step();
        }
    }

    private static void CloseTheGap(FestivalGame game)
    {
        var gap = game.Field.Gap;
        var y = gap.Y + 1;
        Assert.True(game.BuildFence(FenceKind.Rope, new TilePoint(gap.X, y), new TilePoint(gap.Right - 1, y)).Ok);
        Assert.True(game.BuildGate(GateKind.ArchGate, gap.X, y, false).Ok);
    }

    [Fact]
    public void Every_stall_has_a_name_a_size_a_price_and_a_need_it_answers()
    {
        Assert.Equal(5, Catalogue.Stalls.Count);
        Assert.All(Enum.GetValues<StallKind>(), k => Assert.Equal(k, Catalogue.Of(k).Kind));
        Assert.All(Catalogue.Stalls, s => Assert.True(s.Cost > 0 && s.Width > 0 && s.Depth > 0 && s.Servers >= 1 && s.SecondsPerPerson > 0 && s.Name.Length > 0));
        Assert.All(Enum.GetValues<Need>(), n => Assert.Contains(Catalogue.Stalls, s => s.Serves == n));
        Assert.Equal(0, Catalogue.Of(StallKind.Toilets).Price);
        Assert.Equal(0, Catalogue.Of(StallKind.WaterPoint).Price);
        Assert.True(Catalogue.Stalls.Sum(s => s.Cost) + Catalogue.Of(StageKind.Bandstand).Cost + Catalogue.Of(GateKind.VillageGate).Cost < FestivalGame.StartingMoney);
    }

    [Fact]
    public void A_stall_costs_its_hire_blocks_the_ground_and_gives_the_money_back_when_removed()
    {
        var game = new FestivalGame();
        var f = game.Field.Field;
        Assert.True(game.BuildStall(StallKind.Bar, f.X + 20, f.Y + 20, Facing.South).Ok);
        Assert.Equal(FestivalGame.StartingMoney - 200, game.Money);
        Assert.Equal(new LedgerLine("Stalls and toilets", 200), Assert.Single(game.Spending));
        Assert.False(game.Field.IsWalkable(new TilePoint(f.X + 21, f.Y + 21)));
        Assert.False(game.CheckStall(StallKind.Toilets, f.X + 25, f.Y + 22, Facing.South).Ok);
        Assert.False(game.CheckStage(StageKind.FarmTrailer, f.X + 22, f.Y + 22, Facing.South).Ok);
        Assert.False(game.CheckFence(FenceKind.Rope, new TilePoint(f.X + 10, f.Y + 22), new TilePoint(f.X + 40, f.Y + 22)).Ok);
        Assert.False(game.CheckStall(StallKind.Bar, f.X - 2, f.Y + 20, Facing.South).Ok);

        // Turned side-on, its width and depth swap.
        Assert.Equal(new TileRect(f.X + 60, f.Y + 20, 8, 12), new PlacedStall(StallKind.Bar, f.X + 60, f.Y + 20, Facing.East).Rect);

        Assert.Equal(200, game.Remove(new TilePoint(f.X + 25, f.Y + 25)));
        Assert.Equal(FestivalGame.StartingMoney, game.Money);
        Assert.Empty(game.Spending);
        Assert.True(game.Field.IsWalkable(new TilePoint(f.X + 21, f.Y + 21)));
    }

    [Fact]
    public void Tickets_are_paid_at_the_gate_and_not_by_people_who_just_walk_in()
    {
        var open = new FestivalGame("Open Field", seed: 3);
        RunUntil(open, 11);
        Assert.True(open.Crowd.WalkedInFree > 30);
        Assert.Empty(open.Income);
        Assert.Equal(FestivalGame.StartingMoney, open.Money);

        var gated = new FestivalGame("Gated", seed: 3);
        CloseTheGap(gated);
        var afterBuilding = gated.Money;
        RunUntil(gated, 11);
        Assert.True(gated.Crowd.ThroughGates > 30);
        Assert.Equal(new LedgerLine("Tickets", gated.Crowd.ThroughGates * Catalogue.TicketPrice), Assert.Single(gated.Income));
        Assert.Equal(afterBuilding + (gated.Crowd.ThroughGates * 5), gated.Money);
    }

    [Fact]
    public void People_get_hungry_and_thirsty_and_go_and_queue_for_what_they_need()
    {
        var game = new FestivalGame("Needs", seed: 8);
        Amenities.Add(game);
        var served = new Dictionary<StallKind, int>();
        game.Crowd.Served += (kind, people, _) => served[kind] = served.GetValueOrDefault(kind) + people;
        var sawQueue = false;
        while (game.Now < Day.AddHours(16))
        {
            game.Clock.Step();
            foreach (var party in game.Crowd.Parties.Where(p => p.State == PartyState.AtStall && !p.IsWalking))
            {
                // A queue stands on open ground in front of its stall, not inside it.
                sawQueue = true;
                Assert.True(game.Field.IsWalkable(FieldMap.TileAt(party.X, party.Y)));
            }
        }

        Assert.True(sawQueue);
        Assert.True(served[StallKind.FoodVan] > 40, $"{served[StallKind.FoodVan]} fed");
        Assert.True(served[StallKind.Bar] + served[StallKind.WaterPoint] > 60);
        Assert.True(served[StallKind.Toilets] > 40);
        Assert.Contains(game.Income, l => l.What == "Food van" && l.Amount == served[StallKind.FoodVan] * 6);
        Assert.Contains(game.Income, l => l.What == "Bar tent" && l.Amount == served[StallKind.Bar] * 4);
        Assert.DoesNotContain(game.Income, l => l.What is "Toilets" or "Water point");
        Assert.Equal(game.Crowd.Parties.Sum(p => p.Spent), game.TotalIncome);
        Assert.Equal(FestivalGame.StartingMoney - game.TotalSpending + game.TotalIncome, game.Money);
        Assert.True(game.Crowd.Content(Need.Food) > 80 && game.Crowd.Content(Need.Drink) > 80 && game.Crowd.Content(Need.Toilet) > 80);
    }

    [Fact]
    public void Someone_served_stops_needing_it_and_a_drink_makes_the_toilet_more_pressing()
    {
        var game = new FestivalGame("One Bar", seed: 8);
        var f = game.Field.Field;
        Assert.True(game.BuildStall(StallKind.Bar, f.X + 40, f.Y + 20, Facing.South).Ok);
        Party? drinker = null;
        var toiletBefore = 0.0;
        var before = new Dictionary<int, double>();
        game.Crowd.Served += (_, _, _) => { };
        while (game.Now < Day.AddHours(13) && drinker is null)
        {
            foreach (var p in game.Crowd.Parties)
            {
                before[p.Id] = p.Toilet;
            }

            game.Clock.Step();
            drinker = game.Crowd.Parties.FirstOrDefault(p => p.Spent > 0);
            toiletBefore = drinker is null ? 0 : before[drinker.Id];
        }

        Assert.NotNull(drinker);
        Assert.Equal(0, drinker.Thirst, 3);
        Assert.True(drinker.Toilet > toiletBefore + 0.15);
        Assert.Equal(PartyState.Standing, drinker.State);
        Assert.Equal(4 * drinker.Members.Count, drinker.Spent);
    }

    [Fact]
    public void With_too_little_food_the_queue_grows_and_people_give_up()
    {
        var game = new FestivalGame("One Table", seed: 8);
        var f = game.Field.Field;
        Assert.True(game.BuildStall(StallKind.TeaAndCake, f.X + 40, f.Y + 20, Facing.South).Ok);
        var longest = 0;
        while (game.Now < Day.AddHours(17))
        {
            game.Clock.Step();
            longest = Math.Max(longest, game.Crowd.StallQueueLength(0, 0));
        }

        Assert.True(longest >= 6 && game.Crowd.GaveUpQueuing > 0, $"longest queue {longest}, gave up {game.Crowd.GaveUpQueuing}");
    }

    [Fact]
    public void A_festival_with_nothing_to_eat_or_drink_sends_people_home_unhappy()
    {
        var bare = new FestivalGame("Bare Field", seed: 8);
        var good = new FestivalGame("Good Day", seed: 8);
        Amenities.Add(good);
        Assert.Equal(65, bare.Crowd.Happiness);
        RunUntil(bare, 19);
        RunUntil(good, 19);

        Assert.True(bare.Crowd.WentHomeUnhappy > bare.Crowd.Expected / 4, $"{bare.Crowd.WentHomeUnhappy} went home");
        Assert.True(bare.Crowd.Happiness < 35);
        Assert.Equal(0, good.Crowd.WentHomeUnhappy);
        Assert.True(good.Crowd.Happiness >= 60, $"happiness {good.Crowd.Happiness}");
        Assert.True(good.Crowd.OnSite > bare.Crowd.OnSite);
    }

    [Fact]
    public void People_only_show_a_want_once_it_is_pressing()
    {
        var game = new FestivalGame("Bubbles", seed: 8);
        RunUntil(game, 9);
        Assert.DoesNotContain(game.Crowd.People, p => p.Wants is not null);
        RunUntil(game, 15);
        Assert.Contains(game.Crowd.People, p => p.Wants == Need.Drink);
        Assert.All(game.Crowd.Parties.Where(p => p.Wants is { } w && p.IsOnSite), p => Assert.True(p.Level(p.Wants!.Value) >= Crowd.Pressing));
    }

    [Fact]
    public void Taking_a_stall_down_sends_its_queue_away_and_nobody_is_left_stuck()
    {
        var game = new FestivalGame("Take Down", seed: 8);
        var f = game.Field.Field;
        Assert.True(game.BuildStall(StallKind.TeaAndCake, f.X + 40, f.Y + 20, Facing.South).Ok);
        RunUntil(game, 11);
        while (game.Crowd.AtStalls < 4 && game.Now < Day.AddHours(16))
        {
            game.Clock.Step();
        }

        Assert.True(game.Crowd.AtStalls >= 4);
        Assert.Equal(30, game.Remove(new TilePoint(f.X + 41, f.Y + 21)));
        Assert.Equal(0, game.Crowd.AtStalls);
        game.Clock.Step();
        Assert.DoesNotContain(game.Crowd.Parties, p => p.State == PartyState.AtStall);
        Assert.True(game.Crowd.OnSite > 0);
    }

    [Fact]
    public void A_day_saved_with_queues_at_the_stalls_carries_on_exactly_the_same()
    {
        var game = new FestivalGame("Saved Stalls", seed: 8);
        CloseTheGap(game);
        Amenities.Add(game);
        RunUntil(game, 13.2);
        Assert.True(game.Crowd.AtStalls > 0);

        var copy = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));
        Assert.Equal(game.Money, copy.Money);
        Assert.Equal(game.Income, copy.Income);
        Assert.Equal(game.Spending, copy.Spending);
        Assert.Equal(game.Site.Stalls, copy.Site.Stalls);
        Assert.Equal(game.Crowd.AtStalls, copy.Crowd.AtStalls);
        Assert.Equal(game.Crowd.Happiness, copy.Crowd.Happiness);

        RunUntil(game, 15);
        RunUntil(copy, 15);
        Assert.Equal(game.Money, copy.Money);
        Assert.Equal(game.Crowd.Happiness, copy.Crowd.Happiness);
        Assert.Equal(game.Crowd.Parties.Select(p => (p.State, Math.Round(p.X, 3), Math.Round(p.Y, 3), Math.Round(p.Hunger, 6), p.Spent)),
            copy.Crowd.Parties.Select(p => (p.State, Math.Round(p.X, 3), Math.Round(p.Y, 3), Math.Round(p.Hunger, 6), p.Spent)));
    }

    [Fact]
    public void A_save_from_before_stalls_still_loads()
    {
        var game = new FestivalGame("Old Save", seed: 8);
        RunUntil(game, 10);
        var old = game.Save() with { Version = 3, Income = null, Spending = null };
        var json = old.ToJson().Replace("\"Mood\"", "\"WasMood\"", StringComparison.Ordinal);
        var loaded = FestivalGame.Load(SaveData.FromJson(json));
        Assert.Equal(game.Crowd.OnSite, loaded.Crowd.OnSite);
        Assert.Empty(loaded.Income);
        Assert.All(loaded.Crowd.Parties, p => Assert.Equal(Crowd.StartingMood, p.Mood));
        RunUntil(loaded, 11);
        Assert.True(loaded.Crowd.OnSite > 0);
    }
}
