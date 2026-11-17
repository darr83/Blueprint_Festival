using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

/// <summary>Stands: seated stands, terraces and raised decks dragged out facing a stage.</summary>
public class StandTests
{
    /// <summary>A plain field with a bandstand facing south and nothing else.</summary>
    private static FestivalGame Field()
    {
        var game = new FestivalGame(seed: 5);
        var f = game.Field.Field;
        Assert.True(game.BuildStage(StageKind.Bandstand, f.X + 50, f.Y + 14, Facing.South).Ok);
        return game;
    }

    /// <summary>A line across the field some way in front of the first stage, this many tiles long.</summary>
    private static (TilePoint From, TilePoint To) LineInFront(FestivalGame game, int back = 30, int length = 24)
    {
        var r = game.Site.Stages[0].Rect;
        var y = r.Bottom + back;
        return (new TilePoint(r.X, y), new TilePoint(r.X + length - 1, y));
    }

    private static IEnumerable<TilePoint> Tiles(TileRect r) =>
        Enumerable.Range(r.X, r.Width).SelectMany(x => Enumerable.Range(r.Y, r.Height).Select(y => new TilePoint(x, y)));

    [Fact]
    public void A_seated_stand_faces_the_stage_with_rows_going_back_and_up_and_steps_at_the_front()
    {
        var game = Field();
        var (from, to) = LineInFront(game);
        var money = game.Money;
        var check = game.BuildStand(StandKind.Seated, from, to, 5, StandRoof.Full);
        Assert.True(check.Ok, check.Problem);

        var stand = Assert.Single(game.Site.Stands);
        Assert.Equal(Facing.North, stand.Facing);
        Assert.Equal(from.Y, stand.Rect.Y);
        Assert.Equal(24, stand.Rect.Width);
        Assert.Equal(22, stand.Length);
        Assert.Equal(5, stand.Rows);
        Assert.Equal(5 * 22, stand.Places);
        Assert.Equal(Stands.CostOf(StandKind.Seated, 22, 5, StandRoof.Full), money - game.Money);
        Assert.Equal(Stands.CostOf(stand), check.Cost);

        // Each row is a step higher than the one in front.
        for (var row = 0; row < 5; row++)
        {
            Assert.Equal(Stands.Rise * (row + 1), game.Site.HeightAt(stand.At(3, (row * Stands.RowTiles) + 1)), 6);
        }

        // Rail all round except the steps, which are on the front, towards the stage.
        Assert.Equal(2, stand.Steps.Count);
        Assert.All(stand.Steps, s => Assert.Equal(stand.Rect.Y, s.Y));
        var stepTiles = stand.Steps.SelectMany(s => SiteLayout.StepTilesOf(stand, s)).ToHashSet();
        foreach (var p in Tiles(stand.Rect))
        {
            Assert.Equal(stand.IsEdge(p) && !stepTiles.Contains(p), game.Site.IsStandRail(p));
            Assert.Equal(!game.Site.IsStandRail(p), game.Field.IsWalkable(p));
        }

        Assert.All(stepTiles, p => Assert.True(game.Site.HeightAt(p) > 0 && game.Site.HeightAt(p) < Stands.Rise));
        Assert.True(game.Site.StandRoofOver(FieldMap.CentreOf(stand.At(4, 4)).X, FieldMap.CentreOf(stand.At(4, 4)).Y));
    }

    [Fact]
    public void A_stand_is_turned_round_with_R_and_a_line_up_and_down_makes_it_face_sideways()
    {
        var game = Field();
        var (from, to) = LineInFront(game);
        Assert.True(game.BuildStand(StandKind.Terrace, from, to, 3, StandRoof.None, turned: true).Ok);
        Assert.Equal(Facing.South, game.Site.Stands[0].Facing);
        Assert.Equal(from.Y, game.Site.Stands[0].Rect.Bottom - 1);

        // Up and down, to the east of the stage: it looks west, towards it.
        var r = game.Site.Stages[0].Rect;
        var side = new TilePoint(r.Right + 30, r.Bottom + 4);
        Assert.True(game.BuildStand(StandKind.Deck, side, new TilePoint(side.X, side.Y + 15), 4, StandRoof.None).Ok);
        var deck = game.Site.Stands[1];
        Assert.Equal(Facing.West, deck.Facing);
        Assert.Equal(16, deck.Rect.Height);
        Assert.Equal(Stands.DeckHeight, game.Site.HeightAt(deck.At(2, 2)));

        // A deck has its steps at the back, away from the stage.
        Assert.All(deck.Steps, s => Assert.Equal(deck.Rect.Right - 1, s.X));
    }

