using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation;

/// <summary>A festival of more than one day as written to a save file.</summary>
public sealed record DaysSave(int DayNumber, int BillDay, List<List<string?>> Bills, List<int> Happy, List<int> Came)
{
    public int Unhappy { get; init; }

    public bool LitterCrew { get; init; }

    public bool Straw { get; init; }

    public double CampNight { get; init; } = -1;
}

/// <summary>
/// A long weekend. From a licence of 3,000 the festival runs for two days (Saturday and Sunday),
/// and from 5,000 for three (Friday to Sunday). One weekend ticket covers every day, and every day
/// has its own line-up on every stage. After each day but the last, the campers sleep in the
/// camping field and day visitors go home; in the morning the player chooses overnight jobs, can
/// change the site, and opens the gates again. How people felt, the litter and the mud carry over.
/// The big tidy-up comes after the last day.
/// </summary>
public sealed partial class FestivalGame
{
    /// <summary>The licence from which the festival runs for two days.</summary>
    public const int TwoDaysFrom = 3000;

    /// <summary>The licence from which the festival runs for three days.</summary>
    public const int ThreeDaysFrom = 5000;

    /// <summary>What a weekend ticket is worth next to buying every day on its own.</summary>
    public const double WeekendDeal = 0.85;

    /// <summary>
    /// What a set's number goes up by for each day after the first in <see cref="WholeBill"/>, so
    /// the same set on different days has a different number.
    /// </summary>
    public const int DaySets = 1000;

    /// <summary>When the gates open again on the second and third days.</summary>
    public static readonly TimeSpan GatesAgain = TimeSpan.FromHours(7.5);

    private readonly List<Act?[]> _bills = new();
    private readonly List<int> _dayHappy = new();
    private readonly List<int> _dayCame = new();
    private int _dayUnhappy;
    private double _campNight = -1;

    /// <summary>How many days a licence's festival runs for.</summary>
    public static int DaysFor(int licence) => licence >= ThreeDaysFrom ? 3 : licence >= TwoDaysFrom ? 2 : 1;

    /// <summary>How many days this year's festival runs for. A quick one-off festival is one day.</summary>
    public int Days => Planned ? _daysSet ?? DaysFor(Licence) : 1;

    private readonly int? _daysSet;

    /// <summary>Which day of the festival it is, counting from 0.</summary>
    public int DayNumber { get; private set; }

    /// <summary>The date of the festival's first day.</summary>
    public DateTime FirstDay => Day.AddDays(-DayNumber);

    /// <summary>True while there is another day of music after this one.</summary>
    public bool MoreDaysToCome => DayNumber < Days - 1;

    /// <summary>A day's name, counting from 0: "Friday", "Saturday" or "Sunday".</summary>
    public string DayName(int day) => FirstDay.AddDays(day).DayOfWeek.ToString();

    /// <summary>The day whose acts are in <see cref="Shows"/> just now, counting from 0. The planning screen shows one day at a time.</summary>
    public int BillDay { get; private set; }

    /// <summary>How happy people were at the end of each day that is over, oldest first.</summary>
    public IReadOnlyList<int> DayHappiness => _dayHappy;

    /// <summary>True once the litter crew has cleared the arena tonight.</summary>
    public bool LitterCrewHired { get; private set; }

    /// <summary>True once straw has been laid on the worn ground tonight.</summary>
    public bool StrawLaid { get; private set; }

    private static int BillSize => RunningOrder.MaxStages * RunningOrder.SetsAStage;

    /// <summary>Gives every day after the first its own line-up: empty for a bill the player books, otherwise some acts to start from.</summary>
    private void StartDays()
    {
        _bills.Clear();
        for (var d = 0; d < Days; d++)
        {
            _bills.Add(d == 0 ? TakeBill() : EmptyBill ? new Act?[BillSize] : SeededBill(d));
        }
    }

    private Act?[] TakeBill()
    {
        var bill = new Act?[BillSize];
        foreach (var show in Shows)
        {
            for (var i = 0; i < RunningOrder.SetsAStage; i++)
            {
                bill[(show.Number * RunningOrder.SetsAStage) + i] = show.At(show.Base + i);
            }
        }

        return bill;
    }

    private void PutBill(Act?[] bill)
    {
        foreach (var show in Shows)
        {
            for (var i = 0; i < RunningOrder.SetsAStage; i++)
            {
                show.Book(show.Base + i, bill[(show.Number * RunningOrder.SetsAStage) + i]);
            }
        }
    }

