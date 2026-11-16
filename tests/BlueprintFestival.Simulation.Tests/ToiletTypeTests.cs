using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

/// <summary>
/// The newer toilets: urinals that only men and boys use and that are quicker, a back-to-back
/// block whose back doors people walk round to, and an accessible toilet kept for the guests who
/// need it.
/// </summary>
public class ToiletTypeTests
{
    private static readonly DateTime Day = GameClock.DefaultStart.Date;

    /// <summary>A festival with these toilets in a row along the top and a bar, so people need them.</summary>
    private static FestivalGame With(params StallKind[] kinds)
    {
        var game = new FestivalGame("Toilets", seed: 8);
        var f = game.Field.Field;
        var x = 6;
        foreach (var kind in kinds)
        {
            Assert.True(game.BuildStall(kind, f.X + x, f.Y + 14, Facing.South).Ok, kind.ToString());
            x += Catalogue.Of(kind).Width + 6;
        }

        Assert.True(game.BuildStall(StallKind.Bar, f.X + 40, f.Y + 50, Facing.South).Ok);
        return game;
    }

    /// <summary>Everyone using a cubicle or urinal place of a stall just now.</summary>
    private static IEnumerable<(Party Party, int Member, double X, double Y, bool Inside, int Place)> Users(FestivalGame game, int stall) =>
        Enumerable.Range(0, Catalogue.Of(game.Site.Stalls[stall].Kind).Servers)
            .Select(k => (k, use: game.Crowd.CubicleUser(stall, k)))
            .Where(u => u.use is not null)
            .Select(u => (u.use!.Value.Party, u.use.Value.Member, u.use.Value.X, u.use.Value.Y, u.use.Value.Inside, u.k));

    [Fact]
    public void The_new_toilets_are_on_the_toilet_shelf_and_serve_the_toilet_need()
    {
        foreach (var kind in new[] { StallKind.UrinalStand, StallKind.UrinalTrough, StallKind.BackToBackToilets, StallKind.AccessibleToilet })
        {
            var info = Catalogue.Of(kind);
            Assert.Equal(kind, info.Kind);
            Assert.Equal(Need.Toilet, info.Serves);
            Assert.Equal(StallGroup.Services, Catalogue.GroupOf(kind));
            Assert.Contains(info, Catalogue.StallsIn(StallGroup.Services));
        }

        Assert.True(Catalogue.IsUrinal(StallKind.UrinalStand) && Catalogue.IsUrinal(StallKind.UrinalTrough));
        Assert.False(Catalogue.HasCabins(StallKind.UrinalTrough));
        Assert.True(Catalogue.HasCabins(StallKind.BackToBackToilets));
        Assert.True(Catalogue.Of(StallKind.UrinalStand).SecondsPerPerson * 2 < Catalogue.Of(StallKind.Toilets).SecondsPerPerson);
    }

    [Fact]
    public void Only_parties_of_men_and_boys_use_the_urinals_and_they_stand_at_them_in_sight()
    {
        var game = With(StallKind.UrinalTrough, StallKind.Toilets);
        var users = new HashSet<Party>();
        var seenInside = 0;
        while (game.Now < Day.AddHours(15))
        {
            game.Clock.Step();
            foreach (var use in Users(game, 0))
            {
                users.Add(use.Party);
                if (use.Inside)
                {
                    // Nobody is hidden at a urinal: they are drawn standing at it, still.
                    seenInside++;
                    var member = use.Party.Members[use.Member];
                    var drawn = game.Crowd.People.Single(p => p.Id == member.Id);
                    Assert.False(drawn.Walking);
                    Assert.Equal(use.X, drawn.X, 3);
                }
            }
        }

        Assert.True(users.Count >= 5, $"{users.Count} parties used the urinals");
        Assert.All(users, p => Assert.True(p.AllMale));
        Assert.True(seenInside > 0);

        // Mixed parties and women went to the cabins instead.
        Assert.Contains(game.Crowd.Parties, p => !p.AllMale && p.Toilet < 0.5 && p.State != PartyState.Expected);
    }

    [Fact]
    public void A_urinal_visit_is_much_quicker_than_a_cabin()
    {
        var urinal = Visits(StallKind.UrinalStand);
        var cabin = Visits(StallKind.Toilets);
        Assert.True(urinal < cabin * 0.6, $"a urinal visit took {urinal:0} s, a cabin {cabin:0} s, from the front of the queue to done");
    }

    /// <summary>The average time from leaving the front of the queue to being done, at a kind of toilet, in seconds.</summary>
    private static double Visits(StallKind kind)
    {
        var game = With(kind);
        var started = new Dictionary<(Party, int), DateTime>();
        var visits = new List<double>();
        while (game.Now < Day.AddHours(15) && visits.Count < 10)
        {
            game.Clock.Step();
            var now = Users(game, 0).Select(u => (u.Party, u.Place)).ToHashSet();
            foreach (var key in now.Where(k => !started.ContainsKey(k)))
            {
                started[key] = game.Now;
            }

            foreach (var gone in started.Keys.Where(k => !now.Contains(k)).ToList())
            {
                visits.Add((game.Now - started[gone]).TotalSeconds);
                started.Remove(gone);
            }
        }

        Assert.True(visits.Count >= 10, $"{visits.Count} visits to the {kind}");
        return visits.Average();
    }

