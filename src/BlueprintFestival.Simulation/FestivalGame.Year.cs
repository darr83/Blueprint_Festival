using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation;

/// <summary>Where the festival is in its year.</summary>
public enum Phase
{
    /// <summary>Choosing the ticket price and the acts. The clock is stopped.</summary>
    Plan,

    /// <summary>Building the site before the gates open. The clock is stopped.</summary>
    Build,

    /// <summary>The day itself.</summary>
    Day,

    /// <summary>The morning after: clearing the field.</summary>
    Tidy,

    /// <summary>The field is handed back and the year is added up.</summary>
    Results,

    /// <summary>The festival has run out of money for good.</summary>
    Closed,
}

/// <summary>What a year starts with, carried over from the one before.</summary>
public sealed record YearSetup(int Year = 1, int Licence = FestivalGame.FirstLicence, int Bank = FestivalGame.StartingMoney, bool BailedOut = false, int? LastHappiness = null);

/// <summary>A headliner who could be booked: how well known (2 to 5 stars) and what they charge.</summary>
public sealed record HeadlinerOffer(Act Act, int Stars, int Fee);

/// <summary>A piece of litter on the ground, in metres.</summary>
public readonly record struct LitterPiece(double X, double Y);

/// <summary>How the year went.</summary>
public sealed record YearResult(int Year, int Came, int Expected, int Happy, int WentHome, int In, int Out, int LitterCleared, int GrassWorn,
    string Verdict, int LicenceNow, int LicenceNext, bool Loan, bool Closed, int Treated = 0, int Untreated = 0, int TroubleSorted = 0, int TroubleIgnored = 0,
    int CrushInjuries = 0, int PulledOut = 0, bool SeriousIncident = false, int FieldRepair = 0, string Weather = "")
{
    public int Profit => In - Out;
}

public sealed partial class FestivalGame
{
    /// <summary>How many people the parish council lets a first festival have.</summary>
    public const int FirstLicence = 200;

    /// <summary>The biggest licence the village field can take. Bigger fields come later.</summary>
    public const int BiggestLicence = 400;

    /// <summary>What the parish council lends, once, to a festival that has run out of money.</summary>
    public const int LoanAmount = 1000;

    /// <summary>Happiness needed for the licence to grow.</summary>
    public const int HappyEnough = 70;

    /// <summary>The share of litter that must be cleared for the licence to grow.</summary>
    public const int TidyEnough = 90;

    /// <summary>The ticket prices on offer, in pounds.</summary>
    public static readonly IReadOnlyList<int> TicketPrices = new[] { 3, 5, 8, 12 };

    private static readonly double[] TurnoutAtPrice = { 1.0, 0.9, 0.65, 0.4 };

    /// <summary>Person-seconds on one patch of grass that wear it to bare earth.</summary>
    public const int WornOut = 60000;

    /// <summary>The side of one patch of grass in tiles (2 metres).</summary>
    public const int WearTiles = 4;

    private readonly List<HeadlinerOffer> _offers = new();
    private readonly List<LitterPiece> _litter = new();
    private int[] _wear = Array.Empty<int>();
    private bool _loanThisYear;
    private int _fieldRepair;

    /// <summary>True for a festival that runs through a whole year: plan, build, day, tidy, results.</summary>
    public bool Planned { get; }

    public Phase Phase { get; private set; } = Phase.Day;

    /// <summary>Which year of the festival this is, from 1.</summary>
    public int Year { get; } = 1;

    /// <summary>The most people the licence allows this year.</summary>
    public int Licence { get; } = FirstLicence;

    /// <summary>Whether the parish council has already lent money once.</summary>
    public bool BailedOut { get; private set; }

    /// <summary>How happy last year's crowd was, or null in the first year. Word gets round.</summary>
    public int? LastHappiness { get; }

    /// <summary>The date of the festival.</summary>
    public DateTime Day { get; }

    public int TicketPrice { get; private set; } = Catalogue.TicketPrice;

    public IReadOnlyList<HeadlinerOffer> Headliners => _offers;

    /// <summary>Which headliner is picked, counting from 0.</summary>
    public int HeadlinerPicked { get; private set; } = 1;

    /// <summary>What the six acts before the headliner cost between them.</summary>
    public int SupportFee => 6 * Scaled(40);

