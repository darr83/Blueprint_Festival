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

    /// <summary>
    /// When each set on the second stage starts and how long it lasts. They overlap the main
    /// stage's sets, so the crowd has a choice and splits between the two.
    /// </summary>
    private static readonly (double Start, int Minutes)[] SecondTimes =
    {
        (13, 40), (14.25, 40), (15.5, 40), (16.75, 40), (18.5, 45), (20.25, 45),
    };

    /// <summary>Sets on the second stage are numbered from here, so a set's number says which stage it is on.</summary>
    public const int SecondBase = 100;

    /// <summary>How much of the crowd a second stage act draws beside the same act on the main stage.</summary>
    public const double SecondDraw = 0.55;

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

    /// <summary>The line-up for a second stage: six smaller local acts, none of them already on the main stage. It plays only once it is booked.</summary>
    public RunningOrder(SiteLayout site, int seed, DateTime festivalDay, RunningOrder main)
    {
        ArgumentNullException.ThrowIfNull(main);
        _site = site;
        Second = true;
        Booked = false;
        var random = GameRandom.FromSeed(seed, 8);
        var taken = new HashSet<string>(main.Slots.Select(s => s.Act.Name));
        for (var i = 0; i < SecondTimes.Length; i++)
        {
            var start = festivalDay.Date.AddHours(SecondTimes[i].Start);
            var act = ActGenerator.Make(random, false, taken);
            _slots.Add(new Slot(SecondBase + i, act with { Draw = act.Draw * SecondDraw }, start, start.AddMinutes(SecondTimes[i].Minutes), false));
        }
    }

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

    public IReadOnlyList<Slot> Slots => _slots;

    /// <summary>Books a different act for the last set of the night.</summary>
    public void SetHeadliner(Act act)
    {
        ArgumentNullException.ThrowIfNull(act);
        _slots[^1] = _slots[^1] with { Act = act };
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

    /// <summary>When the rest of the day's music was called off, if it was.</summary>
    public DateTime? CalledOff { get; private set; }

    /// <summary>Calls off the rest of the day's music from a moment on.</summary>
    public void CallOff(DateTime from) => CalledOff = from;

    /// <summary>The next set still to start, or null once the last has begun.</summary>
    public Slot? Next(DateTime now) => CalledOff is not null ? null : _slots.FirstOrDefault(s => s.Start > now);

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
