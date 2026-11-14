using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

public class TravelTests
{
    private static FieldPlan Plan(params (string Field, FieldUse Use)[] choices) => new(choices.Select(c => new FieldChoice(c.Field, c.Use)));

    private static FestivalGame Year(int licence, FieldPlan plan, int bank = 40000) =>
        FestivalGame.StartYear("Larkmoor Festival", new YearSetup(4, licence, bank, false, 80)).WithFields(plan)!;

    [Fact]
    public void Only_people_with_a_way_to_get_there_buy_a_ticket()
    {
        // No car park and no buses: only the villagers can come.
        var game = Year(1500, Plan(("home", FieldUse.Music)));
        Assert.Equal(FestivalGame.Walkers, game.CanGetHere);
        Assert.Equal(FestivalGame.Walkers, game.OnSale);

        game.HireBuses(2);
        Assert.Equal(FestivalGame.Walkers + (2 * FestivalGame.BusSeats), game.OnSale);
        game.HireBuses(40);
        Assert.Equal(1500, game.OnSale);

        // A car park brings two and a half people a space.
        var parked = Year(1500, Plan(("home", FieldUse.Music), ("glebe", FieldUse.CarPark)));
        var spaces = Estate.Find("glebe")!.RoomFor(FieldUse.CarPark);
        Assert.Equal(FestivalGame.Walkers + (int)(spaces * 2.5), parked.CanGetHere);

        // A year on the old single field is not held back.
        Assert.Equal(1500, FestivalGame.StartYear("Larkmoor Festival", new YearSetup(4, 1500, 40000, false, 80)).OnSale);
    }

    [Fact]
    public void A_dear_car_park_sends_drivers_away()
    {
        var game = Year(3000, Plan(("home", FieldUse.Music), ("glebe", FieldUse.CarPark), ("laneend", FieldUse.CarPark)));
        game.SetParkingCharge(FestivalGame.FairParking);
        var fair = game.CanDrive;
        Assert.Equal(1, game.DriversWilling, 6);
        game.SetParkingCharge(0);
        Assert.Equal(fair, game.CanDrive);
        game.SetParkingCharge(10);
        Assert.InRange(game.CanDrive, fair * 0.65, fair * 0.75);
        game.SetParkingCharge(99);
        Assert.Equal(FestivalGame.DearestParking, game.ParkingCharge);
        Assert.Equal(0.2, game.DriversWilling, 6);
    }

    [Fact]
    public void The_crowd_is_split_into_walkers_drivers_and_bus_riders()
    {
        var game = Year(1500, Plan(("home", FieldUse.Music), ("ten", FieldUse.Food), ("glebe", FieldUse.CarPark)));
        game.HireBuses(6);
        Assert.True(game.ConfirmPlan());
        var people = game.Crowd.Expected;
        var split = game.TravelFor(people);
        Assert.Equal(people, split.OnFoot + split.ByCar + split.ByBus);
        Assert.Equal(FestivalGame.Walkers, split.OnFoot);
        Assert.True(split.ByCar > 300 && split.ByBus > 300, $"{split}");

        // The parties add up to nearly the same, a party at a time, and every car has a bay of its own and a few people in it.
        Assert.InRange(game.Crowd.ComingBy(Travel.Car), split.ByCar - 4, split.ByCar);
        Assert.InRange(game.Crowd.ComingBy(Travel.Bus), split.ByBus - 4, split.ByBus + 4);
        var drivers = game.Crowd.Parties.Where(p => p.Travel == Travel.Car).ToList();
        var cars = drivers.GroupBy(p => p.Bay).ToList();
        Assert.All(cars, c => Assert.InRange(c.Sum(p => p.Members.Count), 1, 6));
        var park = game.Field.Fields.Single(f => f.Use == FieldUse.CarPark).Grass;
        Assert.All(drivers, p => Assert.True(park.Contains(p.Bay!.Value)));
        Assert.True(cars.Count <= Estate.Find("glebe")!.RoomFor(FieldUse.CarPark));

        // Bus riders come when a bus pulls in, on the quarter hour.
        Assert.All(game.Crowd.Parties.Where(p => p.Travel == Travel.Bus), p => Assert.Equal(0, p.Arrives.Minute % 15));
        Assert.All(game.Crowd.Parties.Where(p => p.Travel == Travel.Bus), p => Assert.Equal(0, p.Arrives.Second));
    }

