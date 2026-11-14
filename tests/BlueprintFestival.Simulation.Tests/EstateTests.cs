using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

public class EstateTests
{
    private static FieldPlan Plan(params (string Field, FieldUse Use)[] choices) => new(choices.Select(c => new FieldChoice(c.Field, c.Use)));

    [Fact]
    public void The_estate_has_nineteen_fields_that_do_not_overlap_and_each_has_its_own_room_and_rent()
    {
        Assert.Equal(19, Estate.All.Count);
        Assert.Equal(19, Estate.All.Select(f => f.Id).Distinct().Count());
        Assert.Equal(19, Estate.All.Select(f => f.Name).Distinct().Count());
        foreach (var a in Estate.All)
        {
            Assert.All(Estate.All.Where(b => b != a), b => Assert.True(a.Right <= b.X || b.Right <= a.X || a.Bottom <= b.Y || b.Bottom <= a.Y, $"{a.Name} overlaps {b.Name}."));
            Assert.True(a.RoomFor(FieldUse.Music) > a.RoomFor(FieldUse.Camping) && a.RoomFor(FieldUse.Camping) > a.RoomFor(FieldUse.CarPark));
        }

        var green = Estate.Find(Estate.VillageGreen)!;
        Assert.Equal(0, green.Rent);
        Assert.Equal((60, 40), (green.MetresWide, green.MetresDeep));
        Assert.Equal(600, green.RoomFor(FieldUse.Music));
        var show = Estate.Find("show")!;
        Assert.True(show.Rent > Estate.Find("home")!.Rent && Estate.Find("home")!.Rent > 0);
        Assert.True(show.RoomFor(FieldUse.Music) is > 3000 and < 5000);
    }

    [Fact]
    public void Fields_either_side_of_a_hedge_are_neighbours_but_not_across_the_lane_or_the_track()
    {
        EstateField F(string id) => Estate.Find(id)!;
        Assert.True(Estate.Neighbours(F("home"), F("ten")));
        Assert.True(Estate.Neighbours(F("show"), F("home")));
        Assert.True(Estate.Neighbours(F("far"), F("show")));
        Assert.True(Estate.Neighbours(F(Estate.VillageGreen), F("church")));
        Assert.False(Estate.Neighbours(F("ten"), F("long")));       // the farm track is between them
        Assert.False(Estate.Neighbours(F("home"), F("church")));    // the lane is between them
        Assert.False(Estate.Neighbours(F("hill"), F("sheep")));     // the copse is between them
        Assert.False(Estate.Neighbours(F("far"), F("home")));
        Assert.False(Estate.Neighbours(F("home"), F("home")));
    }

    [Fact]
    public void A_plan_says_in_plain_words_what_is_wrong_with_it()
    {
        Assert.Empty(FieldPlan.FirstYear().Problems(200, 1));
        Assert.Contains(new FieldPlan().Problems(200, 2), p => p.Contains("music field"));
        Assert.Contains(Plan((Estate.VillageGreen, FieldUse.Music), ("church", FieldUse.Food)).Problems(200, 1), p => p.Contains("first year"));
        Assert.Empty(Plan((Estate.VillageGreen, FieldUse.Music), ("church", FieldUse.Food)).Problems(400, 2));

        var apart = Plan(("home", FieldUse.Music), ("far", FieldUse.Food)).Problems(1500, 3);
        Assert.Contains(apart, p => p.Contains("Far Common") && p.Contains("hedge"));
        Assert.Empty(Plan(("home", FieldUse.Music), ("show", FieldUse.Music), ("far", FieldUse.Food)).Problems(3000, 3));

        Assert.Contains(Plan(("far", FieldUse.Music)).Problems(1500, 3), p => p.Contains("nobody can get in"));
        Assert.Contains(Plan(("home", FieldUse.Music), ("far", FieldUse.CarPark)).Problems(1500, 3), p => p.Contains("car park"));
        Assert.Empty(Plan(("home", FieldUse.Music), ("laneend", FieldUse.CarPark)).Problems(1500, 3));
        Assert.Contains(Plan(("home", FieldUse.Music), ("glebe", FieldUse.Camping)).Problems(3000, 3), p => p.Contains("5,000"));
        Assert.Empty(Plan(("home", FieldUse.Music), ("glebe", FieldUse.Camping)).Problems(5000, 3));
    }

