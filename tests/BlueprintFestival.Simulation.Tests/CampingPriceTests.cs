using BlueprintFestival.Simulation.People;

namespace BlueprintFestival.Simulation.Tests;

public class CampingPriceTests
{
    private static FestivalGame Showground() => FestivalGame.StartYear("Larkmoor Festival", new YearSetup(6, 5000, 90000));

    [Fact]
    public void Cheap_pitches_bring_more_campers_but_never_more_than_half()
    {
        Assert.Equal(0.5, Crowd.CamperShareAt(0), 3);
        var last = 1.0;
        for (var pounds = 0; pounds <= FestivalGame.DearestPitch; pounds++)
        {
            var share = Crowd.CamperShareAt(pounds);
            Assert.True(share <= Crowd.MostCamping && share < last, $"£{pounds} gives {share:P0}.");
            last = share;
        }

        Assert.InRange(Crowd.CamperShareAt(FestivalGame.CampingFee), 0.3, 0.4);
        Assert.True(Crowd.CamperShareAt(20) < 0.07);
    }

    [Fact]
    public void The_player_sets_the_pitch_price_while_planning_and_the_expected_campers_follow()
    {
        var game = Showground();
        game.BookCamping(true);
        Assert.Equal(FestivalGame.CampingFee, game.CampingPrice);

        game.SetCampingPrice(0);
        var free = game.CampersExpected;
        game.SetCampingPrice(12);
        var dear = game.CampersExpected;

        Assert.True(free > dear * 2, $"{free} free against {dear} at £12.");
        Assert.InRange(free, game.ExpectedAt(game.TicketPrice) * 0.49, game.ExpectedAt(game.TicketPrice) * 0.5 + 1);

        game.SetCampingPrice(-5);
        Assert.Equal(0, game.CampingPrice);
        game.SetCampingPrice(500);
        Assert.Equal(FestivalGame.DearestPitch, game.CampingPrice);
    }

    [Fact]
    public void Fewer_parties_pitch_a_tent_when_pitches_are_dear_and_each_pays_the_price_set()
    {
        int Pitched(int pounds)
        {
            var game = Showground();
            game.BookCamping(true);
            game.SetCampingPrice(pounds);
            Assert.True(game.ConfirmPlan());
            return game.Crowd.Parties.Count(p => p.Pitch is not null);
        }

        var cheap = Pitched(1);
        var usual = Pitched(FestivalGame.CampingFee);
        var dear = Pitched(15);
        Assert.True(cheap > usual && usual > dear * 2, $"{cheap}, {usual}, {dear} parties camped.");
        Assert.True(dear > 0);
    }

    [Fact]
    public void Changing_the_price_on_build_day_moves_the_tents()
    {
        var game = Showground();
        game.BookCamping(true);
        Assert.True(game.ConfirmPlan());
        var before = game.Crowd.Parties.Count(p => p.Pitch is not null);

        game.SetCampingPrice(20);

        Assert.True(game.Crowd.Parties.Count(p => p.Pitch is not null) < before / 3);
    }

    [Fact]
    public void The_pitch_price_is_fixed_once_the_gates_open()
    {
        var game = GrowthTests.Built(5000, 90000, camping: true);
        game.SetCampingPrice(9);
        game.StartDay();

        Assert.Throws<InvalidOperationException>(() => game.SetCampingPrice(2));
        Assert.Equal(9, game.CampingPrice);
    }

    [Fact]
    public void The_pitch_price_is_saved_and_kept_for_next_year()
    {
        var game = Showground();
        game.BookCamping(true);
        game.SetCampingPrice(11);

        var loaded = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));

        Assert.Equal(11, loaded.CampingPrice);
        Assert.Equal(game.CampersExpected, loaded.CampersExpected);
    }
}
