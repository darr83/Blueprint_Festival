using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

public class CampingTests
{
    /// <summary>A well built festival of 5,000 with the camping field taken, played to the morning after.</summary>
    private static readonly Lazy<FestivalGame> Camped = new(Night);

    private static FestivalGame Night()
    {
        var game = GrowthTests.Built(5000, 90000, camping: true);
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
    public void Camping_is_only_on_offer_at_five_thousand()
    {
        var meadow = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(5, 3000, 50000));
        var showground = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(6, 5000, 50000));

        Assert.False(FieldMap.Village().HasCamp);
        Assert.False(meadow.CampingOffered);
        meadow.BookCamping(true);
        Assert.False(meadow.CampingBooked);

        Assert.True(showground.CampingOffered);
        Assert.False(showground.CampingBooked);
        showground.BookCamping(true);
        Assert.True(showground.CampingBooked);
        showground.BookCamping(false);
        Assert.False(showground.CampingBooked);
    }

    [Fact]
    public void The_camping_field_is_through_the_east_hedge_and_can_be_walked_to()
    {
        var map = FieldMap.ForLicence(5000);

        Assert.True(map.HasCamp);
        Assert.Equal(map.Field.Height, map.Camp.Height);
        Assert.True(map.Camp.X > map.Field.Right);
        Assert.Equal(Ground.Grass, map[map.CampGap.X, map.CampGap.Y]);
        Assert.Equal(Ground.Hedge, map[map.CampGap.X, map.CampGap.Y - 1]);
        Assert.Equal(Ground.Hedge, map[map.Camp.Right, map.Camp.Y + 10]);
        Assert.Equal(Ground.Hedge, map[map.Camp.X + 10, map.Camp.Y - 1]);

        var pitches = map.Pitches();
        Assert.True(pitches.Count >= 700, $"Only {pitches.Count} pitches.");
        Assert.Equal(pitches.Count, pitches.Distinct().Count());
        Assert.All(pitches, p => Assert.True(map.Camp.Contains(p)));
        var from = new TilePoint(map.Field.X + 20, map.Field.Y + 20);
        Assert.NotNull(new PathFinder(map).FindPath(from, pitches[^1]));
    }

    [Fact]
    public void Taking_the_camping_field_costs_money_and_gives_a_third_of_the_crowd_a_pitch()
    {
        var without = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(6, 5000, 90000));
        var with = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(6, 5000, 90000));
        with.BookCamping(true);
        Assert.True(without.ConfirmPlan());
        Assert.True(with.ConfirmPlan());

        Assert.Equal(without.Money - FestivalGame.CampingCost, with.Money);
        Assert.Contains(with.Spending, l => l.What == "Camping field" && l.Amount == FestivalGame.CampingCost);
        Assert.Equal(0, without.Crowd.Campers);
        Assert.InRange(with.Crowd.Campers, with.Crowd.Expected * 0.28, with.Crowd.Expected * 0.42);
        Assert.InRange(with.CampersExpected, with.Crowd.Campers * 0.8, with.Crowd.Campers * 1.25);
        Assert.Empty(with.Crowd.Tents);
    }

    [Fact]
    public void Campers_pay_for_a_pitch_and_go_to_their_tents_at_the_end_of_the_night()
    {
        var game = Camped.Value;
        var crowd = game.Crowd;

        Assert.True(crowd.CampersIn > 1000, $"Only {crowd.CampersIn} campers came.");
        Assert.Equal(crowd.CampersIn * FestivalGame.CampingFee, game.Income.Where(l => l.What == "Camping").Sum(l => l.Amount));
        Assert.True(crowd.InTents >= crowd.CampersIn * 0.97, $"{crowd.InTents} of {crowd.CampersIn} campers got to their tents.");
        Assert.Equal(0, game.CrushInjuries);
        Assert.True(crowd.Happiness >= 70, $"Only {crowd.Happiness}% happy.");
    }

    [Fact]
    public void Some_campers_leave_a_mess_for_the_morning()
    {
        var game = Camped.Value;
        var camp = game.Field.Camp;
        var inCamp = game.Litter.Count(l => camp.Contains(FieldMap.TileAt(l.X, l.Y)));

        Assert.Equal(Phase.Tidy, game.Phase);
        Assert.InRange(inCamp, game.Crowd.Tents.Count() / 2, game.Crowd.Tents.Count() * 2);
        Assert.Equal(game.Litter.Count, game.LitterAtStart);
    }

    [Fact]
    public void A_day_with_camping_saves_and_loads_and_plays_out_the_same()
    {
        var game = GrowthTests.Built(5000, 90000, camping: true);
        game.OpenGates();
        while (game.Now.TimeOfDay < TimeSpan.FromHours(9))
        {
            game.Clock.Step();
        }

        var loaded = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));
        Assert.True(loaded.CampingBooked);
        Assert.Equal(game.Crowd.Campers, loaded.Crowd.Campers);
        Assert.Equal(game.Crowd.CampersIn, loaded.Crowd.CampersIn);
        Assert.Equal(game.Crowd.Tents.ToList(), loaded.Crowd.Tents.ToList());
        for (var i = 0; i < 900; i++)
        {
            game.Clock.Step();
            loaded.Clock.Step();
        }

        Assert.Equal(game.Crowd.CampersIn, loaded.Crowd.CampersIn);
        Assert.Equal(game.Money, loaded.Money);
        Assert.Equal(game.Crowd.OnSite, loaded.Crowd.OnSite);
    }
}
