namespace BlueprintFestival.Simulation.Show;

/// <summary>
/// How much an artist pulls a crowd, and how much each festival-goer cares for their sort of music.
/// An artist's pull comes half from their stars and half from their following, so a five star act
/// with millions of fans fills the field and a one star local turn plays to a few. Everyone on site
/// has a favourite sort of music, and some have one they cannot stand: they flock to and love the
/// first, and stay away from or sit through the second.
/// </summary>
public static class FanPull
{
    /// <summary>The following that counts as nobody yet, and the one that counts as everybody.</summary>
    public const int FewFans = 100;

    public const int AllTheFans = 3200000;

    /// <summary>An artist's pull from 0 (a nobody) to 1 (a household name): half stars, half following.</summary>
    public static double Of(int stars, int fans)
    {
        var fame = (Math.Log10(Math.Max(FewFans, fans)) - Math.Log10(FewFans)) / (Math.Log10(AllTheFans) - Math.Log10(FewFans));
        var rating = (Math.Clamp(stars, 1, 5) - 1) / 4.0;
        return Math.Clamp((0.5 * rating) + (0.5 * Math.Clamp(fame, 0, 1)), 0, 1);
    }

    public static double Of(Artist artist)
    {
        ArgumentNullException.ThrowIfNull(artist);
        return Of(artist.Rating, artist.Fans);
    }

    public static double Of(Act act)
    {
        ArgumentNullException.ThrowIfNull(act);
        return Of(act.Rating, act.Fans);
    }

    /// <summary>
    /// The share of the people on site who want to watch an act with this pull: about a third for a
    /// nobody up to three quarters for a household name, and nearly everyone for a household name closing the night.
    /// </summary>
    public static double Draw(double pull, bool closing) =>
        closing ? 0.6 + (0.37 * Math.Clamp(pull, 0, 1)) : 0.3 + (0.45 * Math.Clamp(pull, 0, 1));

    /// <summary>How big a crowd an artist pulls, in a few words.</summary>
    public static string Words(double pull) => pull switch
    {
        < 0.25 => "small crowd",
        < 0.5 => "fair crowd",
        < 0.75 => "big crowd",
        _ => "huge crowd",
    };

    /// <summary>How the sorts of music are liked across a crowd, out of 100: most like pop, fewest metal.</summary>
    private static readonly (Genre Genre, int Share)[] Favourites =
    {
        (Genre.Pop, 25), (Genre.Rock, 20), (Genre.Country, 18), (Genre.Electronic, 14), (Genre.HipHop, 13), (Genre.Metal, 10),
    };

    /// <summary>The share of people with a sort of music they cannot stand.</summary>
    public const double ShareWhoHateOne = 0.35;

    /// <summary>Someone's favourite sort of music and the one they cannot stand, if any. The same person always has the same taste.</summary>
    public static (Genre Favourite, Genre? Hates) TasteOf(int person)
    {
        var hash = Mix((uint)person);
        var roll = (int)(hash % 100);
        var favourite = Favourites[^1].Genre;
        foreach (var (genre, share) in Favourites)
        {
            if (roll < share)
            {
                favourite = genre;
                break;
            }

            roll -= share;
        }

        var hash2 = Mix(hash ^ 0x9E3779B9u);
        if ((hash2 % 1000) / 1000.0 >= ShareWhoHateOne)
        {
            return (favourite, null);
        }

        // The one they cannot stand is any other sort.
        var others = Enum.GetValues<Genre>().Where(g => g != favourite).ToArray();
        return (favourite, others[(int)((hash2 / 1000) % (uint)others.Length)]);
    }

    /// <summary>How keen someone is to go and watch an act of a sort: keener for their favourite, much less for one they cannot stand.</summary>
    public static double Keenness(int person, Genre genre)
    {
        var (favourite, hates) = TasteOf(person);
        return genre == favourite ? 1.5 : genre == hates ? 0.35 : 1;
    }

    /// <summary>How much someone enjoys an act of a sort, against an ordinary act: half as much again for their favourite, little for one they cannot stand.</summary>
    public static double Enjoyment(int person, Genre genre)
    {
        var (favourite, hates) = TasteOf(person);
        return genre == favourite ? 1.5 : genre == hates ? 0.25 : 1;
    }

    /// <summary>The chance someone goes to watch an act that draws so much of the field.</summary>
    public static double ChanceToWatch(int person, Genre genre, double draw) => Math.Clamp(draw * Keenness(person, genre), 0, 0.98);

    private static uint Mix(uint x)
    {
        x ^= x >> 16;
        x *= 0x7FEB352Du;
        x ^= x >> 15;
        x *= 0x846CA68Bu;
        x ^= x >> 16;
        return x;
    }
}
