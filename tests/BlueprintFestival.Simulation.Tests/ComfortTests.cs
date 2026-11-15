using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

/// <summary>Seats, hand washing, shade, the phone charging tent and the new floors.</summary>
public class ComfortTests
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

    /// <summary>A village day with food, drink and toilets down the east hedge, and whatever comforts a test adds.</summary>
    private static FestivalGame Festival(WeatherKind weather, Action<FestivalGame, TileRect>? comforts = null, int seed = 8)
    {
        var game = new FestivalGame("Comforts", seed: seed);
        game.SetWeather(DayWeather.Of(weather, 3));
        Amenities.Add(game);
        comforts?.Invoke(game, game.Field.Field);
        return game;
    }

    /// <summary>Picnic tables and benches in front of the burger van and the bar.</summary>
    private static void Tables(FestivalGame game, TileRect f)
    {
        var x = f.Right - 34;
        for (var i = 0; i < 4; i++)
        {
            Assert.True(game.BuildComfort(ComfortKind.PicnicTable, x, f.Y + 6 + (i * 7), Facing.South).Ok);
            Assert.True(game.BuildComfort(ComfortKind.Bench, x - 8, f.Y + 6 + (i * 7), Facing.East).Ok);
        }
    }

    private static readonly Lazy<FestivalGame> Plain = new(() =>
    {
        var game = Festival(WeatherKind.Fine);
        RunUntil(game, 15);
        return game;
    });

    private static readonly Lazy<(FestivalGame Game, int MostSeated, bool Overbooked)> Seated = new(() =>
    {
        var game = Festival(WeatherKind.Fine, Tables);
        var most = 0;
        var over = false;
        var until = Day.AddHours(15);
        while (game.Now < until)
        {
            game.Clock.Step();
            if (game.Clock.TickCount % 60 == 0)
            {
                most = Math.Max(most, game.Crowd.Seated);
                over |= game.Crowd.Seated > game.Seats;
            }
        }

        return (game, most, over);
    });

    [Fact]
    public void There_are_nine_comforts_each_with_a_price_and_a_size()
    {
        Assert.Equal(9, Comforts.Everything.Count);
        Assert.Equal(Enum.GetValues<ComfortKind>(), Comforts.Everything.Select(c => c.Kind));
        Assert.All(Comforts.Everything, c => Assert.True(c.Cost > 0 && c.Width > 0 && c.Depth > 0 && c.Name.Length > 2 && c.About.Length > 10));
        Assert.Equal(12, Comforts.Of(ComfortKind.Bench).Cost);
        Assert.Equal(3, Comforts.Of(ComfortKind.Bench).Seats.Length);
        Assert.Equal(24, Comforts.Of(ComfortKind.SeatingMarquee).Seats.Length);
        Assert.True(Comforts.Of(ComfortKind.ParasolTable).Roofed && Comforts.Of(ComfortKind.SeatingMarquee).Roofed && !Comforts.Of(ComfortKind.Bench).Roofed);
        Assert.True(Comforts.Of(ComfortKind.HandWash).Washes && Comforts.Of(ComfortKind.HandGel).Washes && !Comforts.Of(ComfortKind.PicnicTable).Washes);
        Assert.Empty(Comforts.Of(ComfortKind.ShadeSail).Seats);
    }

    [Fact]
    public void A_comfort_is_paid_for_stands_on_free_grass_and_comes_back_for_its_money()
    {
        var game = new FestivalGame("Comforts", seed: 8);
        var f = game.Field.Field;
        var money = game.Money;
        Assert.True(game.BuildStall(StallKind.Burgers, f.X + 20, f.Y + 20, Facing.South).Ok);
        money -= Catalogue.Of(StallKind.Burgers).Cost;

        Assert.False(game.CheckComfort(ComfortKind.PicnicTable, f.X + 21, f.Y + 21, Facing.South).Ok);
        Assert.False(game.CheckComfort(ComfortKind.PicnicTable, f.X - 30, f.Y + 21, Facing.South).Ok);
        Assert.True(game.BuildComfort(ComfortKind.PicnicTable, f.X + 40, f.Y + 20, Facing.South).Ok);
        Assert.Equal(money - 25, game.Money);
        Assert.Equal(6, game.Seats);

        // Nothing else goes on top of it, and people walk round a table.
        Assert.False(game.CheckComfort(ComfortKind.Bench, f.X + 41, f.Y + 21, Facing.South).Ok);
        Assert.False(game.BuildStall(StallKind.Bar, f.X + 38, f.Y + 18, Facing.South).Ok);
        Assert.Contains(Enumerable.Range(0, 4).SelectMany(dx => Enumerable.Range(0, 4).Select(dy => new TilePoint(f.X + 40 + dx, f.Y + 20 + dy))), p => !game.Field.IsWalkable(p));

        // A sail is only a roof: people walk under it.
        Assert.True(game.BuildComfort(ComfortKind.ShadeSail, f.X + 60, f.Y + 20, Facing.South).Ok);
        Assert.True(game.Field.IsWalkable(new TilePoint(f.X + 64, f.Y + 24)));
        Assert.True(game.Site.RoofOver(new TilePoint(f.X + 64, f.Y + 24)));
        Assert.False(game.Site.RoofOver(new TilePoint(f.X + 41, f.Y + 21)));

        Assert.Equal(25, game.Remove(new TilePoint(f.X + 41, f.Y + 21)));
        Assert.Equal(money - 60, game.Money);
        Assert.Equal(0, game.Seats);
        Assert.True(game.Field.IsWalkable(new TilePoint(f.X + 41, f.Y + 21)));
    }

    [Fact]
    public void Comforts_and_people_sitting_on_them_survive_saving()
    {
        var game = Festival(WeatherKind.Fine, Tables);
        RunUntil(game, 13.25);
        Assert.True(game.Crowd.Seated > 0);
        var loaded = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));
        Assert.Equal(game.Site.Comforts.Select(c => (c.Kind, c.X, c.Y, c.Facing)), loaded.Site.Comforts.Select(c => (c.Kind, c.X, c.Y, c.Facing)));
        Assert.Equal(game.Seats, loaded.Seats);
        Assert.Equal(game.Crowd.Seated, loaded.Crowd.Seated);
        Assert.Equal(game.Crowd.SatDown, loaded.Crowd.SatDown);
        Assert.Equal(game.Crowd.WashedHands, loaded.Crowd.WashedHands);

        // An old save has no comforts in it, and loads.
        var old = SaveData.FromJson(game.Save().ToJson());
        Assert.Empty(FestivalGame.Load(old with { Site = old.Site! with { Comforts = null } }).Site.Comforts);
    }

    [Fact]
    public void People_sit_down_to_eat_and_no_seat_is_taken_twice()
    {
        var (game, most, overbooked) = Seated.Value;
        Assert.Equal(36, game.Seats);
        Assert.True(most >= 6, $"At the busiest only {most} were sitting.");
        Assert.False(overbooked);
        Assert.True(game.Crowd.SatDown >= 20, $"Only {game.Crowd.SatDown} sat down.");

        // People have a sit down a few times in a day, not every few minutes.
        Assert.True(game.Crowd.SatDown <= game.Crowd.Parties.Sum(p => p.Members.Count) * 4, $"{game.Crowd.SatDown} sittings is too many.");

        // Everyone sitting is on a seat, by a table or a bench.
        Assert.All(game.Crowd.Parties.Where(p => p.State == PartyState.Sitting), p =>
            Assert.Contains(game.Site.Comforts, c => Math.Abs(c.Middle.X - p.X) < 3 && Math.Abs(c.Middle.Y - p.Y) < 3));
    }

    [Fact]
    public void With_nothing_put_out_nobody_sits_and_a_seat_makes_a_day_no_worse()
    {
        Assert.Equal(0, Plain.Value.Crowd.SatDown);
        Assert.Equal(0, Plain.Value.Crowd.Seated);
        Assert.Equal(0, Plain.Value.Crowd.WashedHands);
        Assert.True(Seated.Value.Game.Crowd.Happiness >= Plain.Value.Crowd.Happiness - 1, $"{Seated.Value.Game.Crowd.Happiness} with seats, {Plain.Value.Crowd.Happiness} without.");
    }

    [Fact]
    public void In_the_rain_people_only_sit_under_a_roof()
    {
        var open = Festival(WeatherKind.Rain, Tables);
        var roofed = Festival(WeatherKind.Rain, (game, f) =>
        {
            Assert.True(game.BuildComfort(ComfortKind.SeatingMarquee, f.Right - 40, f.Y + 10, Facing.South).Ok);
            Assert.True(game.BuildComfort(ComfortKind.StrawBale, f.Right - 44, f.Y + 30, Facing.South).Ok);
        });
        var dry = 0;
        var wetBefore = false;
        var until = Day.AddHours(16);
        while (roofed.Now < until)
        {
            open.Clock.Step();
            roofed.Clock.Step();
            if (roofed.Clock.TickCount % 60 != 0)
            {
                continue;
            }

            // Give people a minute to notice it has started.
            var wet = open.Crowd.Rain > 0.05;
            if (wet && wetBefore)
            {
                Assert.True(open.Crowd.Seated == 0, $"Nobody should sit at an open table in the rain, but {open.Crowd.Seated} are at {open.Now:HH:mm}.");
            }

            wetBefore = wet;
            if (wet && roofed.Crowd.Rain > 0.05)
            {
                var sitting = roofed.Crowd.Parties.Where(p => p.State == PartyState.Sitting).ToList();
                Assert.All(sitting, p => Assert.True(roofed.Crowd.Sheltered(p)));
                dry = Math.Max(dry, sitting.Sum(p => p.Members.Count));
            }
        }

        Assert.True(dry >= 4, $"Only {dry} sat under the marquee in the rain.");
    }

    [Fact]
    public void Sinks_and_gel_by_the_toilets_and_food_are_used_and_count_as_hygiene()
    {
        var game = Festival(WeatherKind.Fine);
        var f = game.Field.Field;
        Assert.Equal(0, game.HygienePercent);

        // Three places need clean hands: the burger van and two lots of toilets.
        var x = f.Right - 22;
        Assert.True(game.BuildComfort(ComfortKind.HandGel, x, f.Y + 10, Facing.West).Ok);
        Assert.Equal((int)Math.Round(100 * Comforts.GelWorth / 3), game.HygienePercent);
        Assert.True(game.BuildComfort(ComfortKind.HandWash, x, f.Y + 44, Facing.West).Ok);
        Assert.Equal((int)Math.Round(100 * (Comforts.GelWorth + 2) / 3), game.HygienePercent);
        Assert.True(game.BuildComfort(ComfortKind.HandWash, x, f.Y + 12, Facing.West).Ok);
        Assert.Equal(100, game.HygienePercent);
        Assert.InRange(Comforts.CleanHandsSave, 0.1, 0.6);

        RunUntil(game, 15);
        Assert.True(game.Crowd.WashedHands >= 10, $"Only {game.Crowd.WashedHands} washed their hands.");
    }

    [Fact]
    public void The_phone_charging_tent_takes_money()
    {
        Assert.Equal(StallGroup.Other, Catalogue.GroupOf(StallKind.PhoneCharging));
        Assert.Equal(120, Catalogue.Of(StallKind.PhoneCharging).Cost);
        Assert.Equal(new[] { 2, 4, 6 }, Catalogue.MenuOf(StallKind.PhoneCharging).Select(m => m.UsualPrice));
        var game = Festival(WeatherKind.Fine, (g, f) => Assert.True(g.BuildStall(StallKind.PhoneCharging, f.X + 40, f.Y + 30, Facing.South).Ok));
        RunUntil(game, 17);
        var sales = game.SalesOf(game.Site.Stalls.Count - 1);
        Assert.True(sales.Items >= 5 && sales.Takings >= sales.Items * 2, $"The charging tent sold {sales.Items} for £{sales.Takings}.");

        // A party charges up once, not all day long.
        Assert.True(sales.Items <= game.Crowd.Parties.Count, $"{sales.Items} charges for {game.Crowd.Parties.Count} parties.");
    }

    [Fact]
    public void New_floors_have_prices_keep_the_mud_down_and_only_some_are_paths()
    {
        Assert.Equal(new[] { 3, 5, 6, 8, 9 }, new[] { GroundKind.Pallets, GroundKind.Sand, GroundKind.RubberMats, GroundKind.ArtificialGrass, GroundKind.Boardwalk }.Select(FestivalGame.CostOf));
        Assert.True(FestivalGame.IsPathLine(GroundKind.Boardwalk) && !FestivalGame.IsPathLine(GroundKind.Pallets));
        Assert.True(FestivalGame.IsPath(GroundKind.Pallets) && FestivalGame.IsPath(GroundKind.RubberMats) && !FestivalGame.IsPath(GroundKind.Sand));
        Assert.True(FestivalGame.IsLawn(GroundKind.Sand) && FestivalGame.IsLawn(GroundKind.ArtificialGrass) && !FestivalGame.IsLawn(GroundKind.RubberMats));

        // The muddiest patch of a wet day, then the same day with each floor laid on it.
        var bare = Festival(WeatherKind.Rain);
        RunUntil(bare, 17);
        var (column, row) = Enumerable.Range(0, bare.WearColumns).SelectMany(c => Enumerable.Range(0, bare.WearRows).Select(r => (c, r))).MaxBy(p => bare.MudAt(p.c, p.r));
        var mud = bare.MudAt(column, row);
        Assert.True(mud > 0.1, "A wet day should leave mud where people walk.");
        var kinds = new[] { GroundKind.Pallets, GroundKind.Sand, GroundKind.RubberMats, GroundKind.ArtificialGrass, GroundKind.Boardwalk };
        var game = bare;
        foreach (var kind in kinds)
        {
            game = Festival(WeatherKind.Rain);
            var money = game.Money;
            Assert.True(game.LayGround(kind, column, row).Ok);
            Assert.Equal(money - FestivalGame.CostOf(kind), game.Money);
            Assert.False(game.LayGround(kind, column, row).Ok);
            RunUntil(game, 17);
            Assert.True(game.MudAt(column, row) <= mud * 0.4, $"{kind} should keep most of the mud down: {game.MudAt(column, row):0.00} against {mud:0.00}.");
        }

        // The save keeps the floor.
        var loaded = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));
        Assert.Equal(GroundKind.Boardwalk, loaded.GroundAt(column, row));
    }

    [Fact]
    public void A_path_can_be_dragged_two_or_three_patches_wide()
    {
        var game = new FestivalGame("Paths", seed: 8);
        var money = game.Money;
        Assert.Equal(5, game.PathLine((4, 6), (8, 6)).Count());
        Assert.Equal(10, game.PathLine((4, 6), (8, 6), 2).Count());
        Assert.Equal(15, game.PathLine((4, 6), (8, 6), 3).Count());
        Assert.Equal(15, game.PathLine((4, 6), (8, 6), 9).Count());
        Assert.Equal(game.PathLine((4, 6), (8, 6), 3).Count(), game.PathLine((4, 6), (8, 6), 3).Distinct().Count());

        Assert.True(game.LayPath(GroundKind.Boardwalk, (4, 6), (8, 6), 2).Ok);
        Assert.Equal(10, game.PathPatches);
        Assert.Equal(money - 90, game.Money);
        Assert.True(game.PathAt(4, 6) && game.PathAt(4, 7) && !game.PathAt(4, 5));

        // Widening it pays only for the new patches.
        Assert.Equal(45, game.CheckPath(GroundKind.Boardwalk, (4, 6), (8, 6), 3).Cost);
    }

    [Fact]
    public void On_a_dry_day_people_sit_on_laid_grass_and_sand()
    {
        var game = Festival(WeatherKind.Fine, (g, f) =>
        {
            var at = g.PatchAt(new TilePoint(f.Right - 34, f.Y + 14))!.Value;
            for (var c = 0; c < 3; c++)
            {
                for (var r = 0; r < 3; r++)
                {
                    Assert.True(g.LayGround(c == 0 ? GroundKind.Sand : GroundKind.ArtificialGrass, at.Column - c, at.Row + r).Ok);
                }
            }
        });
        RunUntil(game, 15);
        Assert.Equal(0, game.Seats);
        Assert.True(game.Crowd.SatDown >= 5, $"Only {game.Crowd.SatDown} sat on the grass.");
    }
}
