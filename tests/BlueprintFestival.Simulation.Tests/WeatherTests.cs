using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

public class WeatherTests
{
    private static readonly DayWeather Wet = DayWeather.Of(WeatherKind.Rain, 3);

    /// <summary>A whole festival day, run once and shared by the tests that only look at how it ended.</summary>
    private static readonly Lazy<FestivalGame> FineDay = new(() => Run(null, false, false));
    private static readonly Lazy<FestivalGame> RainDay = new(() => Run(Wet, false, false));
    private static readonly Lazy<FestivalGame> PonchoDay = new(() => Run(Wet, true, false));
    private static readonly Lazy<FestivalGame> CoveredDay = new(() => Run(Wet, false, true));

    /// <summary>
    /// A planned festival on its build day: a gate with ticket staff, a bandstand, food, drink and
    /// toilets, a first aid tent and two first-aiders, and the cheapest headliner.
    /// </summary>
    private static FestivalGame BuildDay(DayWeather? weather, bool ponchos = false)
    {
        var game = FestivalGame.NewFestival("Larkmoor Festival");
        if (weather is not null)
        {
            game.SetWeather(weather);
        }

        game.ChooseHeadliner(game.Headliners.ToList().FindIndex(h => h.Stars == game.Headliners.Min(o => o.Stars)));
        game.OrderPonchos(ponchos);
        Assert.True(game.ConfirmPlan());
        var gap = game.Field.Gap;
        var y = gap.Y + 1;
        Assert.True(game.BuildFence(FenceKind.Rope, new TilePoint(gap.X, y), new TilePoint(gap.Right - 1, y)).Ok);
        Assert.True(game.BuildGate(GateKind.VillageGate, gap.X, y, false).Ok);
        var f = game.Field.Field;
        Assert.True(game.BuildStage(StageKind.Bandstand, f.X + 50, f.Y + 6, Facing.South).Ok);
        Amenities.Add(game);
        Assert.True(game.HireStaff(StaffKind.TicketStaff, new TilePoint(gap.X + 1, y)).Ok);
        Assert.True(game.BuildStall(StallKind.FirstAidTent, f.X + 20, f.Y + 6, Facing.South).Ok);
        Assert.True(game.HireStaff(StaffKind.FirstAider, new TilePoint(f.X + 30, f.Y + 30)).Ok);
        Assert.True(game.HireStaff(StaffKind.FirstAider, new TilePoint(f.X + 70, f.Y + 30)).Ok);
        return game;
    }

    /// <summary>The patches of ground in front of the stage, where the audience stands.</summary>
    private static IEnumerable<(int Column, int Row)> InFrontOfStage(FestivalGame game)
    {
        var rect = game.Site.Stages[0].Rect;
        var (left, top) = game.PatchAt(new TilePoint(rect.X, rect.Bottom + 1))!.Value;
        var (right, _) = game.PatchAt(new TilePoint(rect.Right - 1, rect.Bottom + 1))!.Value;
        for (var row = top; row < top + 4; row++)
        {
            for (var column = left - 1; column <= right + 1; column++)
            {
                yield return (column, row);
            }
        }
    }

    private static FestivalGame Run(DayWeather? weather, bool ponchos, bool cover)
    {
        var game = BuildDay(weather, ponchos);
        if (cover)
        {
            var patches = InFrontOfStage(game).ToList();
            foreach (var (column, row) in patches)
            {
                // Trackway along the front row, straw behind it.
                Assert.True(game.LayGround(row == patches[0].Row ? GroundKind.Trackway : GroundKind.Straw, column, row).Ok);
            }
        }

        game.OpenGates();
        var guard = 0;
        while (game.Phase == Phase.Day)
        {
            game.Clock.Step();
            Assert.True(++guard < 70000, "The day never ended.");
        }

        return game;
    }

    [Fact]
    public void A_day_of_weather_is_the_same_every_time_and_says_when_it_rains()
    {
        var rain = DayWeather.Of(WeatherKind.Rain, 3);
        Assert.Equal(rain.Rain, DayWeather.Of(WeatherKind.Rain, 3).Rain);
        Assert.True(rain.Wet);
        var spell = Assert.Single(rain.Spells);
        Assert.InRange(spell.To - spell.From, 6, 9);
        Assert.StartsWith("Rain from ", rain.Summary);
        Assert.Contains("°C", rain.Summary);

        var showers = DayWeather.Of(WeatherKind.Showers, 3);
        Assert.Equal(2, showers.Spells.Count);
        Assert.StartsWith("Showers around ", showers.Summary);

        var fine = DayWeather.Of(WeatherKind.Fine, 3);
        Assert.False(fine.Wet);
        Assert.Empty(fine.Spells);
        Assert.All(DayWeather.Of(WeatherKind.Hot, 3).Rain, r => Assert.Equal(0, r));
        Assert.True(DayWeather.Of(WeatherKind.Hot, 3).High > fine.High);
    }

