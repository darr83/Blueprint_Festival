using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Show;

/// <summary>One set in the day: who plays, and from when to when.</summary>
public sealed record Slot(int Index, Act Act, DateTime Start, DateTime End, bool Headliner)
{
    /// <summary>True for a set that was ended early, so the act walked off before the end.</summary>
    public bool CutShort { get; init; }
}

/// <summary>
/// The music on one stage. A stage has four sets, at two, four, six and eight o'clock to begin
/// with, and any of them may be left empty. The player can move each set to start at any hour from
/// midday to nine in the evening, so the stages (up to four) can start and finish at different times (or
/// overlap; that is up to the player). An act plays for an hour and a half, and the next set on
/// the same stage can start on the next hour after that at the earliest, so the stage stands empty
/// for at least half an hour while the next act sets up and people go for a drink, some food or the toilet. It starts with acts made up from the festival's seed, and
/// the player books who they like. The acts play on the stage the player has put up; with no
/// stage there is no show.
/// </summary>
public sealed class RunningOrder
{
    /// <summary>How long an act plays for, in minutes.</summary>
    public const int SetMinutes = 90;

    /// <summary>How long the stage stands empty after a set before the next one starts, in minutes.</summary>
    public const int BreakMinutes = 30;

    /// <summary>When each set starts to begin with, in hours after midnight.</summary>
    public static readonly IReadOnlyList<int> FirstHours = new[] { 14, 16, 18, 20 };

    /// <summary>The earliest a set can start: midday.</summary>
    public const int EarliestHour = 12;

    /// <summary>The latest a set can start: nine in the evening, so it ends at half past ten.</summary>
    public const int LatestHour = 21;

    /// <summary>
    /// The fewest hours from the start of one set to the start of the next on the same stage: an
    /// hour and a half of music, then the next hour.
    /// </summary>
    public const int HoursApart = 2;

    /// <summary>How many sets a stage has in the day.</summary>
    public static int SetsAStage => FirstHours.Count;

    private readonly int[] _hours = FirstHours.ToArray();

    /// <summary>Sets on the second stage are numbered from here (the third from 200, the fourth from 300), so a set's number says which stage it is on.</summary>
    public const int SecondBase = 100;

    /// <summary>How much of the crowd a second stage act draws beside the same act on the main stage.</summary>
    public const double SecondDraw = 0.55;

    private readonly SiteLayout _site;
    private readonly List<Slot> _slots = new();

    private DateTime _day;

    public RunningOrder(SiteLayout site, int seed, DateTime festivalDay)
    {
        _site = site;
        _day = festivalDay.Date;
        var random = GameRandom.FromSeed(seed, 7);
        var taken = new HashSet<string>();
        for (var i = 0; i < SetsAStage; i++)
        {
            Put(i, ActGenerator.Make(random, i == SetsAStage - 1, taken));
        }
    }

    /// <summary>The line-up for a second stage: four smaller local acts, none of them already on the main stage. It plays only once it is booked.</summary>
    public RunningOrder(SiteLayout site, int seed, DateTime festivalDay, RunningOrder main)
        : this(site, seed, festivalDay, new[] { main }, 1)
    {
    }

    /// <summary>
    /// The line-up for an extra stage (numbered 1 to 3 after the main stage): four smaller local
    /// acts, none of them already on an earlier stage. It plays only once it is booked.
    /// </summary>
    public RunningOrder(SiteLayout site, int seed, DateTime festivalDay, IEnumerable<RunningOrder> before, int number)
    {
        ArgumentNullException.ThrowIfNull(before);
        if (number < 1 || number >= MaxStages)
        {
            throw new ArgumentOutOfRangeException(nameof(number), number, "Extra stages are numbered 1 to 3.");
        }

        _site = site;
        _day = festivalDay.Date;
        Number = number;
        Booked = false;
        var random = GameRandom.FromSeed(seed, number == 1 ? 8 : 40 + number);
        var taken = new HashSet<string>();
        foreach (var slot in before.SelectMany(b => b.Slots))
        {
            ActGenerator.Take(taken, slot.Act);
        }

        for (var i = 0; i < SetsAStage; i++)
        {
            var act = ActGenerator.Make(random, false, taken);
            Put(Base + i, act with { Draw = act.Draw * SecondDraw });
        }
    }