    [Fact]
    public void A_plan_adds_up_its_rent_and_its_room_and_finds_the_way_in()
    {
        var plan = Plan(("show", FieldUse.Music), ("far", FieldUse.Music), ("home", FieldUse.Food), ("orchard", FieldUse.Camping), ("glebe", FieldUse.CarPark), ("laneend", FieldUse.CarPark));
        Assert.Equal(plan.Fields.Sum(f => f.Rent), plan.Rent);
        Assert.Equal(Estate.Find("show")!.RoomFor(FieldUse.Music) + Estate.Find("far")!.RoomFor(FieldUse.Music), plan.MusicRoom);
        Assert.True(plan.MusicRoom >= 5000);
        Assert.Equal(Estate.Find("orchard")!.RoomFor(FieldUse.Camping), plan.CampingRoom);
        Assert.Equal(Estate.Find("glebe")!.RoomFor(FieldUse.CarPark) + Estate.Find("laneend")!.RoomFor(FieldUse.CarPark), plan.ParkingSpaces);
        Assert.Equal("home", plan.WayIn!.Id);
        Assert.Equal("show", Plan(("show", FieldUse.Music)).WayIn!.Id);    // by the farm track

        var again = new FieldPlan(plan.Save());
        Assert.Equal(plan.Save(), again.Save());
        again.Set("far", null);
        Assert.Null(again.UseOf("far"));
        Assert.Equal(FieldUse.Music, plan.UseOf("far"));
    }

    [Fact]
    public void The_site_is_made_of_the_rented_fields_with_gateways_between_them()
    {
        var plan = Plan(("home", FieldUse.Music), ("ten", FieldUse.Food), ("show", FieldUse.Camping), ("glebe", FieldUse.CarPark));
        var map = FieldMap.ForPlan(plan, 5000);
        Assert.True(map.FromPlan);
        Assert.Equal(4, map.Fields.Count);
        var grass = map.Fields.ToDictionary(f => f.Info.Id, f => f.Grass);
        Assert.Equal((102 * 2, 72 * 2), (grass["home"].Width, grass["home"].Height));

        TilePoint Middle(string id) => new(grass[id].X + (grass[id].Width / 2), grass[id].Y + (grass[id].Height / 2));
        Assert.Equal(FieldUse.Music, map.UseAt(Middle("home")));
        Assert.Equal(FieldUse.Food, map.UseAt(Middle("ten")));
        Assert.Equal(FieldUse.Camping, map.UseAt(Middle("show")));
        Assert.Equal(FieldUse.CarPark, map.UseAt(Middle("glebe")));
        Assert.True(map.InFestival(Middle("home")) && map.InFestival(Middle("ten")));
        Assert.False(map.InFestival(Middle("show")) || map.InFestival(Middle("glebe")));
        Assert.True(map.Rented(Middle("show")) && map.Rented(Middle("glebe")));

        // The way in is through the food field, which is nearest the bus stop.
        Assert.True(grass["ten"].X <= map.Gap.X && map.Gap.Right <= grass["ten"].Right);
        Assert.Equal(Ground.Grass, map[map.Gap.X + 1, map.Gap.Y + 1]);
        Assert.Equal(Ground.Lane, map[map.Track.X + 1, map.Track.Y + 1]);

        // Every festival field can be walked to from the lane, through the gateways.
        var finder = new PathFinder(map);
        foreach (var id in new[] { "ten", "home", "show" })
        {
            Assert.NotNull(finder.FindPath(map.LaneStart, Middle(id)));
        }

        // The car park has a way out to the lane of its own, and a hedge between it and the music.
        Assert.NotNull(finder.FindPath(map.LaneStart, Middle("glebe")));
        Assert.Equal(Ground.Hedge, map[grass["glebe"].Right + 1, grass["glebe"].Y + 20]);
        Assert.True(map.HedgeRuns().Count is > 4 and < 80);
        Assert.Equal(Enumerable.Range(0, map.Width).Sum(x => Enumerable.Range(0, map.Height).Count(y => map[x, y] == Ground.Hedge)), map.HedgeRuns().Sum(r => r.Width * r.Height));

        // Tents in the camping field, clear of its gateway.
        var pitches = map.Pitches();
        Assert.True(pitches.Count > 300, $"{pitches.Count} pitches.");
        Assert.All(pitches, p => Assert.True(grass["show"].Contains(p)));
    }

    [Fact]
    public void The_first_year_is_the_village_green_and_the_fields_carry_on_to_the_next_year()
    {
        var first = FestivalGame.NewFestival("Larkmoor Festival").WithFields(FieldPlan.FirstYear())!;
        Assert.NotNull(first.Plan);
        Assert.Equal(0, first.FieldRent);
        Assert.Equal((120, 80), (first.Field.Field.Width, first.Field.Field.Height));
        Assert.Equal(200, first.OnSale);
        Assert.Null(first.WithFields(Plan((Estate.VillageGreen, FieldUse.Music), ("church", FieldUse.Food))));

        var later = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(3, 3000, 30000, false, 80));
        Assert.Null(later.Plan);
        var planned = later.WithFields(Plan(("home", FieldUse.Music), ("ten", FieldUse.Food)))!;
        Assert.Equal(3000, planned.Licence);
        Assert.Equal(30000, planned.Money);
        Assert.Equal(Estate.Find("home")!.RoomFor(FieldUse.Music), planned.OnSale);
        Assert.Equal(Estate.Find("home")!.Rent + Estate.Find("ten")!.Rent, planned.FieldRent);
        Assert.Equal(planned.ActFees + planned.FieldRent, planned.PlanCost);
        Assert.False(planned.CampingBooked);