    [Fact]
    public void The_first_year_is_always_fine_and_later_years_get_every_kind_of_day()
    {
        var kinds = new HashSet<WeatherKind>();
        for (var seed = 0; seed < 60; seed++)
        {
            Assert.Equal(WeatherKind.Fine, DayWeather.ForYear(seed, 1).Kind);
            kinds.Add(DayWeather.ForYear(seed, 2).Kind);
        }

        Assert.Equal(4, kinds.Count);
        Assert.Equal(WeatherKind.Fine, FestivalGame.NewFestival("Larkmoor Festival").Weather.Kind);
        Assert.Equal(10, FestivalGame.NewFestival("Larkmoor Festival").RainChance);
    }

    [Fact]
    public void A_wet_forecast_puts_some_people_off_coming()
    {
        var game = FestivalGame.NewFestival("Larkmoor Festival");
        var fine = game.ExpectedAt(5);
        game.SetWeather(Wet);
        Assert.InRange(game.RainChance, 45, 85);
        Assert.Contains($"{game.RainChance}% chance of rain", game.EarlyForecast);
        Assert.InRange(game.ExpectedAt(5), (int)(fine * 0.78), fine - 5);
        Assert.Equal(game.ExpectedAt(game.TicketPrice), game.Crowd.Expected);
    }

    [Fact]
    public void People_dress_for_rain_and_free_ponchos_cover_everyone()
    {
        static List<PersonLook> Looks(FestivalGame game) => game.Crowd.Parties.SelectMany(p => p.Members).Select(m => m.Look).ToList();

        var dry = Looks(BuildDay(null));
        Assert.DoesNotContain(dry, l => l.Poncho is not null);

        var wet = Looks(BuildDay(Wet));
        Assert.InRange(wet.Count(l => l.Poncho is not null), wet.Count * 4 / 10, wet.Count * 7 / 10);
        Assert.True(wet.Count(l => l.Feet == Footwear.Wellies) > dry.Count(l => l.Feet == Footwear.Wellies) * 2);

        var handedOut = BuildDay(Wet, ponchos: true);
        Assert.All(Looks(handedOut), l => Assert.NotNull(l.Poncho));
        // They are ordered for the crowd expected, before anyone knows quite how many will come.
        var paid = handedOut.Spending.Single(l => l.What == "Rain ponchos").Amount;
        Assert.InRange(paid, handedOut.Crowd.Expected * 9 / 10, handedOut.Crowd.Expected * 11 / 10);

        // Ponchos ordered for a day that turns out dry are money wasted: nobody wears one.
        var wasted = BuildDay(null, ponchos: true);
        Assert.DoesNotContain(Looks(wasted), l => l.Poncho is not null);
        Assert.Contains(wasted.Spending, l => l.What == "Rain ponchos");
    }

    [Fact]
    public void Rain_wets_the_ground_and_feet_turn_it_to_mud()
    {
        var fine = FineDay.Value;
        var rain = RainDay.Value;
        Assert.Equal(0, fine.Wetness);
        Assert.Equal(0, fine.MuddyPatches);
        Assert.True(rain.Wetness > 0.5, $"Wetness {rain.Wetness}");
        Assert.True(rain.MuddyPatches >= 8, $"Muddy patches {rain.MuddyPatches}");
        Assert.True(rain.GrassWorn > fine.GrassWorn, $"Worn {rain.GrassWorn} against {fine.GrassWorn}");
        Assert.True(rain.FieldRepair > fine.FieldRepair, $"Repair {rain.FieldRepair} against {fine.FieldRepair}");
        Assert.Contains(InFrontOfStage(rain), p => rain.MudAt(p.Column, p.Row) >= FestivalGame.Slippery);
    }

    [Fact]
    public void A_wet_day_is_a_less_happy_one_and_ponchos_take_the_edge_off()
    {
        var fine = FineDay.Value.Crowd.Happiness;
        var rain = RainDay.Value.Crowd.Happiness;
        var ponchos = PonchoDay.Value.Crowd.Happiness;
        Assert.True(rain < fine - 3, $"Rain {rain}, fine {fine}");
        Assert.True(ponchos > rain + 2, $"Ponchos {ponchos}, rain {rain}");
        Assert.True(RainDay.Value.Crowd.WentHomeUnhappy >= FineDay.Value.Crowd.WentHomeUnhappy);
    }