    /// <summary>A made-up line-up for a later day, for quick festivals and tests that do not book every act.</summary>
    private Act?[] SeededBill(int day)
    {
        var seed = Seed + (day * 7717);
        var shows = new List<RunningOrder> { new(Site, seed, Day) };
        for (var n = 1; n < RunningOrder.MaxStages; n++)
        {
            shows.Add(new RunningOrder(Site, seed, Day, shows, n));
        }

        var bill = new Act?[BillSize];
        foreach (var show in shows)
        {
            for (var i = 0; i < RunningOrder.SetsAStage; i++)
            {
                bill[(show.Number * RunningOrder.SetsAStage) + i] = show.At(show.Base + i);
            }
        }

        return bill;
    }

    /// <summary>
    /// Puts a day's acts on the stages, so the planning screen can show and book them. Every stage
    /// and set time is the same each day; only who plays changes. Not once the festival has begun.
    /// </summary>
    public void ShowDayBill(int day)
    {
        if (day < 0 || day >= Days || day == BillDay || Phase is not (Phase.Plan or Phase.Sell or Phase.Build))
        {
            return;
        }

        SwapBill(day);
        if (Phase == Phase.Plan)
        {
            Crowd.Replan(ExpectedAt(TicketPrice));
        }
    }

    private void SwapBill(int day)
    {
        if (day == BillDay || day >= _bills.Count)
        {
            return;
        }

        _bills[BillDay] = TakeBill();
        PutBill(_bills[day]);
        BillDay = day;
    }

    /// <summary>Does something to the line-up of every day not on the stages just now.</summary>
    private void EveryOtherBill(Action<Act?[]> change)
    {
        for (var d = 0; d < _bills.Count; d++)
        {
            if (d != BillDay)
            {
                change(_bills[d]);
            }
        }
    }

    /// <summary>Empties a stage's sets on every other day.</summary>
    private void ClearStageOtherDays(int stage) => EveryOtherBill(bill =>
    {
        for (var i = 0; i < RunningOrder.SetsAStage; i++)
        {
            bill[(stage * RunningOrder.SetsAStage) + i] = null;
        }
    });

    /// <summary>Moves the stages after one up a place on every other day, as <see cref="RemoveStage"/> does today.</summary>
    private void RemoveStageOtherDays(int stage) => EveryOtherBill(bill =>
    {
        for (var k = stage; k < RunningOrder.MaxStages; k++)
        {
            for (var i = 0; i < RunningOrder.SetsAStage; i++)
            {
                bill[(k * RunningOrder.SetsAStage) + i] = k + 1 < RunningOrder.MaxStages ? bill[((k + 1) * RunningOrder.SetsAStage) + i] : null;
            }
        }
    });

    /// <summary>The acts booked for a day, on the stages in use.</summary>
    public IEnumerable<Slot> BillOf(int day)
    {
        if (day == BillDay || day < 0 || day >= _bills.Count)
        {
            return day == BillDay ? LineUp : Array.Empty<Slot>();
        }

        var bill = _bills[day];
        var slots = new List<Slot>();
        foreach (var show in BookedShows)
        {
            for (var i = 0; i < RunningOrder.SetsAStage; i++)
            {
                if (bill[(show.Number * RunningOrder.SetsAStage) + i] is { } act)
                {
                    var (start, end) = show.TimeOf(i);
                    slots.Add(new Slot(show.Base + i, act, start.AddDays(day - BillDay), end.AddDays(day - BillDay), false));
                }
            }
        }

        return slots;
    }

    /// <summary>
    /// Every act on every day, on the stages in use. A set on a later day has <see cref="DaySets"/>
    /// added to its number for each day after the first. With one day it is the <see cref="LineUp"/>.
    /// </summary>
    public IEnumerable<Slot> WholeBill => Days == 1 ? LineUp : Enumerable.Range(0, Days).SelectMany(d => BillOf(d).Select(s => s with { Index = s.Index + (d * DaySets) }));

    /// <summary>The day a set in <see cref="WholeBill"/> is on.</summary>
    public static int DayOfSet(int set) => set / DaySets;

    /// <summary>The last act on the main stage on a day: that night's headliner.</summary>
    public Act? HeadlinerOf(int day)
    {
        if (day == BillDay)
        {
            return Show.Slots.LastOrDefault()?.Act;
        }

        if (day < 0 || day >= _bills.Count)
        {
            return null;
        }

        for (var i = RunningOrder.SetsAStage - 1; i >= 0; i--)
        {
            if (_bills[day][i] is { } act)
            {
                return act;
            }
        }

        return null;
    }

    /// <summary>The ticket price a set of acts is worth, day by day: each day as a day ticket, less for buying the weekend.</summary>
    private double FairOfDays(IReadOnlyList<Slot> sets)
    {
        var total = 0.0;
        for (var d = 0; d < Days; d++)
        {
            total += FairOfDay(sets.Where(s => DayOfSet(s.Index) == d).ToList(), HeadlinerOf(d));
        }

        return total * WeekendDeal;
    }

