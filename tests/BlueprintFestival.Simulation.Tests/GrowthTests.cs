using System.Diagnostics;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

public class GrowthTests
{
    /// <summary>
    /// A festival in a later year with money in the bank, built for its licence: turnstile rows
    /// across the way in with staff on every lane, a main stage with a front barrier and pit
    /// crew, and food, drink, toilets and first aid in proportion to the crowd.
    /// </summary>
    private static FestivalGame Built(int licence, int bank = 60000, bool bigName = false)
    {
        var game = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(4, licence, bank, false, 80));
        var stars = bigName ? game.Headliners.Max(o => o.Stars) : game.Headliners.Min(o => o.Stars);
        game.ChooseHeadliner(game.Headliners.ToList().FindIndex(h => h.Stars == stars));
        Assert.True(game.ConfirmPlan());
        var gap = game.Field.Gap;
        var y = gap.Y + 1;
        var f = game.Field.Field;
        Assert.True(game.BuildFence(FenceKind.SteelMesh, new TilePoint(gap.X, y), new TilePoint(gap.Right - 1, y)).Ok);
        var row = Catalogue.Of(GateKind.TurnstileRow);
        for (var x = gap.X; x + row.Width <= gap.Right; x += row.Width)
        {
            Assert.True(game.BuildGate(GateKind.TurnstileRow, x, y, false).Ok);
            for (var lane = 0; lane < row.Lanes; lane++)
            {
                Assert.True(game.HireStaff(StaffKind.TicketStaff, new TilePoint(x + (lane * row.Width / row.Lanes) + 1, y)).Ok);
            }
        }

        Assert.True(game.BuildStage(StageKind.MainStage, f.X + (f.Width / 2) - 20, f.Y + 6, Facing.South).Ok);
        var stage = game.Site.Stages[0].Rect;
        Assert.True(game.BuildFence(FenceKind.CrowdBarrier, new TilePoint(stage.X - 2, stage.Bottom + 4), new TilePoint(stage.Right + 1, stage.Bottom + 4)).Ok);

        // Each of the pit crew holds another eight metres of crowd, so a bigger crowd wants more of them.
        var crew = Math.Clamp(licence / 1200, 2, 4);
        for (var i = 0; i < crew; i++)
        {
            Assert.True(game.HireStaff(StaffKind.Security, new TilePoint(stage.X + 4 + (i * 32 / (crew - 1)), stage.Bottom + 2)).Ok);
        }

        // And more out in the field, so trouble never has to wait for the pit crew.
        for (var i = 0; i < licence / 600; i++)
        {
            Assert.True(game.HireStaff(StaffKind.Security, new TilePoint(f.X + 50 + (i * 61 % (f.Width - 100)), f.Y + 70 + (i * 47 % (f.Height - 110)))).Ok);
        }

        // A row of stalls down the east hedge and another down the west, facing in.
        var kinds = new[] { StallKind.FoodVan, StallKind.Bar, StallKind.ToiletBlock, StallKind.WaterPoint };
        var wanted = Math.Max(8, licence / 110);
        var perSide = (f.Height - 50) / 30;
        for (var i = 0; i < wanted; i++)
        {
            var east = i % 2 == 0;
            var column = i / 2 / perSide;
            var along = i / 2 % perSide;
            var x = east ? f.Right - 16 - (column * 40) : f.X + 4 + (column * 40);
            Assert.True(game.BuildStall(kinds[i % kinds.Length], x, f.Y + 44 + (along * 30), east ? Facing.West : Facing.East).Ok, $"Stall {i} would not fit.");
        }

        Assert.True(game.BuildStall(StallKind.FirstAidTent, f.X + 20, f.Y + 6, Facing.South).Ok);
        Assert.True(game.BuildStall(StallKind.FirstAidTent, f.X + 40, f.Y + 6, Facing.South).Ok);
        for (var i = 0; i < Math.Max(2, licence / 300); i++)
        {
            Assert.True(game.HireStaff(StaffKind.FirstAider, new TilePoint(f.X + 30 + (i * 37 % (f.Width - 60)), f.Y + 40 + (i * 53 % (f.Height - 80)))).Ok);
        }

