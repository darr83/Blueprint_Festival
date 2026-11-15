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

/// <summary>The sort of music an act plays.</summary>
public enum Genre
{
    Pop,
    Rock,
    Country,
    Electronic,
    Metal,
    HipHop,
}

/// <summary>
/// A made-up act. Its members are listed most important first, so a small stage takes the first
/// few. Draw is the share of the people on site who want to watch, from 0 to 1.
/// </summary>
public sealed record Act(string Name, ActKind Kind, IReadOnlyList<BandRole> Members, double Draw, int LookSeed)
{
    /// <summary>The sort of music they play.</summary>
    public Genre Genre { get; init; }

    /// <summary>How good they are, from one star (a local turn) to five (a household name).</summary>
    public int Rating { get; init; } = 1;

    /// <summary>How many people follow them.</summary>
    public int Fans { get; init; }

    /// <summary>Whether each member is a woman ('f') or a man ('m'), in the order of <see cref="Members"/>.</summary>
    public string Who { get; init; } = "";

    /// <summary>Whether a member is a woman. A member not listed in <see cref="Who"/> is settled by the act's look.</summary>
    public bool IsFemale(int member) =>
        member >= 0 && member < Who.Length ? Who[member] == 'f' : ((LookSeed / 7) + (member * 5)) % 2 == 0;

    /// <summary>The name the act was first drawn under, kept so the same festival always books the same acts.</summary>
    internal string Stem { get; init; } = "";

    /// <summary>The folder under the game's Music folder that holds this act's songs: the act's name.</summary>
    public string MusicFolder => Name;

    /// <summary>The members who fit on a stage with room for so many.</summary>
    public IReadOnlyList<BandRole> On(int room) => Members.Take(Math.Max(1, room)).ToList();
}

/// <summary>One of the 25 artists who play the festival. Headline artists are the ones offered to close the night.</summary>
/// <remarks>
/// Who says whether each member is a woman ('f') or a man ('m'), in the order the members are
/// listed: singer first, then guitarist and so on (see the line-ups in <see cref="ActGenerator"/>).
/// </remarks>
public sealed record Artist(string Name, ActKind Kind, Genre Genre, int Rating, int Fans, bool Headline = false, string Who = "");

