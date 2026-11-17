using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;

namespace BlueprintFestival.Simulation.Tests;

/// <summary>An artist's stars and following decide how big a crowd they pull; everyone's taste in music decides who goes and who enjoys it.</summary>
public class FanPullTests
{
    [Fact]
    public void More_stars_and_more_fans_pull_more()
    {
        Assert.True(FanPull.Of(5, 1000) > FanPull.Of(4, 1000));
        Assert.True(FanPull.Of(3, 50000) > FanPull.Of(3, 5000));
        Assert.Equal(0, FanPull.Of(1, 10));
        Assert.Equal(1, FanPull.Of(5, 10000000));
        Assert.InRange(FanPull.Of(3, 38000), 0.45, 0.6);
    }

    [Fact]
    public void A_bigger_name_draws_more_of_the_field_and_the_last_act_of_the_night_most()
    {
        var byPull = Roster.Artists.OrderBy(FanPull.Of).ToList();
        for (var i = 1; i < byPull.Count; i++)
        {
            Assert.True(ActGenerator.DrawOf(byPull[i], false) >= ActGenerator.DrawOf(byPull[i - 1], false));
        }

        Assert.All(Roster.Artists, a =>
        {
            Assert.InRange(ActGenerator.DrawOf(a, false), 0.3, 0.75);
            Assert.InRange(ActGenerator.DrawOf(a, true), 0.6, 0.97);
            Assert.True(ActGenerator.DrawOf(a, true) > ActGenerator.DrawOf(a, false));
        });

        var local = ActGenerator.DrawOf(Roster.Named("Marnie Thrupp")!, false);
        var star = ActGenerator.DrawOf(Roster.Named("The Velvet Otters")!, false);
        Assert.True(star > local * 2);
        Assert.Equal("small crowd", FanPull.Words(FanPull.Of(Roster.Named("Marnie Thrupp")!)));
        Assert.Equal("huge crowd", FanPull.Words(FanPull.Of(Roster.Named("DJ Otter")!)));
    }

    [Fact]
    public void Tastes_are_spread_across_the_crowd_and_the_same_person_keeps_theirs()
    {
        var tastes = Enumerable.Range(0, 20000).Select(FanPull.TasteOf).ToList();
        Assert.Equal(tastes[123], FanPull.TasteOf(123));
        Assert.All(tastes, t => Assert.NotEqual(t.Favourite, t.Hates));

        double Share(Genre g) => tastes.Count(t => t.Favourite == g) / (double)tastes.Count;
        Assert.InRange(Share(Genre.Pop), 0.15, 0.21);
        Assert.InRange(Share(Genre.Metal), 0.05, 0.09);
        Assert.InRange(Share(Genre.Reggae), 0.015, 0.045);
        Assert.All(Enum.GetValues<Genre>(), g => Assert.True(Share(g) > 0.01, $"Nobody likes {g} best."));
        Assert.InRange(tastes.Count(t => t.Hates is not null) / (double)tastes.Count, 0.32, 0.38);
        Assert.All(Enum.GetValues<Genre>(), g => Assert.Contains(tastes, t => t.Hates == g));
    }

    [Fact]
    public void Fans_of_the_music_are_keener_and_enjoy_it_more_and_haters_the_reverse()
    {
        var hater = Enumerable.Range(0, 1000).First(p => FanPull.TasteOf(p).Hates is not null);
        var (favourite, hates) = FanPull.TasteOf(hater);
        var neutral = Enum.GetValues<Genre>().First(g => g != favourite && g != hates);

        Assert.True(FanPull.Keenness(hater, favourite) > FanPull.Keenness(hater, neutral));
        Assert.True(FanPull.Keenness(hater, neutral) > FanPull.Keenness(hater, hates!.Value));
        Assert.True(FanPull.Enjoyment(hater, favourite) > FanPull.Enjoyment(hater, neutral));
        Assert.True(FanPull.Enjoyment(hater, neutral) > FanPull.Enjoyment(hater, hates.Value));
        Assert.True(FanPull.ChanceToWatch(hater, favourite, 0.9) < 1);
    }

    [Fact]
    public void A_line_up_with_a_bigger_headline_name_sells_better()
    {
        var game = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(4, 1500, 60000, false, 80));
        Assert.True(game.BookAct(RunningOrder.SetsAStage - 1, "Little Ferrets"));
        var small = game.LineUpPull;
        Assert.True(game.BookAct(RunningOrder.SetsAStage - 1, "The Lantern Hearts"));
        Assert.True(game.LineUpPull > small + 0.45, $"{small} {game.LineUpPull}");
    }

    [Fact]
    public void A_big_name_pulls_a_bigger_crowd_to_the_stage_than_a_local_turn_and_fans_of_the_music_come_most()
    {
        var (localCrowd, _, _) = Watch("Marnie Thrupp");
        var (starCrowd, fans, haters) = Watch("The Velvet Otters");
        Assert.True(starCrowd > localCrowd * 1.5, $"{starCrowd} watching the big name, {localCrowd} the local turn");

        // Pop fans turn out for a pop act far more than people who cannot stand pop.
        Assert.True(fans > haters * 2, $"{fans} of pop fans watched, {haters} of pop haters");
    }

    /// <summary>
    /// Runs the first set of a festival with an artist in it and counts those watching halfway
    /// through, with the share of fans of the artist's music and of those who cannot stand it.
    /// </summary>
    private static (int Watching, double Fans, double Haters) Watch(string artist)
    {
        var game = new FestivalGame(seed: 21);
        var f = game.Field.Field;
        Assert.True(game.BuildStage(StageKind.Bandstand, f.X + 50, f.Y + 14, Facing.South).Ok);
        Amenities.Add(game);
        var act = ActGenerator.Of(Roster.Named(artist)!);
        game.Show.Book(1, act);
        var mid = game.Show.TimeOf(1).Start.AddMinutes(45);
        while (game.Now < mid)
        {
            game.Clock.Step();
        }

        var onSite = game.Crowd.Parties.Where(p => p.IsOnSite).ToList();
        double ShareWatching(Func<Party, bool> who)
        {
            var group = onSite.Where(who).ToList();
            return group.Count == 0 ? 0 : group.Count(p => p.State == PartyState.Watching) / (double)group.Count;
        }

        return (game.Crowd.Watching,
            ShareWatching(p => FanPull.TasteOf(p.Id).Favourite == act.Genre),
            ShareWatching(p => FanPull.TasteOf(p.Id).Hates == act.Genre));
    }
}