    /// <summary>What the acts cost in all with the headliner picked.</summary>
    public int ActFees => SupportFee + (_offers.Count > 0 ? _offers[HeadlinerPicked].Fee : 0);

    public bool CanBuild => Phase is Phase.Build or Phase.Day;

    public IReadOnlyList<LitterPiece> Litter => _litter;

    /// <summary>How much litter lay on the field when the tidying started.</summary>
    public int LitterAtStart { get; private set; }

    /// <summary>The share of the litter cleared so far, from 0 to 100.</summary>
    public int LitterCleared => LitterAtStart == 0 ? 100 : (int)Math.Floor(100.0 * (LitterAtStart - _litter.Count) / LitterAtStart);

    /// <summary>What the scouts ask to clear what is left.</summary>
    public int ScoutsFee => _litter.Count == 0 ? 0 : Math.Max(10, (int)Math.Ceiling(_litter.Count / 10.0) * 5);

    public int WearColumns => Field.Field.Width / WearTiles;

    public int WearRows => Field.Field.Height / WearTiles;

    /// <summary>How worn one patch of grass is, from 0 (fresh) to 1 (bare earth).</summary>
    public double WearAt(int column, int row) => Math.Min(1.0, (double)_wear[(row * WearColumns) + column] / WornOut);

    /// <summary>How worn the whole field is, from 0 to 100.</summary>
    public int GrassWorn => _wear.Length == 0 ? 0 : (int)Math.Round(100.0 * _wear.Sum(w => Math.Min(1.0, (double)w / WornOut)) / _wear.Length);

    /// <summary>The first year of a festival, opening on the planning screen.</summary>
    public static FestivalGame NewFestival(string? name = null) => new(name, null, null, new YearSetup());

    /// <summary>A year of a festival from what the year before left behind.</summary>
    public static FestivalGame StartYear(string? name, YearSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        return new FestivalGame(name, null, null, setup);
    }

    /// <summary>The third Saturday of July, a year later each year.</summary>
    public static DateTime FestivalDay(int year)
    {
        var first = new DateTime(2026 + year, 7, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return first.AddDays((((int)DayOfWeek.Saturday - (int)first.DayOfWeek + 7) % 7) + 14);
    }

    private int Scaled(int pounds) => pounds * Licence / FirstLicence;

    private static int FeeFor(int stars) => stars switch { <= 2 => 150, 3 => 300, 4 => 500, _ => 750 };

    private void MakeOffers()
    {
        var random = GameRandom.FromSeed(Seed, 21);
        var taken = new HashSet<string>(Show.Slots.Select(s => s.Act.Name));
        // There is always a cheap local act; one of the bigger names is busy that weekend.
        var stars = new List<int> { 2, 3, 4, 5 };
        stars.RemoveAt(1 + random.Next(3));
        foreach (var s in stars)
        {
            var act = ActGenerator.Make(random, true, taken) with { Draw = 0.55 + (0.08 * s) };
            _offers.Add(new HeadlinerOffer(act, s, Scaled(FeeFor(s))));
        }

        Crowd.Replan(ExpectedAt(TicketPrice));
    }

    /// <summary>How many people would probably come at a ticket price, with the headliner picked.</summary>
    public int ExpectedAt(int price)
    {
        var i = TicketPrices.ToList().IndexOf(price);
        if (i < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), price, "Not a ticket price on offer.");
        }