/// <summary>
/// The artists. Every one is made up: none is a real act. Each has a kind (a solo guitarist, a
/// duo, a full band or a DJ), a genre, a rating out of five and a following. Their songs are
/// sound files in a folder of the same name under the game's Music folder.
/// </summary>
public static class Roster
{
    public static readonly IReadOnlyList<Artist> Artists = new[]
    {
        // Solo guitarists.
        new Artist("Marnie Thrupp", ActKind.Solo, Genre.Country, 1, 320, Who: "f"),
        new Artist("Odell Ashby", ActKind.Solo, Genre.Pop, 2, 4200, Who: "m"),
        new Artist("Wren Hallows", ActKind.Solo, Genre.Rock, 2, 6800, Who: "f"),
        new Artist("Tobias Pell", ActKind.Solo, Genre.Country, 3, 38000, Who: "m"),

        // Duos.
        new Artist("Salt and Bramble", ActKind.Duo, Genre.Pop, 1, 540, Who: "fm"),
        new Artist("Paper Magpies", ActKind.Duo, Genre.Pop, 2, 5100, Who: "ff"),
        new Artist("Ida and Corin", ActKind.Duo, Genre.Country, 2, 2900, Who: "fm"),
        new Artist("Crooked Lantern", ActKind.Duo, Genre.HipHop, 3, 52000, Who: "mm"),
        new Artist("Tin Foxgloves", ActKind.Duo, Genre.Rock, 3, 27000, Who: "fm"),

        // Full bands with a fiddle.
        new Artist("The Muddy Gateposts", ActKind.FolkBand, Genre.Country, 1, 780, Who: "mfmm"),
        new Artist("Hollow Skylarks", ActKind.FolkBand, Genre.Country, 2, 7400, Who: "fffm"),
        new Artist("The Wandering Wellies", ActKind.FolkBand, Genre.Country, 3, 44000, Who: "fmmf"),

        // Full bands.
        new Artist("The Copper Turnips", ActKind.Band, Genre.Rock, 3, 61000, Who: "mmfmm"),
        new Artist("Bramble Hammer", ActKind.Band, Genre.Metal, 4, 240000, Who: "mmmmm"),
        new Artist("The Velvet Otters", ActKind.Band, Genre.Pop, 5, 2600000, Who: "fmfmf"),
        new Artist("Little Ferrets", ActKind.Band, Genre.Rock, 2, 8900, Headline: true, Who: "fmmmf"),
        new Artist("Tin Thunder", ActKind.Band, Genre.Metal, 3, 33000, Headline: true, Who: "mmmfm"),
        new Artist("Hedgerow Howl", ActKind.Band, Genre.Metal, 4, 310000, Headline: true, Who: "fmmmm"),
        new Artist("The Lantern Hearts", ActKind.Band, Genre.Pop, 5, 1900000, Headline: true, Who: "mfmff"),

        // DJs.
        new Artist("DJ Haybale", ActKind.Dj, Genre.Electronic, 2, 3600, Who: "f"),
        new Artist("DJ Weathervane", ActKind.Dj, Genre.HipHop, 4, 180000, Who: "m"),
        new Artist("DJ Tractor", ActKind.Dj, Genre.Electronic, 2, 9500, Headline: true, Who: "m"),
        new Artist("DJ Magpie", ActKind.Dj, Genre.HipHop, 3, 47000, Headline: true, Who: "f"),
        new Artist("DJ Foxglove", ActKind.Dj, Genre.Electronic, 4, 420000, Headline: true, Who: "f"),
        new Artist("DJ Otter", ActKind.Dj, Genre.Electronic, 5, 3100000, Headline: true, Who: "m"),
    };

    /// <summary>The artist of that name, or null.</summary>
    public static Artist? Named(string name) => Artists.FirstOrDefault(a => a.Name == name);

    /// <summary>What a genre is called.</summary>
    public static string NameOf(Genre genre) => genre == Genre.HipHop ? "Hip hop" : genre.ToString();

    /// <summary>A following in a few characters: 780, 7.4k, 2.6m.</summary>
    public static string FansText(int fans) => fans switch
    {
        >= 1000000 => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{fans / 1000000.0:0.#}m"),
        >= 10000 => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{fans / 1000}k"),
        >= 1000 => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{fans / 1000.0:0.#}k"),
        _ => fans.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };
}

/// <summary>
/// A sound file in an artist's music folder. Files are named with a number and the song's
/// title, such as "01 Long Way Home.mp3"; the number sets the order the songs are played in.
/// </summary>
public sealed record SongFile(string File, int Order, string Title)
{
    public static readonly IReadOnlyList<string> Endings = new[] { ".mp3", ".ogg", ".wav" };

    /// <summary>Reads a file name. Null if it is not a sound file.</summary>
    public static SongFile? Read(string file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var ending = Endings.FirstOrDefault(e => file.EndsWith(e, StringComparison.OrdinalIgnoreCase));
        if (ending is null)
        {
            return null;
        }

        var name = file[..^ending.Length].Trim();
        var digits = name.TakeWhile(char.IsDigit).Count();
        var order = digits is > 0 and < 6 ? int.Parse(name[..digits], System.Globalization.CultureInfo.InvariantCulture) : int.MaxValue;
        var title = name[digits..].TrimStart(' ', '-', '_', '.').Replace('_', ' ').Trim();
        return new SongFile(file, order, title.Length == 0 ? name : title);
    }

    /// <summary>The songs among some file names, in the order they are played: by number, then by name.</summary>
    public static IReadOnlyList<SongFile> InOrder(IEnumerable<string> files) =>
        files.Select(Read).Where(s => s is not null).Select(s => s!).OrderBy(s => s.Order).ThenBy(s => s.File, StringComparer.OrdinalIgnoreCase).ToList();
}

/// <summary>Books acts from the roster for a festival, the same ones every time for the same festival.</summary>
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

    /// <summary>One act from the roster. Headliners are bands or DJs where one is free, and draw most of the field.</summary>
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
        var look = random.Next(100000);

        // The bigger the pull, the bigger the name: support acts are one to three stars, a headliner four or five.
        var rating = headliner ? (draw < 0.9 ? 4 : 5) : 1 + Math.Min(2, (int)((draw - 0.4) / 0.1));
        return From(Pick(kind, rating, headline: false, taken, name), draw, look, name);
    }

