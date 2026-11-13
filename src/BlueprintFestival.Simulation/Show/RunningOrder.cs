using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Show;

/// <summary>One set in the day: who plays, and from when to when.</summary>
public sealed record Slot(int Index, Act Act, DateTime Start, DateTime End, bool Headliner);

/// <summary>
/// The day's music. Seven acts are made up from the festival's seed and play one after another,
/// the headliner last. They play on the first stage the player has put up; with no stage there
/// is no show.
/// </summary>
public sealed class RunningOrder
{
    /// <summary>When each set starts (hours after midnight) and how long it lasts (minutes).</summary>
    private static readonly (double Start, int Minutes)[] Times =
    {
        (12, 45), (13.25, 45), (14.5, 45), (16, 45), (17.5, 60), (19.25, 60), (20.75, 75),
    };

    private readonly SiteLayout _site;
    private readonly List<Slot> _slots = new();

    public RunningOrder(SiteLayout site, int seed, DateTime festivalDay)
    {
        _site = site;
        var random = GameRandom.FromSeed(seed, 7);
        var taken = new HashSet<string>();
        for (var i = 0; i < Times.Length; i++)
        {
            var headliner = i == Times.Length - 1;
            var start = festivalDay.Date.AddHours(Times[i].Start);
            _slots.Add(new Slot(i, ActGenerator.Make(random, headliner, taken), start, start.AddMinutes(Times[i].Minutes), headliner));
        }
    }

    public IReadOnlyList<Slot> Slots => _slots;

    /// <summary>The stage the acts play on, or null if none has been put up.</summary>
    public PlacedStage? Stage => _site.Stages.Count > 0 ? _site.Stages[0] : null;

    /// <summary>The set that is on right now, or null between sets or with no stage.</summary>
    public Slot? Playing(DateTime now) => Stage is null ? null : _slots.FirstOrDefault(s => now >= s.Start && now < s.End);

    /// <summary>The next set still to start, or null once the last has begun.</summary>
    public Slot? Next(DateTime now) => _slots.FirstOrDefault(s => s.Start > now);

    /// <summary>Who is on stage right now: the act's members who fit on the stage.</summary>
    public IReadOnlyList<BandRole> OnStage(DateTime now) =>
        Playing(now) is { } slot && Stage is { } stage ? slot.Act.On(Catalogue.Of(stage.Kind).Performers) : Array.Empty<BandRole>();

    /// <summary>The song being played right now: the act works through its songs in order, each the same length.</summary>
    public Song? SongNow(DateTime now)
    {
        if (Playing(now) is not { } slot)
        {
            return null;
        }

        var songs = slot.Act.Songs;
        var through = (now - slot.Start).TotalSeconds / (slot.End - slot.Start).TotalSeconds;
        return songs[Math.Min(songs.Count - 1, (int)(through * songs.Count))];
    }

    /// <summary>The middle of the front edge of the stage in metres, and the way it faces.</summary>
    public static (double X, double Y, int DirX, int DirY) Front(PlacedStage stage) => SiteLayout.Front(stage.Rect, stage.Facing);
}
