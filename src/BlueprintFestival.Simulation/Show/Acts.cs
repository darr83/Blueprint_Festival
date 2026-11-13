using BlueprintFestival.Simulation.Core;

namespace BlueprintFestival.Simulation.Show;

/// <summary>What someone does in a band.</summary>
public enum BandRole
{
    Singer,
    Guitarist,
    Bassist,
    Drummer,
    Keys,
    Fiddler,
    Dj,
}

/// <summary>The sort of act.</summary>
public enum ActKind
{
    Solo,
    Duo,
    FolkBand,
    Band,
    Dj,
}

/// <summary>
/// A made-up act. Its members are listed most important first, so a small stage takes the first
/// few. Draw is the share of the people on site who want to watch, from 0 to 1.
/// </summary>
public sealed record Act(string Name, ActKind Kind, IReadOnlyList<BandRole> Members, double Draw, int LookSeed)
{
    /// <summary>
    /// The number the act's music is made from. It comes from the act's name alone, so the same
    /// act plays the same songs in every festival, every year and after every load.
    /// </summary>
    public int SongSeed => ActGenerator.SongSeedFor(Name);

    /// <summary>The act's own songs, in the order it plays them. Always the same for the same act.</summary>
    public IReadOnlyList<Song> Songs => ActGenerator.SongsFor(Name, Kind);

    /// <summary>The members who fit on a stage with room for so many.</summary>
    public IReadOnlyList<BandRole> On(int room) => Members.Take(Math.Max(1, room)).ToList();
}

/// <summary>
/// One of an act's songs. Seed is what the music is made from (tune, key and so on), Tempo is in
/// beats a minute. File is empty unless a recording is supplied for it later.
/// </summary>
public sealed record Song(string Title, int Seed, int Tempo, string File = "");

/// <summary>Makes up acts. Every name is built from two plain words; none is a real artist.</summary>
public static class ActGenerator
{
    private static readonly string[] First =
    {
        "Velvet", "Muddy", "Copper", "Paper", "Wandering", "Salt", "Hollow", "Crooked", "Little", "Lantern", "Bramble", "Tin",
    };

    private static readonly string[] Second =
    {
        "Otters", "Hedgerows", "Magpies", "Tractors", "Foxgloves", "Wellies", "Skylarks", "Ferrets", "Turnips", "Weathervanes", "Gateposts", "Haybales",
    };

    private static readonly string[] People =
    {
        "Marnie Thrupp", "Odell Ashby", "Wren Hallows", "Tobias Pell", "Ida Marrow", "Corin Vane", "Betsy Lark", "Jem Tolliver",
    };

    private static readonly BandRole[][] LineUps =
    {
        new[] { BandRole.Guitarist },
        new[] { BandRole.Singer, BandRole.Guitarist },
        new[] { BandRole.Singer, BandRole.Fiddler, BandRole.Guitarist, BandRole.Drummer },
        new[] { BandRole.Singer, BandRole.Guitarist, BandRole.Bassist, BandRole.Drummer, BandRole.Keys },
        new[] { BandRole.Dj },
    };

    private static readonly string[] SongStarts =
    {
        "Long Way", "Last Bus", "Rain On", "Dancing In", "Meet Me By", "Back To", "Lights Over", "Down By", "Waiting For", "Singing To", "Lost In", "Home From",
    };

    private static readonly string[] SongEnds =
    {
        "The Top Field", "Market Square", "The Old Barn", "Midsummer", "The River", "Harvest Moon", "The Night Train", "Bluebell Wood", "The Seaside", "Your Kitchen", "The Morning", "The Hill",
    };

    /// <summary>A number worked out from a name, the same on every computer and every run.</summary>
    public static int SongSeedFor(string name)
    {
        var hash = 2166136261u;
        foreach (var c in name)
        {
            hash = (hash ^ c) * 16777619u;
        }

        return (int)(hash & 0x7FFFFFFF);
    }

    /// <summary>The six songs an act plays, made from its name so they never change.</summary>
    public static IReadOnlyList<Song> SongsFor(string name, ActKind kind)
    {
        var random = GameRandom.FromSeed(SongSeedFor(name), 11);
        var (slow, fast) = kind switch
        {
            ActKind.Dj => (118, 132),
            ActKind.Band => (100, 150),
            ActKind.FolkBand => (90, 140),
            _ => (70, 115),
        };
        var songs = new List<Song>();
        var titles = new HashSet<string>();
        while (songs.Count < 6)
        {
            var title = $"{SongStarts[random.Next(SongStarts.Length)]} {SongEnds[random.Next(SongEnds.Length)]}";
            if (titles.Add(title))
            {
                songs.Add(new Song(title, random.Next(1000000), slow + random.Next(fast - slow + 1)));
            }
        }

        return songs;
    }

    /// <summary>One act. Headliners are bands or DJs and draw most of the field.</summary>
    public static Act Make(GameRandom random, bool headliner, ISet<string> taken)
    {
        var roll = random.NextDouble();
        var kind = headliner
            ? (roll < 0.7 ? ActKind.Band : ActKind.Dj)
            : roll < 0.18 ? ActKind.Solo : roll < 0.42 ? ActKind.Duo : roll < 0.68 ? ActKind.FolkBand : roll < 0.9 ? ActKind.Band : ActKind.Dj;

        string name;
        do
        {
            var a = First[random.Next(First.Length)];
            var b = Second[random.Next(Second.Length)];
            name = kind switch
            {
                ActKind.Solo => People[random.Next(People.Length)],
                ActKind.Duo => $"{a} & {b}",
                ActKind.Dj => $"DJ {b[..^1]}",
                ActKind.FolkBand => $"{a} {b}",
                _ => $"The {a} {b}",
            };
        }
        while (!taken.Add(name));

        var draw = headliner ? random.Between(0.85, 0.95) : random.Between(0.4, 0.7);
        return new Act(name, kind, LineUps[(int)kind], draw, random.Next(100000));
    }
}