    [Fact]
    public void Stands_must_be_long_enough_and_cannot_overlap_anything()
    {
        var game = Field();
        var (from, to) = LineInFront(game);
        Assert.False(game.CheckStand(StandKind.Seated, from, new TilePoint(from.X + 4, from.Y), 3, StandRoof.None).Ok);
        Assert.False(game.CheckStand(StandKind.Seated, from, new TilePoint(from.X + 90, from.Y), 3, StandRoof.None).Ok);
        Assert.True(game.BuildStand(StandKind.Seated, from, to, 3, StandRoof.None).Ok);
        Assert.False(game.CheckStand(StandKind.Deck, new TilePoint(from.X + 4, from.Y + 2), new TilePoint(from.X + 20, from.Y + 2), 3, StandRoof.None).Ok);
        var r = game.Site.Stages[0].Rect;
        Assert.False(game.CheckStand(StandKind.Deck, new TilePoint(r.X, r.Y + 2), new TilePoint(r.X + 12, r.Y + 2), 2, StandRoof.None).Ok);

        // Rows are kept between one and ten.
        Assert.Equal(Stands.MostRows, game.Site.StandBox(new TilePoint(10, 10), new TilePoint(30, 10), 40).Rect.Height / Stands.RowTiles - 1);
    }

    [Fact]
    public void Steps_can_be_added_and_closed_but_a_stand_keeps_one_way_up()
    {
        var game = Field();
        var (from, to) = LineInFront(game);
        Assert.True(game.BuildStand(StandKind.Seated, from, to, 4, StandRoof.None).Ok);
        var stand = game.Site.Stands[0];
        var side = new TilePoint(stand.Rect.Right - 1, stand.Rect.Y + 4);
        Assert.Null(game.ToggleStandSteps(side));
        Assert.Equal(3, game.Site.Stands[0].Steps.Count);
        Assert.True(game.Field.IsWalkable(side));
        Assert.NotNull(game.ToggleStandSteps(new TilePoint(stand.Rect.X, stand.Rect.Y)));
        foreach (var step in game.Site.Stands[0].Steps.ToList().Skip(1))
        {
            Assert.Null(game.ToggleStandSteps(step));
        }

        Assert.Single(game.Site.Stands[0].Steps);
        Assert.NotNull(game.ToggleStandSteps(game.Site.Stands[0].Steps[0]));
    }

    [Fact]
    public void Who_may_use_a_stand_and_its_seat_price_are_kept_in_the_save_and_taking_it_down_pays_it_all_back()
    {
        var game = Field();
        var (from, to) = LineInFront(game);
        var money = game.Money;
        Assert.True(game.BuildStand(StandKind.Seated, from, to, 3, StandRoof.BackHalf).Ok);
        Assert.True(game.SetStandUse(0, StandUse.SeatTicket));
        Assert.Equal(8, game.SetStandPrice(0, 8));
        Assert.Equal(Stands.MostPrice, game.SetStandPrice(0, 500));
        Assert.Equal(1, game.SetStandPrice(0, 0));
        game.SetStandPrice(0, 9);
        Assert.Null(game.SetStandPrice(4, 9));

        var restored = new SiteLayout(game.Field);
        restored.Restore(game.Site.Save());
        var stand = Assert.Single(restored.Stands);
        Assert.Equal(StandUse.SeatTicket, stand.Use);
        Assert.Equal(9, stand.Price);
        Assert.Equal(StandRoof.BackHalf, stand.Roof);
        Assert.Equal(game.Site.Stands[0].Steps, stand.Steps);
        Assert.Equal(game.Site.Stands[0].Rect, stand.Rect);

        game.Remove(stand.At(2, 2));
        Assert.Empty(game.Site.Stands);
        Assert.Equal(money, game.Money);
        Assert.True(game.Field.IsWalkable(stand.At(-1, -1)));
    }

    [Fact]
    public void A_VIP_stand_is_for_VIPs_and_the_accessible_deck_for_those_who_need_it()
    {
        var game = Field();
        var (from, to) = LineInFront(game);
        Assert.True(game.BuildStand(StandKind.Deck, from, to, 3, StandRoof.None).Ok);
        var inside = game.Site.Stands[0].At(3, 2);
        Assert.True(game.Field.IsWalkable(inside));
        game.SetStandUse(0, StandUse.Vip);
        Assert.False(game.Field.IsWalkable(inside));
        Assert.True(game.Field.IsWalkableForVip(inside));
        Assert.Equal(0, game.Crowd.StandWish(game.Crowd.Parties.First(p => !p.Vip), game.Site.Stands[0], 0.5));

        var accessible = game.Site.Stands[0] with { Kind = StandKind.Accessible, Use = StandUse.Anyone };
        Assert.Contains(game.Crowd.Parties, p => p.NeedsAccess);
        Assert.All(game.Crowd.Parties.Take(300), p => Assert.Equal(p.NeedsAccess, game.Crowd.StandWish(p, accessible, 0.5) > 0));
        Assert.True(Crowd.WillPay(5) > Crowd.WillPay(10));
        Assert.Equal(0, Crowd.WillPay(Stands.MostPrice));
    }

