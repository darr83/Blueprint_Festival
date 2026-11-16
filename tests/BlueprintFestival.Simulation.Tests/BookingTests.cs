using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

/// <summary>Booking the acts: offers, other festivals, late bookings and stages big enough.</summary>
public class BookingTests
{
    private static FestivalGame Year(int licence = 1500, int bank = 40000) =>
        FestivalGame.StartYear("Larkmoor Festival", new YearSetup(4, licence, bank, false, 80));

    private static FestivalGame Empty(int licence = 1500, int bank = 40000)
    {
        var game = Year(licence, bank);
        game.ClearBill();
        return game;
    }

    [Fact]
    public void The_bill_can_start_empty_for_the_player_to_fill()
    {
        var game = Empty();
        Assert.Empty(game.LineUp);
        Assert.False(game.SecondStageBooked);
        Assert.Equal(0, game.ActFees);
        Assert.Null(game.StageNeeded(false));
        Assert.All(Roster.Artists, a => Assert.Equal(Availability.Free, game.AvailabilityOf(a)));
    }

    [Fact]
    public void The_chance_of_a_yes_runs_from_nothing_at_an_insult_to_certain_at_the_asking_fee()
    {
        Assert.Equal(1, Booking.ChanceOfYes(1000, 1000, 3));
        Assert.Equal(1, Booking.ChanceOfYes(1200, 1000, 3));
        Assert.Equal(0, Booking.ChanceOfYes(590, 1000, 3));
        Assert.Equal(0.5, Booking.ChanceOfYes(800, 1000, 3), 6);
        Assert.True(Booking.ChanceOfYes(800, 1000, 0) > Booking.ChanceOfYes(800, 1000, 3));
        Assert.True(Booking.ChanceOfYes(900, 1000, 2) > Booking.ChanceOfYes(700, 1000, 2));
        Assert.Equal("About 5 in 10", Booking.ChanceWords(0.5));
        Assert.Equal("Certain", Booking.ChanceWords(1));
    }

    [Fact]
    public void Paying_what_they_ask_always_books_them_and_the_fee_is_what_was_agreed()
    {
        var game = Empty();
        var artist = Roster.Named("Tin Thunder")!;
        var ask = game.AskOf(artist);
        Assert.Equal(game.FeeOf(3), ask);
        Assert.Equal(OfferOutcome.Accepted, game.Offer(3, artist.Name, ask));
        Assert.Equal(3, game.SetOf(artist.Name));
        Assert.Equal(ask, game.ActFees);
        Assert.Equal(Availability.Booked, game.AvailabilityOf(artist));
    }

    [Fact]
    public void A_lower_offer_is_a_gamble_and_two_refusals_send_them_elsewhere()
    {
        // Across many festivals a cheeky offer is sometimes taken and sometimes not, and an insult never is.
        var (yes, no) = (0, 0);
        for (var year = 1; year <= 40; year++)
        {
            var game = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(year, 1500, 40000, false, 80));
            game.ClearBill();
            var artist = Roster.Named("The Copper Turnips")!;
            var ask = game.AskOf(artist);
            Assert.Equal(OfferOutcome.Refused, game.Offer(2, artist.Name, ask / 2));
            Assert.Equal(1, game.RefusalsFrom(artist.Name));
            var outcome = game.Offer(2, artist.Name, ask * 8 / 10);
            if (outcome == OfferOutcome.Accepted)
            {
                yes++;
                Assert.Equal(ask * 8 / 10, game.ActFees);
                Assert.Equal(ask * 8 / 10, game.FeeFor(artist.Name));
            }
            else
            {
                no++;
                Assert.Equal(OfferOutcome.WalkedAway, outcome);
                Assert.Equal(Availability.WalkedAway, game.AvailabilityOf(artist));
                Assert.Equal(OfferOutcome.NotFree, game.Offer(2, artist.Name, ask * 2));
                Assert.False(game.BookAct(2, artist.Name));
                Assert.Empty(game.LineUp);
            }
        }

