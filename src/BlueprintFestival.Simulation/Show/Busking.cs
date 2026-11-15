using BlueprintFestival.Simulation.Site;

namespace BlueprintFestival.Simulation.Show;

/// <summary>
/// The entertainers: a guitarist, a pianist, a drummer and a DJ the player hires like staff and
/// puts in a camping or food field. Each plays 45-minute sets starting on the hours the player
/// picks, to their own songs from a folder of their own under Music, and people walking past stop
/// to listen for five, ten or fifteen minutes. Now and then one draws a proper crowd.
/// </summary>
public static class Busking
{
    /// <summary>The kinds of staff who are entertainers, in the order they are offered.</summary>
    public static readonly IReadOnlyList<StaffKind> Kinds = new[] { StaffKind.Guitarist, StaffKind.Pianist, StaffKind.Drummer, StaffKind.Dj };

    /// <summary>True for an entertainer rather than someone who works the site.</summary>
    public static bool IsEntertainer(StaffKind kind) => kind is StaffKind.Guitarist or StaffKind.Pianist or StaffKind.Drummer or StaffKind.Dj;

    /// <summary>The first hour a set can start.</summary>
    public const int FirstHour = 10;

    /// <summary>The last hour a set can start.</summary>
    public const int LastHour = 22;

    /// <summary>How long each set lasts, in minutes. The rest of the hour they have a break.</summary>
    public const int SetMinutes = 45;

    /// <summary>The sets a newly hired entertainer plays until the player changes them.</summary>
    public static readonly IReadOnlyList<int> UsualHours = new[] { 12, 14, 16, 18 };

    /// <summary>
    /// The folder under Music that holds an entertainer's songs: Music/Entertainers/Guitarist and so on.
    /// </summary>
    public static string MusicFolder(StaffKind kind) => $"Entertainers/{FolderName(kind)}";

    /// <summary>The name of an entertainer's own music folder.</summary>
    public static string FolderName(StaffKind kind) => kind switch
    {
        StaffKind.Guitarist => "Guitarist",
        StaffKind.Pianist => "Pianist",
        StaffKind.Drummer => "Drummer",
        StaffKind.Dj => "DJ",
        _ => kind.ToString(),
    };

    /// <summary>What an entertainer plays as, for drawing them and their moves.</summary>
    public static BandRole RoleOf(StaffKind kind) => kind switch
    {
        StaffKind.Pianist => BandRole.Keys,
        StaffKind.Drummer => BandRole.Drummer,
        StaffKind.Dj => BandRole.Dj,
        _ => BandRole.Guitarist,
    };

    /// <summary>The sort of music each plays, for the steady beat they keep with no songs and for the way they move.</summary>
    public static Genre GenreOf(StaffKind kind) => kind switch
    {
        StaffKind.Pianist => Genre.Pop,
        StaffKind.Drummer => Genre.Rock,
        StaffKind.Dj => Genre.Electronic,
        _ => Genre.Country,
    };

    /// <summary>How close people have to pass, in metres, to notice an entertainer and maybe stop.</summary>
    public const double NoticeReach = 14;

    /// <summary>How near to an entertainer people stand to watch, in metres: no nearer than this...</summary>
    public const double NearestWatch = 2.6;

    /// <summary>...and, with a big crowd round them, no further than this.</summary>
    public const double FurthestWatch = 9;

    /// <summary>How long people stop to listen, in minutes: one of these, picked at random.</summary>
    public static readonly IReadOnlyList<int> WatchMinutes = new[] { 5, 10, 15 };

    /// <summary>The chance someone walking past stops, on an ordinary set.</summary>
    public const double StopChance = 0.18;

    /// <summary>The chance on a set that has caught on and draws a crowd.</summary>
    public const double CrowdStopChance = 0.5;

    /// <summary>One set in this many catches on and draws a proper crowd.</summary>
    public const int CrowdOneIn = 5;

    /// <summary>The most people stood round an entertainer on an ordinary set.</summary>
    public const int FewWatching = 8;

    /// <summary>The most on a set that has caught on.</summary>
    public const int CrowdWatching = 35;

    /// <summary>How much listening lifts the mood, an hour, the same as a two-star act on a stage.</summary>
    public const double MoodPerHour = 0.016;

    /// <summary>
    /// Whether a set has caught on and draws a crowd: one in <see cref="CrowdOneIn"/>, decided by
    /// the festival's seed, the entertainer and the hour, so the same day plays out the same way.
    /// </summary>
    public static bool CatchesOn(int seed, int entertainer, int hour)
    {
        var mix = unchecked(((uint)seed * 2654435761u) ^ ((uint)(entertainer + 1) * 40503u) ^ ((uint)hour * 2246822519u));
        mix ^= mix >> 15;
        mix *= 2246822519u;
        mix ^= mix >> 13;
        return mix % CrowdOneIn == 0;
    }

    /// <summary>The time a set starting on an hour ends.</summary>
    public static TimeSpan EndOf(int hour) => TimeSpan.FromHours(hour) + TimeSpan.FromMinutes(SetMinutes);
}