    [Fact]
    public void At_a_back_to_back_block_people_walk_round_to_the_back_doors_and_never_through_the_block()
    {
        var game = With(StallKind.BackToBackToilets);
        var block = game.Site.Stalls[0];
        const double t = FieldMap.TileMetres;
        var (left, top, right, bottom) = (block.Rect.X * t, block.Rect.Y * t, block.Rect.Right * t, block.Rect.Bottom * t);
        var used = new HashSet<int>();
        var back = 0;
        while (game.Now < Day.AddHours(16))
        {
            game.Clock.Step();
            foreach (var use in Users(game, 0))
            {
                used.Add(use.Place);

                // Inside a cabin is fine; walking through the middle of the block is not.
                if (!use.Inside && game.Crowd.Cubicle(0, use.Place) == CubicleLook.Vacant)
                {
                    var inBlock = use.X > left + 0.3 && use.X < right - 0.3 && use.Y > top + 0.3 && use.Y < bottom - 0.3;
                    Assert.False(inBlock, $"{game.Now:HH:mm:ss}: someone at {use.X:0.0},{use.Y:0.0} inside the block");
                }

                if (use.Y < top)
                {
                    back++;
                }
            }
        }

        // Doors on both sides were used, and people were seen behind the block on their way.
        Assert.Contains(used, k => k < 6);
        Assert.Contains(used, k => k >= 6);
        Assert.True(back > 0);
    }

    [Theory]
    [InlineData(Facing.South)]
    [InlineData(Facing.North)]
    [InlineData(Facing.East)]
    [InlineData(Facing.West)]
    public void The_back_row_doors_are_on_the_far_side_from_the_queue_whichever_way_the_block_faces(Facing facing)
    {
        var block = new PlacedStall(StallKind.BackToBackToilets, 40, 40, facing);
        var (fx, fy, dx, dy) = SiteLayout.Front(block.Rect, facing);
        for (var k = 0; k < 12; k++)
        {
            var way = SiteLayout.CubicleWay(block, k);
            var outFront = ((way.Door.X - fx) * dx) + ((way.Door.Y - fy) * dy);
            if (k < 6)
            {
                // The back row (the queue's side is the front).
                Assert.True(outFront < -2.5, $"place {k} door is {outFront:0.0} m out from the front");
                Assert.Equal(2, way.Round.Count);
            }
            else
            {
                Assert.InRange(outFront, 0.5, 1.5);
                Assert.Empty(way.Round);
            }
        }
    }

    [Fact]
    public void The_accessible_toilet_is_kept_for_the_guests_who_need_it_and_they_use_nothing_else()
    {
        var game = With(StallKind.AccessibleToilet, StallKind.Toilets);
        var users = new HashSet<Party>();
        var needing = new HashSet<Party>();
        while (game.Now < Day.AddHours(16))
        {
            game.Clock.Step();
            users.UnionWith(Users(game, 0).Select(u => u.Party));
            foreach (var use in Users(game, 1))
            {
                Assert.False(use.Party.NeedsAccess, "someone who needs the accessible toilet used a cabin");
            }

            needing.UnionWith(users);
        }

        Assert.NotEmpty(users);
        Assert.All(users, p => Assert.True(p.NeedsAccess));
        Assert.Equal(0, game.Crowd.WithoutAccessibleToilet);
    }

    [Fact]
    public void Without_an_accessible_toilet_the_guests_who_need_one_make_do_and_mind()
    {
        var game = With(StallKind.Toilets);
        var share = game.Crowd.Parties.Count(p => p.NeedsAccess) / (double)game.Crowd.Parties.Count;
        Assert.InRange(share, 0.02, 0.09);
        while (game.Now < Day.AddHours(16))
        {
            game.Clock.Step();
        }

        Assert.True(game.Crowd.WithoutAccessibleToilet > 0);
    }

    [Fact]
    public void A_back_to_back_block_and_urinals_save_and_load_mid_visit()
    {
        var game = With(StallKind.BackToBackToilets, StallKind.UrinalStand);
        while (game.Now < Day.AddHours(13) || !Users(game, 0).Any(u => u.Place < 6))
        {
            game.Clock.Step();
            Assert.True(game.Now < Day.AddHours(16));
        }

        var loaded = FestivalGame.Load(game.Save());
        for (var i = 0; i < 600; i++)
        {
            game.Clock.Step();
            loaded.Clock.Step();
        }

        Assert.Equal(game.Crowd.BeingServed(0), loaded.Crowd.BeingServed(0));
        Assert.Equal(game.Crowd.People.Count(), loaded.Crowd.People.Count());
        Assert.Equal(game.Crowd.Happiness, loaded.Crowd.Happiness);
    }
}