        Assert.True(yes >= 8 && no >= 8, $"{yes} yes, {no} no");
    }

    [Fact]
    public void A_big_name_only_plays_one_of_the_last_two_sets()
    {
        var game = Empty(5000, 90000);
        Assert.Equal(OfferOutcome.WantsLaterSet, game.Offer(0, "The Lantern Hearts", 99999));
        Assert.Equal(OfferOutcome.WantsLaterSet, game.Offer(1, "Hedgerow Howl", 99999));
        Assert.Equal(OfferOutcome.Accepted, game.Offer(2, "Hedgerow Howl", 99999));
        Assert.Equal(OfferOutcome.Accepted, game.Offer(3, "The Lantern Hearts", 99999));
        Assert.Equal(OfferOutcome.Accepted, game.Offer(0, "Tin Thunder", 99999));

        // Offering more than they ask still only costs what they ask.
        Assert.Equal(game.FeeOf(4) + game.FeeOf(5) + game.FeeOf(3), game.ActFees);

        // A three star act cannot be swapped into a set by pushing a big name out of the last two.
        Assert.False(game.BookAct(3, "Tin Thunder"));
        Assert.Equal(StageSize.Large, game.StageNeeded(false));
    }

    [Fact]
    public void Bigger_names_have_more_festivals_after_them_and_are_gone_sooner()
    {
        var game = Empty();
        Assert.All(Roster.Artists.Where(a => a.Rating <= 2), a =>
        {
            Assert.Equal(0, game.RivalsFor(a));
            Assert.Null(game.GoneAfter(a));
        });
        Assert.All(Roster.Artists.Where(a => a.Rating == 5), a =>
        {
            Assert.InRange(game.RivalsFor(a), 4, 5);
            Assert.InRange(game.GoneAfter(a)!.Value, 0, 1);
        });
        Assert.All(Roster.Artists.Where(a => a.Rating == 4), a => Assert.InRange(game.GoneAfter(a)!.Value, 1, 3));
    }

    [Fact]
    public void Acts_can_be_booked_into_empty_sets_while_tickets_sell_but_they_cost_more_and_the_best_are_gone()
    {
        var game = Empty(1500, 40000);
        Assert.Equal(OfferOutcome.Accepted, game.Offer(3, "Tin Thunder", 99999));
        Assert.True(game.BookEvent());
        var bank = game.Money;
        var star = Roster.Named("The Lantern Hearts")!;
        var cheap = Roster.Named("Marnie Thrupp")!;
        var january = game.AskOf(cheap);

        Assert.NotNull(game.RunSales());
        Assert.NotNull(game.RunSales());
        Assert.NotNull(game.RunSales());
        Assert.True(game.AskOf(cheap) > january || january < 13);
        Assert.Equal((int)Math.Round(game.FeeOf(5) * (1 + (3 * Booking.DearerAMonth))), game.AskOf(star));

        // Three months in, the five star act has signed for somebody else.
        Assert.Equal(Availability.Gone, game.AvailabilityOf(star));
        Assert.Equal(OfferOutcome.NotFree, game.Offer(2, star.Name, 99999));

        // A local act is still free, goes into an empty set, and is paid on the spot.
        var before = game.Money;
        var pull = game.LineUpPull;
        Assert.Equal(OfferOutcome.NotPossible, game.Offer(3, cheap.Name, 99999));
        Assert.Equal(OfferOutcome.Accepted, game.Offer(0, cheap.Name, 99999));
        Assert.Equal(before - game.AskOf(cheap), game.Money);
        Assert.True(game.LineUpPull > pull);
        Assert.Equal(OfferOutcome.NotPossible, game.Offer(1, cheap.Name, 99999));
        Assert.True(bank > 0);
    }

    [Fact]
    public void An_act_will_not_play_a_stage_too_small_for_it_and_people_arrive_let_down()
    {
        var game = Empty(1500, 40000);
        Assert.Equal(OfferOutcome.Accepted, game.Offer(3, "Hedgerow Howl", 99999));
        Assert.Equal(OfferOutcome.Accepted, game.Offer(0, "Marnie Thrupp", 99999));
        Assert.Equal(StageSize.Medium, game.StageNeeded(false));
        Assert.True(game.BookEvent());
        while (game.Phase == Phase.Sell)
        {
            Assert.NotNull(game.RunSales());
        }

        Assert.Contains(game.StageProblems, p => p.Contains("no stage"));
        var f = game.Field.Field;
        Assert.True(game.BuildStage(StageKind.Bandstand, f.X + 40, f.Y + 6, Facing.South).Ok);
        var problem = Assert.Single(game.StageProblems);
        Assert.Contains("Hedgerow Howl will not play the bandstand", problem);
        Assert.Contains("medium stage (from £1,500)", problem);

        var json = System.Text.Json.JsonSerializer.Serialize(game.Save());
        var mood = game.ArrivalMood;
        game.StartDay();
        Assert.Equal(new[] { "Hedgerow Howl" }, game.NoShows);
        Assert.Equal("Marnie Thrupp", Assert.Single(game.Show.Slots).Act.Name);
        Assert.Empty(game.StageProblems);
        Assert.True(game.ArrivalMood < mood);

        // With a stage big enough everybody plays.
        var again = FestivalGame.Load(System.Text.Json.JsonSerializer.Deserialize<SaveData>(json)!);
        Assert.Equal(40, again.Remove(new TilePoint(f.X + 42, f.Y + 8)) / 10);
        Assert.True(again.BuildStage(StageKind.TruckStage, f.X + 40, f.Y + 6, Facing.South).Ok);
        Assert.Empty(again.StageProblems);
        again.StartDay();
        Assert.Empty(again.NoShows);
        Assert.Equal(2, again.Show.Slots.Count);
    }

    [Fact]
    public void Agreed_fees_and_refusals_are_saved_and_old_saves_still_load()
    {
        var game = Empty();
        var artist = Roster.Named("The Copper Turnips")!;
        Assert.Equal(OfferOutcome.Refused, game.Offer(2, artist.Name, 1));
        Assert.Equal(OfferOutcome.Accepted, game.Offer(1, "Marnie Thrupp", 99999));
        var loaded = FestivalGame.Load(System.Text.Json.JsonSerializer.Deserialize<SaveData>(System.Text.Json.JsonSerializer.Serialize(game.Save()))!);
        Assert.Equal(1, loaded.RefusalsFrom(artist.Name));
        Assert.Equal(game.ActFees, loaded.ActFees);
        Assert.Equal(new[] { "Marnie Thrupp" }, loaded.LineUp.Select(s => s.Act.Name));

        var old = game.Save();
        var bare = FestivalGame.Load(old with { Year = old.Year! with { Booking = null } });
        Assert.Equal(0, bare.RefusalsFrom(artist.Name));
    }
}