        return game;
    }

    [Fact]
    public void A_good_year_climbs_one_step_of_the_licence_ladder()
    {
        Assert.Equal(new[] { 200, 400, 800, 1500, 3000, 5000 }, FestivalGame.Licences);
        Assert.Equal(400, FestivalGame.LicenceAfter(200));
        Assert.Equal(800, FestivalGame.LicenceAfter(400));
        Assert.Equal(5000, FestivalGame.LicenceAfter(3000));
        Assert.Equal(5000, FestivalGame.LicenceAfter(5000));
        Assert.Equal(FestivalGame.BiggestLicence, FestivalGame.Licences[^1]);
    }

    [Fact]
    public void A_bigger_licence_moves_the_festival_to_a_bigger_field_with_a_wider_way_in()
    {
        var village = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(2, 400, 5000));
        var meadow = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(3, 800, 20000));
        var showground = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(6, 5000, 100000));

        Assert.Equal("the village field", village.Ground.Name);
        Assert.Equal("Home Farm meadow", meadow.Ground.Name);
        Assert.Equal("the showground", showground.Ground.Name);
        Assert.Equal(FieldMap.Village().Width, village.Field.Width);
        Assert.Equal(110 * 2, meadow.Field.Field.Width);
        Assert.Equal(70 * 2, meadow.Field.Field.Height);
        Assert.Equal(180 * 2, showground.Field.Field.Width);
        Assert.True(meadow.Field.Gap.Width > village.Field.Gap.Width);
        Assert.True(showground.Field.Gap.Width > meadow.Field.Gap.Width);

        // Every field has at least four square metres of grass for each person its biggest licence allows.
        foreach (var size in FieldMap.Sizes)
        {
            Assert.True(size.MetresWide * size.MetresDeep >= size.Holds * 4, size.Name);
        }

        // The wear and crowd maps cover the whole of the bigger field.
        Assert.Equal(showground.Field.Field.Width / FestivalGame.WearTiles, showground.WearColumns);
        Assert.True(showground.Density.Across * 4 >= showground.Field.Width);
    }

    [Fact]
    public void A_bigger_field_is_rented_and_the_village_field_is_free()
    {
        var village = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(2, 400, 5000));
        Assert.Equal(0, village.FieldRent);
        Assert.True(village.ConfirmPlan());
        Assert.DoesNotContain(village.Spending, l => l.What == "Field rent and licence");

        var meadow = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(3, 800, 20000));
        Assert.Equal(800, meadow.FieldRent);
        Assert.True(meadow.ConfirmPlan());
        Assert.Equal(800, meadow.Spending.Single(l => l.What == "Field rent and licence").Amount);
        Assert.Equal(20000 - meadow.ActFees - 800, meadow.Money);

        // The bank has to cover the rent as well as the acts.
        var poor = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(3, 800, 0));
        poor = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(3, 800, poor.ActFees + 100));
        Assert.False(poor.ConfirmPlan());
        Assert.True(FestivalGame.NeedsFor(800) > FestivalGame.NeedsFor(400) * 2);
    }

    [Fact]
    public void There_is_bigger_kit_for_bigger_festivals_and_it_is_all_on_offer_from_the_start()
    {
        var main = Catalogue.Of(StageKind.MainStage);
        var hired = Catalogue.Of(StageKind.HiredStage);
        Assert.True(main.Width > hired.Width && main.Cost > hired.Cost && main.Performers >= hired.Performers);

        var row = Catalogue.Of(GateKind.TurnstileRow);
        var arch = Catalogue.Of(GateKind.ArchGate);
        Assert.True(row.Lanes * 60.0 / row.SecondsPerPerson > 3 * arch.Lanes * 60.0 / arch.SecondsPerPerson);

        var block = Catalogue.Of(StallKind.ToiletBlock);
        Assert.Equal(Need.Toilet, block.Serves);
        Assert.Equal(3 * Catalogue.Of(StallKind.Toilets).Servers, block.Servers);

        // A first-year village festival can put any of them up, if it has the money.
        var game = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(1, 200, 20000));
        Assert.True(game.ConfirmPlan());
        var f = game.Field.Field;
        Assert.True(game.BuildStage(StageKind.MainStage, f.X + 40, f.Y + 6, Facing.South).Ok);
        Assert.True(game.BuildStall(StallKind.ToiletBlock, f.X + 4, f.Y + 40, Facing.South).Ok);
        Assert.Equal(6, game.Show.Slots.Max(s => s.Act.Members.Count) <= 6 ? 6 : 0);
    }

    [Fact]
    public void A_big_crowd_has_more_go_wrong_but_not_in_step_with_its_size()
    {
        Assert.Equal(150, FestivalGame.Crowded(150));
        Assert.Equal(400, FestivalGame.Crowded(400));
        Assert.InRange(FestivalGame.Crowded(5000), 1500, 2200);
        Assert.True(FestivalGame.Crowded(5000) > FestivalGame.Crowded(3000));
    }

    [Fact]
    public void A_festival_of_fifteen_hundred_runs_a_whole_day_and_can_grow()
    {
        var game = Built(1500);
        Assert.InRange(game.Crowd.Expected, 1000, 1500);
        var spent = game.TotalSpending;
        game.OpenGates();
        var watch = Stopwatch.StartNew();
        var guard = 0;
        var mostOnSite = 0;
        var redOnTheLane = 0;
        while (game.Phase == Phase.Day)
        {
            game.Clock.Step();
            mostOnSite = Math.Max(mostOnSite, game.Crowd.OnSite);
            if (game.Clock.TickCount % 600 == 0)
            {
                // The lane outside is never measured as a crush, however many are walking home along it.
                var lane = game.Field.Lane;
                for (var x = lane.X; x < lane.Right; x += 4)
                {
                    redOnTheLane += game.Density.StatusAt(x * FieldMap.TileMetres, (lane.Y + 5) * FieldMap.TileMetres) == Crowding.CrushRisk ? 1 : 0;
                }
            }
            Assert.True(++guard < 70000, "The day never ended.");
        }

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(60), $"A day of 1,500 took {watch.Elapsed.TotalSeconds:0} seconds.");
        Assert.True(game.Crowd.Arrived >= game.Crowd.Expected * 95 / 100, $"{game.Crowd.Arrived} of {game.Crowd.Expected} arrived.");
        Assert.True(mostOnSite >= game.Crowd.Expected * 8 / 10, $"Most on site {mostOnSite}.");
        Assert.Equal(0, game.Crowd.WalkedInFree);
        Assert.False(game.SeriousIncident);
        Assert.Equal(0, game.CrushInjuries);
        Assert.Equal(0, redOnTheLane);

        // Stalls are a long walk away on a big field, but hardly anyone gives up once they are in the queue.
        Assert.True(game.Crowd.GaveUpQueuing < game.Crowd.Expected / 4, $"{game.Crowd.GaveUpQueuing} gave up queuing.");
        Assert.True(game.Crowd.Happiness >= 60, $"Only {game.Crowd.Happiness}% happy.");
        Assert.True(game.TotalIncome > 8000, $"Took £{game.TotalIncome}.");
        Assert.True(game.Treated + game.Untreated is > 5 and < 120, $"{game.Treated} treated, {game.Untreated} not.");

        Assert.True(game.PayScouts());
        var result = game.HandBack();
        Assert.Equal(1500, result.LicenceNow);
        Assert.True(result.LicenceNext is 1500 or 3000);
        if (result.LicenceNext == 3000)
        {
            Assert.Contains("the showground", result.Verdict);
            Assert.Equal("the showground", game.NextYear().Ground.Name);
        }
    }

    [Fact]
    public void Each_of_the_pit_crew_holds_another_stretch_of_a_big_crowd()
    {
        var game = Built(1500);
        var stage = game.Site.Stages[0];
        var (fx, fy, _, _) = Show.RunningOrder.Front(stage);
        game.OpenGates();
        while (game.Now.TimeOfDay < TimeSpan.FromHours(9))
        {
            game.Clock.Step();
        }

        // Two behind the barrier: sixteen metres and a bit of crowd in hand, and no more.
        Assert.Equal(2, game.PitCrew(stage));
        Assert.True(game.Stewarded(fx, fy + 6));
        Assert.True(game.Stewarded(fx, fy + 17));
        Assert.False(game.Stewarded(fx, fy + 24));

        // A third looks after the next eight metres.
        Assert.True(game.HireStaff(StaffKind.Security, new TilePoint(stage.Rect.X + 20, stage.Rect.Bottom + 2)).Ok);
        while (game.PitCrew(stage) < 3 && game.Now.TimeOfDay < TimeSpan.FromHours(10))
        {
            game.Clock.Step();
        }

        Assert.Equal(3, game.PitCrew(stage));
        Assert.True(game.Stewarded(fx, fy + 24));
        Assert.False(game.Stewarded(fx, fy + 40));
    }

    [Fact]
    public void A_big_name_on_the_main_stage_is_safe_with_a_barrier_and_pit_crew_and_they_stay_at_their_post()
    {
        var game = Built(1500, bigName: true);
        var stage = game.Site.Stages[0];
        game.OpenGates();
        var fewestInThePit = int.MaxValue;
        var guard = 0;
        while (game.Phase == Phase.Day)
        {
            game.Clock.Step();
            if (game.Show.Playing(game.Now) is not null && game.Now.TimeOfDay > TimeSpan.FromHours(12.2))
            {
                fewestInThePit = Math.Min(fewestInThePit, game.PitCrew(stage));
            }

            Assert.True(++guard < 70000, "The day never ended.");
        }

        // Trouble in the field goes to the security out there, never to the two at the front.
        Assert.True(game.TroubleSorted > 0);
        Assert.Equal(2, fewestInThePit);
        Assert.False(game.SeriousIncident);
        Assert.Equal(0, game.CrushInjuries);
        Assert.True(game.Crowd.Happiness >= 70, $"Only {game.Crowd.Happiness}% happy.");
    }

    [Fact]
    public void Five_thousand_people_get_through_the_turnstiles_and_the_game_keeps_up()
    {
        var game = Built(5000, 200000);
        Assert.InRange(game.Crowd.Expected, 3500, 5000);
        game.OpenGates();
        var watch = Stopwatch.StartNew();
        while (game.Now.TimeOfDay < TimeSpan.FromHours(11.5))
        {
            game.Clock.Step();
        }

        // Three and a half hours of the busiest part of the day: well under a hundredth of a second a tick.
        var perTick = watch.Elapsed.TotalMilliseconds / game.Clock.TickCount;
        Assert.True(perTick < 8, $"{perTick:0.0} ms a tick.");
        Assert.True(game.Crowd.OnSite > 2500, $"Only {game.Crowd.OnSite} on site by half past eleven.");
        Assert.True(game.Crowd.Queuing < 600, $"{game.Crowd.Queuing} still queuing at half past eleven.");
        Assert.Equal(0, game.Crowd.WalkedInFree);

        // A save of a big festival comes back on the same big field.
        var loaded = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));
        Assert.Equal(game.Field.Width, loaded.Field.Width);
        Assert.Equal(game.Crowd.OnSite, loaded.Crowd.OnSite);
        Assert.Equal(game.Site.Stalls.Count, loaded.Site.Stalls.Count);
    }
}
