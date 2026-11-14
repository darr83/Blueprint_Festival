using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Show;

/// <summary>One set in the day: who plays, and from when to when.</summary>
public sealed record Slot(int Index, Act Act, DateTime Start, DateTime End, bool Headliner);

/// <summary>
/// The music on one stage. A stage has four sets, at two, four, six and eight o'clock, and any of
/// them may be left empty. An act plays for an hour and a half, and the stage then stands empty
/// for half an hour while the next act sets up and people go for a drink, some food or the toilet. It starts with acts made up from the festival's seed, and
/// the player books who they like. The acts play on the stage the player has put up; with no
/// stage there is no show.
/// </summary>
public sealed class RunningOrder
{
    /// <summary>How long an act plays for, in minutes.</summary>
    public const int SetMinutes = 90;

    /// <summary>How long the stage stands empty after a set before the next one starts, in minutes.</summary>
    public const int BreakMinutes = 30;

    /// <summary>When each set starts (hours after midnight) and how long it lasts (minutes). The same on every stage.</summary>
    private static readonly (double Start, int Minutes)[] Times =
    {
        (14, SetMinutes), (16, SetMinutes), (18, SetMinutes), (20, SetMinutes),
    };

    /// <summary>How many sets a stage has in the day.</summary>
    public static int SetsAStage => Times.Length;

    /// <summary>Sets on the second stage are numbered from here, so a set's number says which stage it is on.</summary>
    public const int SecondBase = 100;

    /// <summary>How much of the crowd a second stage act draws beside the same act on the main stage.</summary>
    public const double SecondDraw = 0.55;

    private readonly SiteLayout _site;
    private readonly List<Slot> _slots = new();

    private readonly DateTime _day;

    public RunningOrder(SiteLayout site, int seed, DateTime festivalDay)
    {
        _site = site;
        _day = festivalDay.Date;
        var random = GameRandom.FromSeed(seed, 7);
        var taken = new HashSet<string>();
        for (var i = 0; i < Times.Length; i++)
        {
            Put(i, ActGenerator.Make(random, i == Times.Length - 1, taken));
        }
    }

    /// <summary>The line-up for a second stage: four smaller local acts, none of them already on the main stage. It plays only once it is booked.</summary>
    public RunningOrder(SiteLayout site, int seed, DateTime festivalDay, RunningOrder main)
    {
        ArgumentNullException.ThrowIfNull(main);
        _site = site;
        _day = festivalDay.Date;
        Second = true;
        Booked = false;
        var random = GameRandom.FromSeed(seed, 8);
        var taken = new HashSet<string>();
        foreach (var slot in main.Slots)
        {
            ActGenerator.Take(taken, slot.Act);
        }

        for (var i = 0; i < Times.Length; i++)
        {
            var act = ActGenerator.Make(random, false, taken);
            Put(SecondBase + i, act with { Draw = act.Draw * SecondDraw });
        }
    }

    /// <summary>The number of this stage's first set.</summary>
    public int Base => Second ? SecondBase : 0;

    /// <summary>When a set starts and ends, by its place in the day (0 to 3).</summary>
    public (DateTime Start, DateTime End) TimeOf(int place)
    {
        var start = _day.AddHours(Times[place].Start);
        return (start, start.AddMinutes(Times[place].Minutes));
    }

    private void Put(int index, Act? act)
    {
        _slots.RemoveAll(s => s.Index == index);
        if (act is not null)
        {
            var (start, end) = TimeOf(index - Base);
            _slots.Add(new Slot(index, act, start, end, false));
        }

        // The last act of the night on the main stage is the headliner.
        _slots.Sort((a, b) => a.Index.CompareTo(b.Index));
        for (var i = 0; i < _slots.Count; i++)
        {
            _slots[i] = _slots[i] with { Headliner = !Second && i == _slots.Count - 1 };
        }
    }

    /// <summary>Books an act for a set, or with null leaves the set empty.</summary>
    public void Book(int index, Act? act)
    {
        if (index < Base || index >= Base + Times.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "No such set on this stage.");
        }

        Put(index, act);
    }

    /// <summary>Who is booked for a set, or null if it is empty.</summary>
    public Act? At(int index) => _slots.FirstOrDefault(s => s.Index == index)?.Act;

    /// <summary>True for the second stage's line-up.</summary>
    public bool Second { get; }

    /// <summary>True if the acts have been booked. The main stage's always are; the second stage's are a choice at planning.</summary>
    public bool Booked { get; set; } = true;

    /// <summary>
    /// The stages in the order the acts use them: the biggest is the main stage, the next biggest
    /// the second stage, and of two the same the one put up first.
    /// </summary>
    public static IReadOnlyList<PlacedStage> InOrder(SiteLayout site)
    {
        ArgumentNullException.ThrowIfNull(site);
        return site.Stages.Select((stage, built) => (stage, built)).OrderByDescending(s => Catalogue.Of(s.stage.Kind).Cost).ThenBy(s => s.built).Select(s => s.stage).ToList();
    }

    /// <summary>The sets that have an act booked, in the order they play.</summary>
    public IReadOnlyList<Slot> Slots => _slots;

    /// <summary>Books a different act for the last set of the night.</summary>
    public void SetHeadliner(Act act)
    {
        ArgumentNullException.ThrowIfNull(act);
        Put(Base + Times.Length - 1, act);
    }

    /// <summary>The stage the acts play on, or null if none has been put up.</summary>
    public PlacedStage? Stage
    {
        get
        {
            var place = Second ? 1 : 0;
            if (!Booked || _site.Stages.Count <= place)
            {
                return null;
            }

            return _site.Stages.Count == 1 ? _site.Stages[0] : InOrder(_site)[place];
        }
    }

    /// <summary>The set that is on right now, or null between sets or with no stage.</summary>
    public Slot? Playing(DateTime now) => Stage is null || now >= CalledOff ? null : _slots.FirstOrDefault(s => now >= s.Start && now < s.End);

    /// <summary>
    /// The set that has just finished, while its act is still on stage taking the applause (for
    /// <see cref="Performing.BowMinutes"/> after the end), or null. Not after the music was called off.
    /// </summary>
    public Slot? Bowing(DateTime now) =>
        Stage is null || CalledOff is not null ? null
        : _slots.FirstOrDefault(s => now >= s.End && now < s.End.AddMinutes(Performing.BowMinutes) && Playing(now) is null);

    /// <summary>True in the gap after a set has finished and before the next one starts.</summary>
    public bool Changeover(DateTime now) => Stage is not null && CalledOff is null && Playing(now) is null && Next(now) is not null && _slots.Any(s => s.End <= now);

    /// <summary>When the rest of the day's music was called off, if it was.</summary>
    public DateTime? CalledOff { get; private set; }

    /// <summary>Calls off the rest of the day's music from a moment on.</summary>
    public void CallOff(DateTime from) => CalledOff = from;

    /// <summary>The next set still to start, or null once the last has begun.</summary>
    public Slot? Next(DateTime now) => CalledOff is not null ? null : _slots.FirstOrDefault(s => s.Start > now);

    /// <summary>Who is on stage right now: the act's members who fit on the stage.</summary>
    public IReadOnlyList<BandRole> OnStage(DateTime now) =>
        Playing(now) is { } slot && Stage is { } stage ? slot.Act.On(Catalogue.Of(stage.Kind).Performers) : Array.Empty<BandRole>();

    /// <summary>The middle of the front edge of the stage in metres, and the way it faces.</summary>
    public static (double X, double Y, int DirX, int DirY) Front(PlacedStage stage) => SiteLayout.Front(stage.Rect, stage.Facing);
}