    /// <summary>A big name on the farm trailer with a seated stand facing it: how full does the stand get, and what does it take?</summary>
    private static (int Most, int Places, bool Seated, int Takings, int Falls) BigNameWithStand(bool steward, int length, int rows, StandUse use)
    {
        var game = FestivalGame.NewFestival("Larkmoor Festival");
        game.ChooseHeadliner(game.Headliners.ToList().FindIndex(h => h.Stars == game.Headliners.Max(x => x.Stars)));
        Assert.True(game.ConfirmPlan());
        var gateGap = game.Field.Gap;
        var gy = gateGap.Y + 1;
        Assert.True(game.BuildFence(FenceKind.Rope, new TilePoint(gateGap.X, gy), new TilePoint(gateGap.Right - 1, gy)).Ok);
        Assert.True(game.BuildGate(GateKind.VillageGate, gateGap.X, gy, false).Ok);
        var f = game.Field.Field;
        Assert.True(game.BuildStage(StageKind.FarmTrailer, f.X + 50, f.Y + 6, Facing.South).Ok);
        Amenities.Add(game);
        Assert.True(game.HireStaff(StaffKind.TicketStaff, new TilePoint(gateGap.X + 1, gy)).Ok);
        var r = game.Site.Stages[0].Rect;
        var from = new TilePoint(r.X - 4, r.Bottom + 26);
        var built = game.BuildStand(StandKind.Seated, from, new TilePoint(from.X + length + 1, from.Y), rows, StandRoof.None);
        Assert.True(built.Ok, built.Problem);
        game.SetStandUse(0, use);
        var stand = game.Site.Stands[0];
        if (steward)
        {
            foreach (var step in stand.Steps)
            {
                var hired = game.HireStaff(StaffKind.Security, new TilePoint(step.X, step.Y - 2));
                Assert.True(hired.Ok, hired.Problem + $" money {game.Money}");
            }
        }

        var takings = 0;
        game.Crowd.SeatTicketsSold += (_, paid) => takings += paid;
        var most = 0;
        var seated = false;
        game.OpenGates();
        var guard = 0;
        while (game.Phase == Phase.Day)
        {
            game.Clock.Step();
            if (game.Now.Second == 0)
            {
                var up = game.Crowd.Parties.Where(p => Crowd.UpOnAStand(p)).ToList();
                most = Math.Max(most, up.Sum(p => p.Members.Count));
                if (up.Count > 0 && !seated)
                {
                    // Drawn sitting, high up on their row, facing the stage.
                    var ids = up.SelectMany(p => p.Members.Select(m => m.Id)).ToHashSet();
                    var people = game.Crowd.People.Where(p => ids.Contains(p.Id)).ToList();
                    seated = people.Count > 0 && people.All(p => p.Seat == Stands.SeatHeight && game.Site.HeightAt(FieldMap.TileAt(p.X, p.Y)) >= Stands.Rise && stand.Rect.Contains(FieldMap.TileAt(p.X, p.Y)));
                }
            }

            Assert.True(++guard < 70000, "The day never ended.");
        }

        return (most, stand.Places, seated, takings, game.StandFalls);
    }

    [Fact]
    public void People_watch_from_a_stewarded_stand_sitting_down_and_it_never_holds_more_than_its_seats()
    {
        var (most, places, seated, _, falls) = BigNameWithStand(steward: true, length: 10, rows: 2, StandUse.Anyone);
        Assert.True(most >= places / 2, $"People should go up on the stand: {most} of {places}.");
        Assert.True(most <= places, $"A stewarded stand should never hold more than its seats: {most} of {places}.");
        Assert.True(seated, "People on a seated stand should be drawn sitting on their row.");
        Assert.Equal(0, falls);
    }

    [Fact]
    public void With_nobody_on_the_steps_people_squeeze_on_past_its_seats()
    {
        var (most, places, _, _, _) = BigNameWithStand(steward: false, length: 10, rows: 2, StandUse.Anyone);
        Assert.True(most > places, $"Without a steward more than the seats should squeeze on: {most} of {places}.");
    }

    [Fact]
    public void Seat_tickets_are_paid_at_the_steps()
    {
        var (most, _, _, takings, _) = BigNameWithStand(steward: true, length: 10, rows: 2, StandUse.SeatTicket);
        Assert.True(most > 0, "Some should pay for a seat.");
        Assert.True(takings >= Stands.StartPrice * most, $"Everyone up there paid £{Stands.StartPrice}: £{takings} for {most}.");
    }
}
