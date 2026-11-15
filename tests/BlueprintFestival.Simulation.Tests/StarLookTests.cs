using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Show;

namespace BlueprintFestival.Simulation.Tests;

public class StarLookTests
{
    private static IEnumerable<(Act Act, int Member, PersonLook Look)> EveryMember() =>
        Roster.Artists.Select(a => ActGenerator.Of(a)).SelectMany(act => act.Members.Select((_, i) => (act, i, StarLooks.For(act, i))));

    [Fact]
    public void Every_artist_says_who_is_a_woman_and_who_is_a_man()
    {
        foreach (var artist in Roster.Artists)
        {
            var act = ActGenerator.Of(artist);
            Assert.Equal(act.Members.Count, artist.Who.Length);
            Assert.All(artist.Who, c => Assert.Contains(c, "fm"));
            for (var i = 0; i < act.Members.Count; i++)
            {
                Assert.Equal(artist.Who[i] == 'f', StarLooks.For(act, i).Female);
            }
        }

        // A mix of both, not all one or the other.
        var who = string.Concat(Roster.Artists.Select(a => a.Who));
        Assert.InRange(who.Count(c => c == 'f') / (double)who.Length, 0.3, 0.7);
    }

    [Fact]
    public void Each_kind_of_music_has_its_own_look()
    {
        foreach (var (act, _, look) in EveryMember())
        {
            switch (act.Genre)
            {
                case Genre.Country:
                    Assert.Equal(Hat.Cowboy, look.Hat);
                    Assert.True(look.Dungarees);
                    break;
                case Genre.Rock:
                    Assert.Contains(look.Hair, new[] { HairStyle.Long, HairStyle.Mane });
                    break;
                case Genre.Metal:
                    Assert.Equal(HairStyle.Mane, look.Hair);
                    Assert.Equal("#26292E", look.TopColour);
                    Assert.NotNull(look.Chain);
                    break;
                case Genre.Pop:
                    Assert.True(look.Jacket is not null || act.Members.Count > 1);
                    break;
                case Genre.HipHop:
                    Assert.NotNull(look.Chain);
                    break;
            }

            Assert.Null(look.Poncho);
        }

        // Rock stars wear leather jackets, unless they are behind the drums.
        Assert.All(EveryMember().Where(m => m.Act.Genre == Genre.Rock && m.Act.Members[m.Member] != BandRole.Drummer), m => Assert.NotNull(m.Look.Jacket));
    }

    [Fact]
    public void Every_dj_wears_headphones_and_stars_wear_shades()
    {
        Assert.All(EveryMember().Where(m => m.Act.Members[m.Member] == BandRole.Dj), m => Assert.NotNull(m.Look.Headphones));
        Assert.All(EveryMember().Where(m => m.Act.Rating >= 4), m => Assert.True(m.Look.Sunglasses));
    }

    [Fact]
    public void The_same_act_always_looks_the_same()
    {
        var first = EveryMember().Select(m => m.Look).ToList();
        var again = EveryMember().Select(m => m.Look).ToList();
        Assert.Equal(first, again);

        // And the members of a band do not all look alike.
        var band = ActGenerator.Of(Roster.Named("Bramble Hammer")!);
        var looks = band.Members.Select((_, i) => StarLooks.For(band, i)).ToList();
        Assert.True(looks.Select(l => (l.Skin, l.HairColour, l.Beard, l.Top)).Distinct().Count() > 1);
    }

    [Fact]
    public void The_crowd_never_dresses_like_a_star()
    {
        var crowd = Enumerable.Range(1, 2000).Select(i => LookGenerator.FestivalGoer(i)).ToList();
        Assert.All(crowd, l =>
        {
            Assert.Null(l.Jacket);
            Assert.Null(l.Sparkles);
            Assert.Null(l.Chain);
            Assert.Null(l.Headphones);
            Assert.False(l.Dungarees);
            Assert.DoesNotContain(l.Hair, new[] { HairStyle.Mane, HairStyle.Quiff });
        });
    }
}