    [Fact]
    public void Car_parks_have_bays_in_rows_and_as_many_as_the_field_holds()
    {
        var map = FieldMap.ForPlan(Plan(("home", FieldUse.Music), ("glebe", FieldUse.CarPark), ("laneend", FieldUse.CarPark)), 1500);
        var bays = map.ParkingBays();
        Assert.Equal(Estate.Find("glebe")!.RoomFor(FieldUse.CarPark) + Estate.Find("laneend")!.RoomFor(FieldUse.CarPark), bays.Count);
        Assert.Equal(bays.Count, bays.Distinct().Count());
        Assert.All(bays, b => Assert.Equal(FieldUse.CarPark, map.UseAt(b)));
        Assert.NotNull(map.BusStop);
        Assert.Equal(Ground.Lane, map[map.BusStop!.Value]);
        Assert.Empty(FieldMap.Village().ParkingBays());
    }

    [Fact]
    public void A_day_with_cars_and_buses_takes_parking_money_and_everyone_gets_home()
    {
        var game = Year(1500, Plan(("home", FieldUse.Music), ("ten", FieldUse.Food), ("glebe", FieldUse.CarPark)));
        game.HireBuses(6);
        game.SetParkingCharge(4);
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
        var kinds = new[] { StallKind.FoodVan, StallKind.Bar, StallKind.ToiletBlock, StallKind.WaterPoint };
        var perSide = (food.Height - 20) / 30;
        for (var i = 0; i < 14; i++)
        {
            var east = i % 2 == 0;
            var column = i / 2 / perSide;
            Assert.True(game.BuildStall(kinds[i % kinds.Length], east ? food.Right - 16 - (column * 40) : food.X + 4 + (column * 40), food.Y + 10 + (i / 2 % perSide * 30), east ? Facing.West : Facing.East).Ok);
        }

        Assert.True(game.BuildStall(StallKind.FirstAidTent, music.X + 20, music.Y + 6, Facing.South).Ok);
        for (var i = 0; i < 5; i++)
        {
            Assert.True(game.HireStaff(StaffKind.FirstAider, new TilePoint(music.X + 30 + (i * 37), music.Y + 60)).Ok);
        }

        var bank = game.Money;
        game.OpenGates();
        Assert.Equal(bank - (6 * FestivalGame.BusCost), game.Money);
        Assert.Equal(6 * FestivalGame.BusCost, game.Spending.Single(l => l.What == "Shuttle buses").Amount);

        var guard = 0;
        var mostCars = 0;
        FestivalGame? loaded = null;
        while (game.Phase == Phase.Day)
        {
            game.Clock.Step();
            if (game.Clock.TickCount % 600 == 0)
            {
                mostCars = Math.Max(mostCars, game.Crowd.CarsParked.Count());
            }

            if (loaded is null && game.Now.Hour == 15)
            {
                loaded = FestivalGame.Load(game.Save());
                Assert.Equal(game.Crowd.CarsParked.OrderBy(b => b.X).ThenBy(b => b.Y), loaded.Crowd.CarsParked.OrderBy(b => b.X).ThenBy(b => b.Y));
                Assert.Equal(game.Crowd.ComingBy(Travel.Bus), loaded.Crowd.ComingBy(Travel.Bus));
                Assert.Equal(6, loaded.BusesHired);
                Assert.Equal(4, loaded.ParkingCharge);
            }

            Assert.True(++guard < 70000, "The day never ended.");
        }

        var drivers = game.Crowd.Parties.Where(p => p.Travel == Travel.Car).Select(p => p.Bay).Distinct().Count();
        Assert.True(game.Crowd.Arrived >= game.Crowd.Expected * 95 / 100, $"{game.Crowd.Arrived} of {game.Crowd.Expected} arrived.");
        Assert.True(mostCars >= drivers * 9 / 10, $"Most cars parked {mostCars} of {drivers}.");
        Assert.InRange(game.Income.Single(l => l.What == "Parking").Amount, drivers * 4 * 9 / 10, drivers * 4);
        Assert.True(game.Crowd.CameBy(Travel.Bus) > 300, $"{game.Crowd.CameBy(Travel.Bus)} came by bus.");
        Assert.Equal(0, game.Crowd.WalkedInFree);
        Assert.Empty(game.Crowd.CarsParked);
        Assert.Equal(0, game.Crowd.OnSite);
    }

    [Fact]
    public void Buses_and_the_parking_charge_carry_on_when_the_fields_change()
    {
        var game = Year(1500, Plan(("home", FieldUse.Music), ("glebe", FieldUse.CarPark)));
        game.HireBuses(5);
        game.SetParkingCharge(7);
        var moved = game.WithFields(Plan(("home", FieldUse.Music), ("ten", FieldUse.Food), ("glebe", FieldUse.CarPark)))!;
        Assert.Equal(5, moved.BusesHired);
        Assert.Equal(7, moved.ParkingCharge);
        Assert.Equal(moved.CanGetHere, FestivalGame.Walkers + moved.CanDrive + (5 * FestivalGame.BusSeats));
    }
}