    /// <summary>One of the headline artists with exactly so many stars, to be offered for the last set of the night.</summary>
    public static Act MakeOffer(GameRandom random, int stars, ISet<string> taken)
    {
        ArgumentNullException.ThrowIfNull(taken);
        var rolled = Make(random, true, new HashSet<string>(taken));
        taken.Add(rolled.Stem);
        return From(Pick(rolled.Kind, stars, headline: true, taken, rolled.Stem), rolled.Draw, rolled.LookSeed, rolled.Stem);
    }

    private static Act From(Artist artist, double draw, int look, string stem) =>
        new(artist.Name, artist.Kind, artist.Kind == ActKind.Solo ? new[] { BandRole.Guitarist } : LineUps[(int)artist.Kind], draw, look)
        {
            Genre = artist.Genre,
            Rating = artist.Rating,
            Fans = artist.Fans,
            Who = artist.Who,
            Stem = stem,
        };

    /// <summary>How much of the field an artist draws to the stage: more stars, more of it, and most of all as the last act of the night.</summary>
    public static double DrawOf(Artist artist, bool closing)
    {
        ArgumentNullException.ThrowIfNull(artist);
        return closing ? 0.55 + (0.08 * artist.Rating) : 0.35 + (0.1 * artist.Rating);
    }

    /// <summary>An artist from the roster as an act ready to play.</summary>
    public static Act Of(Artist artist, bool closing = false)
    {
        ArgumentNullException.ThrowIfNull(artist);
        return From(artist, DrawOf(artist, closing), Hash(artist.Name) % 100000, artist.Name);
    }

    /// <summary>Marks an act as booked, so nobody is booked twice in one festival.</summary>
    public static void Take(ISet<string> taken, Act act)
    {
        ArgumentNullException.ThrowIfNull(taken);
        ArgumentNullException.ThrowIfNull(act);
        taken.Add(act.Stem);
        taken.Add(Booked + act.Name);
    }

    private const string Booked = "#";

    /// <summary>
    /// The artist not yet booked who best fits: the right kind if there is one, then the nearest
    /// rating. Ties are settled by the name first drawn, so the same festival books the same artist.
    /// </summary>
    private static Artist Pick(ActKind kind, int rating, bool headline, ISet<string> taken, string stem)
    {
        var free = Roster.Artists.Where(a => !taken.Contains(Booked + a.Name)).ToList();
        var pool = free.Where(a => a.Headline == headline).ToList();
        if (pool.Count == 0)
        {
            pool = free.Count > 0 ? free : Roster.Artists.ToList();
        }

        // A support act is never a four or five star name, and the night is never closed by less.
        var fitting = pool.Where(a => headline ? a.Rating == rating : (a.Rating >= 4) == (rating >= 4)).ToList();
        if (fitting.Count > 0)
        {
            pool = fitting;
        }

        var tie = (uint)Hash(stem);
        var best = pool
            .OrderBy(a => a.Kind == kind ? 0 : 1)
            .ThenBy(a => Math.Abs(a.Rating - rating))
            .ThenBy(a => (uint)Hash(a.Name) ^ tie)
            .First();
        taken.Add(Booked + best.Name);
        return best;
    }

    private static int Hash(string name)
    {
        var hash = 2166136261u;
        foreach (var c in name)
        {
            hash = (hash ^ c) * 16777619u;
        }

        return (int)(hash & 0x7FFFFFFF);
    }
}