    /// <summary>How strongly a set of acts sells tickets over the days: the days taken together, so a weak day drags the weekend down.</summary>
    private double PullOfDays(IReadOnlyList<Slot> sets) =>
        Enumerable.Range(0, Days).Average(d => PullOfDay(sets.Where(s => DayOfSet(s.Index) == d).ToList(), HeadlinerOf(d)));

    /// <summary>The most a ticket can cost: more for every day it covers.</summary>
    public int TopTicketPrice => MaxTicketPrice * Days;

    /// <summary>The days with no act at the top of the main stage, by name.</summary>
    public IReadOnlyList<string> DaysWithNoHeadliner => Enumerable.Range(0, Days).Where(d => HeadlinerOf(d) is null).Select(DayName).ToList();

    /// <summary>Notes how a day went, as the music stops.</summary>
    private void RecordDay()
    {
        if (_dayHappy.Count > DayNumber)
        {
            return;
        }

        _dayHappy.Add(Crowd.Happiness);
        _dayCame.Add(Crowd.Arrived);
        _dayUnhappy += Crowd.WentHomeUnhappy;
    }

    /// <summary>How happy people were over the whole festival: each day's end counted the same.</summary>
    public int FestivalHappiness => Days == 1 || _dayHappy.Count == 0 ? Crowd.Happiness : (int)Math.Round(_dayHappy.Average());

    /// <summary>
    /// A day is over and another is to come: day visitors have gone home and campers are in their
    /// tents. The clock stops on the morning of the next day until the player opens the gates.
    /// </summary>
    private void BetweenDays(DateTime now)
    {
        Phase = Phase.Between;
        _incidents.Clear();
        EndTrouble();
        foreach (var member in _staff)
        {
            (member.X, member.Y, member.Heading, member.State, member.Incident, member.Path) = (member.PostX, member.PostY, member.PostHeading, StaffState.AtPost, -1, null);
        }

        LitterCrewHired = false;
        StrawLaid = false;
        var morning = Day.AddDays(1) + GatesAgain;
        Clock.RestoreState(new ClockState(now > morning ? now : morning, Clock.TickCount, GameSpeed.Normal));
    }

    /// <summary>What the overnight litter crew charges to clear the arena before the gates open. Nothing with no litter.</summary>
    public int LitterCrewCost => _litter.Count == 0 && _sick.Count == 0 ? 0 : Math.Max(40, (int)Math.Ceiling((_litter.Count + (_sick.Count * 4)) / 10.0) * 6);

    /// <summary>Pays the overnight litter crew to clear the arena. False if there is nothing to clear or not the money.</summary>
    public bool HireLitterCrew()
    {
        Need(Phase.Between);
        var cost = LitterCrewCost;
        if (cost == 0 || cost > Money)
        {
            return false;
        }

        Spend("Overnight litter crew", cost);
        _litter.Clear();
        _sick.Clear();
        LitterCrewHired = true;
        return true;
    }

    /// <summary>How many patches of ground are worn to mud, more than half worn through.</summary>
    public int WornPatches => _wear.Count(w => w >= WornOut / 2);

    /// <summary>What a bale of straw on every muddy patch costs.</summary>
    public const int StrawAPatch = 12;

    /// <summary>What straw on the mud costs tonight. Nothing with no mud.</summary>
    public int StrawCost => WornPatches == 0 ? 0 : Math.Max(50, WornPatches * StrawAPatch);

    /// <summary>Lays straw on the muddy ground: it saves the grass under it, and the next day nobody minds the mud as much. False if there is no mud or not the money.</summary>
    public bool LayStraw()
    {
        Need(Phase.Between);
        var cost = StrawCost;
        if (StrawLaid || cost == 0 || cost > Money)
        {
            return false;
        }

        Spend("Straw on the mud", cost);
        for (var i = 0; i < _wear.Length; i++)
        {
            if (_wear[i] >= WornOut / 2)
            {
                _wear[i] /= 2;
            }
        }

        StrawLaid = true;
        return true;
    }

    /// <summary>The mood lost the next day for litter still lying about when the gates open.</summary>
    public double LitterMood => Math.Min(0.08, 0.2 * _litter.Count / Math.Max(1.0, Crowd.Expected));

    /// <summary>The mood lost the next day for mud, much less with straw down.</summary>
    public double MudMood => Math.Min(0.06, 0.12 * GrassWorn / 100.0) * (StrawLaid ? 0.3 : 1);

    /// <summary>How the night in the camping field changes how people come back: campers are a share of everyone.</summary>
    public double CampNightMood => _campNight < 0 ? 0 : (_campNight - 0.7) * 0.15 * Math.Min(1, (double)Crowd.Campers / Math.Max(1, Crowd.Expected));