    /// <summary>The most stages that can have music on them in a day: the main stage and three more.</summary>
    public const int MaxStages = 4;

    /// <summary>Which stage a set is on (0 for the main stage, 1 to 3 for the others), from the set's number.</summary>
    public static int StageOf(int set) => Math.Clamp(set / SecondBase, 0, MaxStages - 1);

    /// <summary>The number of a stage's first set, by the stage (0 for the main stage).</summary>
    public static int BaseOf(int stage) => stage * SecondBase;

    /// <summary>The stage's place in the day's order: 0 for the main stage, then 1, 2 and 3.</summary>
    public int Number { get; }

    /// <summary>The number of this stage's first set.</summary>
    public int Base => BaseOf(Number);

    /// <summary>When a set starts and ends, by its place in the day (0 to 3).</summary>
    public (DateTime Start, DateTime End) TimeOf(int place)
    {
        var start = _day.AddHours(_hours[place]);
        return (start, start.AddMinutes(SetMinutes));
    }

    /// <summary>The hour a set starts, by its place in the day (0 to 3).</summary>
    public int HourOf(int place) => _hours[place];

    /// <summary>The hour each set starts, in order.</summary>
    public IReadOnlyList<int> Hours => _hours;

    /// <summary>
    /// Where the sets would start if one were moved some hours earlier or later: the sets after it
    /// (or before it) are pushed along to keep them <see cref="HoursApart"/> apart. Null if that
    /// would take a set before midday or after nine.
    /// </summary>
    public static int[]? Moved(IReadOnlyList<int> hours, int place, int by)
    {
        ArgumentNullException.ThrowIfNull(hours);
        if (place < 0 || place >= hours.Count)
        {
            return null;
        }

        var moved = hours.ToArray();
        moved[place] += by;
        for (var i = place + 1; i < moved.Length; i++)
        {
            moved[i] = Math.Max(moved[i], moved[i - 1] + HoursApart);
        }

        for (var i = place - 1; i >= 0; i--)
        {
            moved[i] = Math.Min(moved[i], moved[i + 1] - HoursApart);
        }

        return moved[0] < EarliestHour || moved[^1] > LatestHour ? null : moved;
    }

    /// <summary>Moves a set some hours earlier or later, pushing the others along as needed. False if there is no room.</summary>
    public bool Move(int index, int by)
    {
        if (Moved(_hours, index - Base, by) is not { } moved)
        {
            return false;
        }

        SetHours(moved);
        return true;
    }

    /// <summary>Sets when every set starts, as a save has it. False (and nothing changes) if the hours do not fit the rules.</summary>
    public bool SetHours(IReadOnlyList<int> hours)
    {
        ArgumentNullException.ThrowIfNull(hours);
        if (hours.Count != SetsAStage || hours[0] < EarliestHour || hours[^1] > LatestHour
            || Enumerable.Range(1, hours.Count - 1).Any(i => hours[i] - hours[i - 1] < HoursApart))
        {
            return false;
        }

        for (var i = 0; i < _hours.Length; i++)
        {
            _hours[i] = hours[i];
        }

        for (var i = 0; i < _slots.Count; i++)
        {
            var (start, end) = TimeOf(_slots[i].Index - Base);
            _slots[i] = _slots[i] with { Start = start, End = end };
        }

        return true;
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

    /// <summary>
    /// Moves the line-up on to another day of a festival of more than one day: every set keeps its
    /// hour, the stage is no longer called off, and nothing has been cut short yet.
    /// </summary>
    public void MoveTo(DateTime day)
    {
        _day = day.Date;
        CalledOff = null;
        for (var i = 0; i < _slots.Count; i++)
        {
            var (start, end) = TimeOf(_slots[i].Index - Base);
            _slots[i] = _slots[i] with { Start = start, End = end, CutShort = false };
        }
    }

    /// <summary>The day the line-up plays on.</summary>
    public DateTime Date => _day;

    /// <summary>Books an act for a set, or with null leaves the set empty.</summary>
    public void Book(int index, Act? act)
    {
        if (index < Base || index >= Base + SetsAStage)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "No such set on this stage.");
        }