        var word = LastHappiness is { } h ? 0.6 + (0.5 * h / 100.0) : 1.0;
        var pull = _offers.Count > 0 ? 0.85 + (0.05 * _offers[HeadlinerPicked].Stars) : 1.0;
        return Math.Clamp((int)Math.Round(Licence * TurnoutAtPrice[i] * word * pull * ForecastPull), 10, Licence);
    }

    public void ChooseTicket(int price)
    {
        Need(Phase.Plan);
        ExpectedAt(price);
        TicketPrice = price;
        Crowd.Replan(ExpectedAt(TicketPrice));
    }

    public void ChooseHeadliner(int index)
    {
        Need(Phase.Plan);
        if (index < 0 || index >= _offers.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "No such headliner.");
        }

        HeadlinerPicked = index;
        Crowd.Replan(ExpectedAt(TicketPrice));
    }

    /// <summary>Books the acts and puts the tickets on sale. False if the bank cannot cover the fees.</summary>
    public bool ConfirmPlan()
    {
        Need(Phase.Plan);
        var ponchos = PonchosOrdered ? PonchoCost : 0;
        if (ActFees + ponchos > Money)
        {
            return false;
        }

        Spend("Act fees", ActFees);
        if (ponchos > 0)
        {
            Spend("Rain ponchos", ponchos);
        }

        ApplyPlan(Turnout());
        Phase = Phase.Build;
        return true;
    }

    /// <summary>The plan's guess with a little luck either way.</summary>
    private int Turnout() => Math.Clamp((int)Math.Round(ExpectedAt(TicketPrice) * GameRandom.FromSeed(Seed, 22).Between(0.93, 1.05)), 10, Licence);

    private void ApplyPlan(int people)
    {
        Show.SetHeadliner(_offers[HeadlinerPicked].Act);
        Crowd.Replan(people);
    }

    /// <summary>The build day is over: the clock starts and people begin to arrive.</summary>
    public void OpenGates()
    {
        Need(Phase.Build);
        Phase = Phase.Day;
    }

    private void Need(Phase phase)
    {
        if (Phase != phase)
        {
            throw new InvalidOperationException($"That is for the {phase} part of the year; this is {Phase}.");
        }
    }

    private void Tick(GameClock clock)
    {
        Crowd.Tick(clock.Now);
        TickStaff(clock.Now);
        TickSafety(clock.Now);
        TickWeather(clock.Now);
        if (clock.TickCount % 10 == 0)
        {
            Wear();
        }

        // The day is over when the last person has gone home, or at half past midnight whatever.
        if (Planned && Phase == Phase.Day && clock.Now >= Day + People.Crowd.EveryoneOut && (Crowd.OnSite == 0 || clock.Now >= Day.AddHours(24.5)))
        {
            EndDay();
        }
    }

    private void Wear()
    {
        var f = Field.Field;
        foreach (var party in Crowd.Parties)
        {
            if (!party.IsOnSite)
            {
                continue;
            }

            var tile = FieldMap.TileAt(party.X, party.Y);
            if (f.Contains(tile))
            {
                var column = Math.Min(WearColumns - 1, (tile.X - f.X) / WearTiles);
                var row = Math.Min(WearRows - 1, (tile.Y - f.Y) / WearTiles);
                var index = (row * WearColumns) + column;
                _wear[index] += (int)Math.Round(party.Members.Count * 10 * WearRate(index));
            }
        }
    }

    private void EndDay()
    {
        Phase = Phase.Tidy;
        LitterAtStart = _litter.Count;
        _incidents.Clear();
        foreach (var member in _staff)
        {
            (member.X, member.Y, member.Heading, member.State, member.Incident, member.Path) = (member.PostX, member.PostY, member.PostHeading, StaffState.AtPost, -1, null);
        }

        Clock.RestoreState(new ClockState(Day.AddDays(1).AddHours(6), Clock.TickCount, GameSpeed.Normal));
    }

    /// <summary>Picks up the litter within reach of a spot. Returns how many pieces.</summary>
    public int PickLitter(double x, double y, double radius = 1.5)
    {
        Need(Phase.Tidy);
        return _litter.RemoveAll(l => ((l.X - x) * (l.X - x)) + ((l.Y - y) * (l.Y - y)) <= radius * radius);
    }

    /// <summary>Pays the scouts to clear whatever is left. False if there is not the money.</summary>
    public bool PayScouts()
    {
        Need(Phase.Tidy);
        var fee = ScoutsFee;
        if (fee == 0 || fee > Money)
        {
            return false;
        }

        Spend("Scouts' litter pick", fee);
        _litter.Clear();
        return true;
    }

    /// <summary>Hands the field back to the farmer and adds the year up.</summary>
    public YearResult HandBack()
    {
        Need(Phase.Tidy);
        Phase = Phase.Results;
        _fieldRepair = FieldRepair;
        if (_fieldRepair > 0)
        {
            Spend("Field repairs", _fieldRepair);
        }

        if (Money < NeedsFor(Licence))
        {
            if (BailedOut)
            {
                Phase = Phase.Closed;
            }
            else
            {
                Money += LoanAmount;
                BailedOut = true;
                _loanThisYear = true;
            }
        }

        return Result!;
    }

    /// <summary>The least there must be in the bank to put on a year at a licence: the cheapest acts and a little over.</summary>
    public static int NeedsFor(int licence) => (((6 * 40) + 150) * licence / FirstLicence) + 200;

    /// <summary>How the year went, once the field is handed back.</summary>
    public YearResult? Result
    {
        get
        {
            if (Phase is not (Phase.Results or Phase.Closed))
            {
                return null;
            }

            var happy = Crowd.Happiness;
            var cleared = LitterCleared;
            var profit = TotalIncome - TotalSpending;
            var bigger = Math.Min(BiggestLicence, Licence * 2);
            var good = Phase == Phase.Results && !_loanThisYear && happy >= HappyEnough && cleared >= TidyEnough && profit >= 0 && Untreated == 0 && !SeriousIncident;
            var next = good && Money >= NeedsFor(bigger) ? bigger : Licence;
            var verdict =
                Phase == Phase.Closed ? "The money has run out a second time. The parish council will not lend again, and the festival is over." :
                _loanThisYear ? $"The bank is empty. The parish council lends £{LoanAmount:N0} to try once more. They will not do it twice." :
                SeriousIncident ? $"{CrushInjuries} people were hurt in a crush and the day had to be called off. The licence stays as it is until the crowd is kept safe." :
                Untreated > 0 ? $"{Untreated} {(Untreated == 1 ? "person was" : "people were")} taken ill and nobody came to help. The licence stays as it is until there is proper first aid." :
                happy < HappyEnough ? "People did not enjoy it enough. The licence stays as it is until they do." :
                cleared < TidyEnough ? "The farmer is not happy with the state of the field. The licence stays as it is." :
                profit < 0 ? "A good day, but it lost money. The licence stays as it is." :
                bigger > Licence && next == Licence ? "A good festival, but there is not yet the money in the bank for a bigger one." :
                next > Licence ? $"A good festival and a tidy field. The parish council raises the licence to {next:N0} people." :
                "A good festival and a tidy field. The village field can take no more people than this.";
            return new YearResult(Year, Crowd.Arrived, Crowd.Expected, happy, Crowd.WentHomeUnhappy, TotalIncome, TotalSpending, cleared, GrassWorn,
                verdict, Licence, next, _loanThisYear, Phase == Phase.Closed, Treated, Untreated, TroubleSorted, TroubleIgnored,
                CrushInjuries, PulledOut, SeriousIncident, _fieldRepair, Weather.Summary);
        }
    }

    /// <summary>Next year's festival: the bank and the licence carry over, the buildings do not.</summary>
    public FestivalGame NextYear()
    {
        Need(Phase.Results);
        var result = Result!;
        return StartYear(Name, new YearSetup(Year + 1, result.LicenceNext, Money, BailedOut, result.Happy));
    }

    private YearSave SaveYear() => new(Year, Licence, Phase, Day, TicketPrice, HeadlinerPicked, Crowd.Expected, BailedOut, _loanThisYear, LastHappiness,
        LitterAtStart, _litter.ToList(), _wear.ToArray())
    {
        FieldRepair = _fieldRepair,
    };

    private void RestoreYear(YearSave year)
    {
        TicketPrice = TicketPrices.Contains(year.TicketPrice) ? year.TicketPrice : Catalogue.TicketPrice;
        HeadlinerPicked = Math.Clamp(year.Headliner, 0, _offers.Count - 1);
        Phase = Enum.IsDefined(year.Phase) ? year.Phase : Phase.Plan;
        if (Phase != Phase.Plan)
        {
            ApplyPlan(Math.Clamp(year.Turnout, 1, Math.Max(1, Licence)));
        }
        else
        {
            Crowd.Replan(ExpectedAt(TicketPrice));
        }

        BailedOut = year.BailedOut;
        _loanThisYear = year.Loan;
        _fieldRepair = Math.Max(0, year.FieldRepair);
        LitterAtStart = Math.Max(0, year.LitterAtStart);
        _litter.AddRange(year.Litter ?? new());
        if (year.Wear is { } wear && wear.Length == _wear.Length)
        {
            _wear = wear.ToArray();
        }
    }
}

/// <summary>The festival year as written to a save file.</summary>
public sealed record YearSave(int Number, int Licence, Phase Phase, DateTime Day, int TicketPrice, int Headliner, int Turnout, bool BailedOut, bool Loan,
    int? LastHappiness, int LitterAtStart, List<LitterPiece>? Litter, int[]? Wear)
{
    /// <summary>What the farmer charged for worn-out grass, once the field is handed back.</summary>
    public int FieldRepair { get; init; }
}