        // A stage can only go in a music field; a stall can go in either.
        var grass = planned.Field.Fields.ToDictionary(f => f.Info.Id, f => f.Grass);
        Assert.False(planned.Site.CheckStage(StageKind.HiredStage, grass["ten"].X + 20, grass["ten"].Y + 20, Facing.South).Ok);
        Assert.Contains("music field", planned.Site.CheckStage(StageKind.HiredStage, grass["ten"].X + 20, grass["ten"].Y + 20, Facing.South).Problem);
        Assert.True(planned.Site.CheckStage(StageKind.HiredStage, grass["home"].X + 60, grass["home"].Y + 10, Facing.South).Ok);
        Assert.True(planned.Site.CheckStall(StallKind.FoodVan, grass["ten"].X + 20, grass["ten"].Y + 20, Facing.South).Ok);
        Assert.True(planned.Site.CheckStall(StallKind.FoodVan, grass["home"].X + 20, grass["home"].Y + 60, Facing.South).Ok);

        var loaded = FestivalGame.Load(planned.Save());
        Assert.Equal(planned.Plan!.Save(), loaded.Plan!.Save());
        Assert.Equal(planned.Field.Width, loaded.Field.Width);
    }

    [Fact]
    public void A_day_across_a_music_field_and_a_food_field_runs_from_gates_to_close()
    {
        var game = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(4, 1500, 40000, false, 80))
            .WithFields(Plan(("home", FieldUse.Music), ("ten", FieldUse.Food)))!;
        Assert.True(game.ConfirmPlan());
        var grass = game.Field.Fields.ToDictionary(f => f.Info.Id, f => f.Grass);
        var (music, food) = (grass["home"], grass["ten"]);

        var gap = game.Field.Gap;
        var y = gap.Y + 1;
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

        Assert.True(game.BuildStage(StageKind.MainStage, music.X + (music.Width / 2) - 20, music.Y + 6, Facing.South).Ok);
        var stage = game.Site.Stages[0].Rect;
        Assert.True(game.BuildFence(FenceKind.CrowdBarrier, new TilePoint(stage.X - 2, stage.Bottom + 4), new TilePoint(stage.Right + 1, stage.Bottom + 4)).Ok);
        Assert.True(game.HireStaff(StaffKind.Security, new TilePoint(stage.X + 6, stage.Bottom + 2)).Ok);
        Assert.True(game.HireStaff(StaffKind.Security, new TilePoint(stage.Right - 6, stage.Bottom + 2)).Ok);

        // Every stall is in the food field, down its west and east hedges.
        var kinds = new[] { StallKind.FoodVan, StallKind.Bar, StallKind.ToiletBlock, StallKind.WaterPoint };
        var perSide = (food.Height - 20) / 30;
        for (var i = 0; i < 14; i++)
        {
            var east = i % 2 == 0;
            var column = i / 2 / perSide;
            var x = east ? food.Right - 16 - (column * 40) : food.X + 4 + (column * 40);
            Assert.True(game.BuildStall(kinds[i % kinds.Length], x, food.Y + 10 + (i / 2 % perSide * 30), east ? Facing.West : Facing.East).Ok, $"Stall {i} would not fit.");
        }

        Assert.True(game.BuildStall(StallKind.FirstAidTent, music.X + 20, music.Y + 6, Facing.South).Ok);
        for (var i = 0; i < 5; i++)
        {
            Assert.True(game.HireStaff(StaffKind.FirstAider, new TilePoint(music.X + 30 + (i * 37), music.Y + 60)).Ok);
            Assert.True(game.HireStaff(StaffKind.Security, new TilePoint(music.X + 40 + (i * 31), music.Y + 90)).Ok);
        }

        game.OpenGates();
        var guard = 0;
        var mostWatching = 0;
        var mostInFood = 0;
        while (game.Phase == Phase.Day)
        {
            game.Clock.Step();
            if (game.Clock.TickCount % 300 == 0)
            {
                mostWatching = Math.Max(mostWatching, game.Crowd.Watching);
                mostInFood = Math.Max(mostInFood, game.Crowd.Parties.Where(p => p.IsOnSite && food.Contains(FieldMap.TileAt(p.X, p.Y))).Sum(p => p.Members.Count));
            }

            Assert.True(++guard < 70000, "The day never ended.");
        }

        Assert.True(game.Crowd.Arrived >= game.Crowd.Expected * 95 / 100, $"{game.Crowd.Arrived} of {game.Crowd.Expected} arrived.");
        Assert.Equal(0, game.Crowd.WalkedInFree);
        Assert.True(mostWatching > game.Crowd.Expected / 4, $"Most watching {mostWatching}.");
        Assert.True(mostInFood > 50, $"Most in the food field {mostInFood}.");
        Assert.True(game.TotalIncome > 5000, $"Took £{game.TotalIncome}.");
        Assert.True(game.Crowd.Happiness >= 50, $"Only {game.Crowd.Happiness}% happy.");
        Assert.False(game.SeriousIncident);
    }
}