        Put(index, act);
    }

    /// <summary>Who is booked for a set, or null if it is empty.</summary>
    public Act? At(int index) => _slots.FirstOrDefault(s => s.Index == index)?.Act;

    /// <summary>True for any stage but the main stage (the second, third or fourth).</summary>
    public bool Second => Number > 0;

    /// <summary>True if the acts have been booked. The main stage's always are; the second stage's are a choice at planning.</summary>
    public bool Booked { get; set; } = true;

    /// <summary>
    /// The stages in the order the acts use them: the biggest is the main stage, the next biggest
    /// the second stage, and so on, and of two the same the one put up first.
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
        Put(Base + SetsAStage - 1, act);
    }

    /// <summary>The stage the acts play on, or null if none has been put up.</summary>
    public PlacedStage? Stage
    {
        get
        {
            var place = Number;
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
    /// <see cref="Performing.BowMinutes"/> after the end), or null. Not after the music was called off, nor after a set that was cancelled.
    /// </summary>
    public Slot? Bowing(DateTime now) =>
        Stage is null || CalledOff is not null ? null
        : _slots.FirstOrDefault(s => !s.CutShort && now >= s.End && now < s.End.AddMinutes(Performing.BowMinutes) && Playing(now) is null);

    /// <summary>How many fans an act needs before people go and wait at the front ahead of the set.</summary>
    public const int BigFollowing = 20000;

    /// <summary>How many game minutes before a big act's set people start filling the front of the stage.</summary>
    public const int EarlyMinutes = 20;

    /// <summary>
    /// The next set, while the stage is empty and it starts within <see cref="EarlyMinutes"/>, if its act has
    /// <see cref="BigFollowing"/> fans or more: people are already making their way to the front for it. Otherwise null.
    /// </summary>
    public Slot? FillingUp(DateTime now) =>
        Stage is null || CalledOff is not null || Playing(now) is not null ? null
        : Next(now) is { } next && next.Act.Fans >= BigFollowing && next.Start - now <= TimeSpan.FromMinutes(EarlyMinutes) ? next : null;

    /// <summary>The set people are watching or waiting for: the one on now, or a big act's that is about to start.</summary>
    public Slot? Drawing(DateTime now) => Playing(now) ?? FillingUp(now);

    /// <summary>True in the gap after a set has finished and before the next one starts.</summary>
    public bool Changeover(DateTime now) => Stage is not null && CalledOff is null && Playing(now) is null && Next(now) is not null && _slots.Any(s => s.End <= now);

    /// <summary>When the rest of the day's music was called off, if it was.</summary>
    public DateTime? CalledOff { get; private set; }

    /// <summary>Calls off the rest of the day's music from a moment on.</summary>
    public void CallOff(DateTime from) => CalledOff = from;

    /// <summary>
    /// Ends a set early, from a moment on: the act walks off and the stage stands empty until the next
    /// set. A big act's set can also be called off while the crowd waits for it, before it starts.
    /// False if the set is not on then, nor about to start.
    /// </summary>
    public bool CutShort(int index, DateTime at)
    {
        var place = _slots.FindIndex(s => s.Index == index);
        if (place < 0 || at >= _slots[place].End)
        {
            return false;
        }

        if (at < _slots[place].Start)
        {
            if (FillingUp(at)?.Index != index)
            {
                return false;
            }

            // Called off before it began: the set never happens.
            _slots[place] = _slots[place] with { End = _slots[place].Start, CutShort = true };
            return true;
        }

        _slots[place] = _slots[place] with { End = at, CutShort = true };
        return true;
    }

    /// <summary>The next set still to start, or null once the last has begun.</summary>
    public Slot? Next(DateTime now) => CalledOff is not null ? null : _slots.FirstOrDefault(s => s.Start > now && !s.CutShort);

    /// <summary>Who is on stage right now: the act's members who fit on the stage.</summary>
    public IReadOnlyList<BandRole> OnStage(DateTime now) =>
        Playing(now) is { } slot && Stage is { } stage ? slot.Act.On(Catalogue.Of(stage.Kind).Performers) : Array.Empty<BandRole>();

    /// <summary>The middle of the front edge of the stage in metres, and the way it faces.</summary>
    public static (double X, double Y, int DirX, int DirY) Front(PlacedStage stage) => SiteLayout.Front(stage.Rect, stage.Facing);
}