    [Fact]
    public void Trackway_saves_the_grass_and_straw_soaks_up_most_of_the_mud()
    {
        var bare = RainDay.Value;
        var covered = CoveredDay.Value;
        var patches = InFrontOfStage(covered).ToList();
        foreach (var (column, row) in patches)
        {
            if (covered.GroundAt(column, row) == GroundKind.Trackway)
            {
                Assert.Equal(0, covered.MudAt(column, row));
                Assert.Equal(0, covered.WearAt(column, row));
            }
            else
            {
                Assert.Equal(GroundKind.Straw, covered.GroundAt(column, row));
                Assert.True(covered.MudAt(column, row) < FestivalGame.Slippery);
                Assert.True(covered.WearAt(column, row) <= bare.WearAt(column, row));
            }
        }

        Assert.True(covered.MuddyPatches < bare.MuddyPatches, $"{covered.MuddyPatches} against {bare.MuddyPatches}");
        Assert.True(covered.FieldRepair < bare.FieldRepair, $"{covered.FieldRepair} against {bare.FieldRepair}");
        Assert.Equal(patches.Count, covered.GroundCovered);
    }

    [Fact]
    public void Ground_cover_costs_money_and_comes_back_only_on_the_build_day()
    {
        var game = BuildDay(null);
        var before = game.Money;
        Assert.True(game.LayGround(GroundKind.Straw, 3, 3).Ok);
        Assert.True(game.LayGround(GroundKind.Trackway, 4, 3).Ok);
        Assert.Equal(before - FestivalGame.CostOf(GroundKind.Straw) - FestivalGame.CostOf(GroundKind.Trackway), game.Money);
        Assert.False(game.LayGround(GroundKind.Straw, 3, 3).Ok);
        Assert.False(game.CheckGround(GroundKind.Straw, -1, 0).Ok);
        Assert.False(game.CheckGround(GroundKind.Straw, game.WearColumns, 0).Ok);

        // Taking down with the ordinary remove tool lifts the cover when nothing stands on it.
        var f = game.Field.Field;
        Assert.Equal(FestivalGame.CostOf(GroundKind.Straw), game.Remove(new TilePoint(f.X + (3 * FestivalGame.WearTiles) + 1, f.Y + (3 * FestivalGame.WearTiles) + 1)));
        Assert.Null(game.GroundAt(3, 3));

        game.OpenGates();
        Assert.True(game.LayGround(GroundKind.Straw, 3, 3).Ok);
        Assert.Equal(0, game.LiftGround(3, 3));
        Assert.Equal(0, game.LiftGround(4, 3));
        Assert.Equal(0, game.GroundCovered);
    }

    [Fact]
    public void A_hot_day_makes_people_thirsty_faster()
    {
        var game = BuildDay(DayWeather.Of(WeatherKind.Hot, 3));
        Assert.Equal(1.6, game.Crowd.Heat);
        Assert.False(game.Crowd.DressedForRain);
        Assert.Equal(1.0, BuildDay(null).Crowd.Heat);
    }

    [Fact]
    public void The_farmer_charges_for_worn_out_grass_when_the_field_is_handed_back()
    {
        var game = Run(Wet, false, false);
        var owed = game.FieldRepair;
        Assert.True(owed > 0);
        var before = game.Money;
        var result = game.HandBack();
        Assert.Equal(owed, result.FieldRepair);
        Assert.Equal(owed, game.Spending.Single(l => l.What == "Field repairs").Amount);
        Assert.True(game.Money == before - owed || result.Loan);
        Assert.StartsWith("Rain from ", result.Weather);
    }

    [Fact]
    public void A_wet_day_saved_in_the_rain_comes_back_wet()
    {
        var game = BuildDay(Wet, ponchos: true);
        Assert.True(game.LayGround(GroundKind.Trackway, 5, 2).Ok);
        game.OpenGates();
        while (game.Now.Hour < 16)
        {
            game.Clock.Step();
        }

        Assert.True(game.RainNow > 0.5);
        Assert.True(game.Wetness > 0.2);

        var loaded = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));
        Assert.Equal(WeatherKind.Rain, loaded.Weather.Kind);
        Assert.Equal(game.Weather.Rain, loaded.Weather.Rain);
        Assert.Equal(game.Wetness, loaded.Wetness);
        Assert.Equal(game.RainChance, loaded.RainChance);
        Assert.True(loaded.PonchosOrdered);
        Assert.Equal(GroundKind.Trackway, loaded.GroundAt(5, 2));
        Assert.Equal(game.Crowd.Rain, loaded.Crowd.Rain);
        Assert.Equal(game.Crowd.People.Select(p => p.Look.Poncho), loaded.Crowd.People.Select(p => p.Look.Poncho));

        // Both carry on exactly alike.
        for (var i = 0; i < 1800; i++)
        {
            game.Clock.Step();
            loaded.Clock.Step();
        }

        Assert.Equal(game.Wetness, loaded.Wetness);
        Assert.Equal(game.GrassWorn, loaded.GrassWorn);
        Assert.Equal(game.Crowd.Happiness, loaded.Crowd.Happiness);
    }
}