    /// <summary>
    /// How people feel as they come back the next day: half how they felt as the gates opened, half
    /// how yesterday ended, less for litter and mud, more or less for a good or bad night in the camp.
    /// </summary>
    public double NextDayMood
    {
        get
        {
            var start = SoldAhead ? ArrivalMood : People.Crowd.StartingMood;
            var yesterday = (_dayHappy.Count > 0 ? _dayHappy[^1] : 65) / 100.0;
            return Math.Clamp((0.5 * start) + (0.5 * yesterday) - LitterMood - MudMood + CampNightMood, 0.05, 1);
        }
    }

    /// <summary>What the staff cost for another day: everyone is paid again.</summary>
    public int DayWages => _staff.Sum(s => Catalogue.Of(s.Kind).Wage);

    /// <summary>
    /// Opens the gates on the next day: the next day's acts go on, the same people come again (the
    /// campers from their tents), the staff are paid for another day, and the clock runs from eight.
    /// </summary>
    public void OpenNextDay()
    {
        Need(Phase.Between);
        var mood = NextDayMood;
        DayNumber++;
        Day = Day.AddDays(1);
        foreach (var show in Shows)
        {
            show.MoveTo(Day);
        }

        SwapBill(DayNumber);
        Weather = DayWeather.ForYear(Seed + (DayNumber * 977), Year);
        RainChance = ChanceFor(Weather);
        Dress();
        _fireworkResults.Clear();
        _buskNoticed.Clear();
        _notes.Clear();
        _mood.Clear();
        ResetCampLife();
        _campNight = -1;
        Crowd.NewDay(Day, Crowd.Expected, CampingBooked);
        if (CampingBooked)
        {
            Crowd.PitchCamp();
        }

        Crowd.ArriveFeeling(mood);
        if (DayWages > 0)
        {
            Spend("Staff wages", DayWages);
        }

        PayForBuses();
        SendHomeActsWithNoStage();
        Crowd.GatesShut = true;
        GatesOpenedAt = null;
        Clock.RestoreState(new ClockState(Day + GameClock.DefaultStart.TimeOfDay, Clock.TickCount, GameSpeed.Normal));
        Phase = Phase.Day;
        Note(DayNoteKind.Gates, $"{DayName(DayNumber)}: day {DayNumber + 1} of {Days}", $"People come back {Math.Round(mood * 100):0}% happy", mood >= 0.6 ? DayTone.Good : DayTone.Plain);
    }

    private DaysSave? SaveDays() => Days == 1 && DayNumber == 0 ? null : new DaysSave(DayNumber, BillDay,
        _bills.Select((bill, d) => (d == BillDay ? TakeBill() : bill).Select(a => a?.Name).ToList()).ToList(), _dayHappy.ToList(), _dayCame.ToList())
    {
        Unhappy = _dayUnhappy,
        LitterCrew = LitterCrewHired,
        Straw = StrawLaid,
        CampNight = _campNight,
    };

    /// <summary>
    /// Brings back the days. The game was made for the day it was saved on, so the day before is
    /// worked out from that. A saved name that is not the made-up act already there is booked from the roster.
    /// </summary>
    private void RestoreDays(DaysSave? save)
    {
        if (save is null || _bills.Count == 0)
        {
            return;
        }

        DayNumber = Math.Clamp(save.DayNumber, 0, Days - 1);
        var shown = Math.Clamp(save.BillDay, 0, Days - 1);
        for (var d = 0; d < _bills.Count && d < save.Bills.Count; d++)
        {
            var names = save.Bills[d];
            for (var k = 0; k < BillSize && k < names.Count; k++)
            {
                if (_bills[d][k]?.Name != names[k])
                {
                    var stage = k / RunningOrder.SetsAStage;
                    var place = k % RunningOrder.SetsAStage;
                    _bills[d][k] = names[k] is { } name && Roster.Named(name) is { } artist ? ActFor(stage, place, artist) : null;
                }
            }
        }

        PutBill(_bills[shown]);
        BillDay = shown;
        _dayHappy.Clear();
        _dayHappy.AddRange(save.Happy ?? new());
        _dayCame.Clear();
        _dayCame.AddRange(save.Came ?? new());
        _dayUnhappy = save.Unhappy;
        LitterCrewHired = save.LitterCrew;
        StrawLaid = save.Straw;
        _campNight = save.CampNight;
        if (DayNumber > 0)
        {
            Crowd.TentsStayUp = CampingBooked;
        }
    }

    /// <summary>An artist as booked for a set: the last on the main stage draws most, and the other stages less.</summary>
    private static Act ActFor(int stage, int place, Artist artist)
    {
        var act = ActGenerator.Of(artist, stage == 0 && place == RunningOrder.SetsAStage - 1);
        return stage > 0 ? act with { Draw = act.Draw * RunningOrder.SecondDraw } : act;
    }
}
