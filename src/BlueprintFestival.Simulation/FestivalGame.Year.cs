using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Sales;
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

    /// <summary>January to June: the acts are booked and tickets are on sale, a month at a time. It comes between Plan and Build.</summary>
    Sell,
}

/// <summary>What a year starts with, carried over from the one before.</summary>
public sealed record YearSetup(int Year = 1, int Licence = FestivalGame.FirstLicence, int Bank = FestivalGame.StartingMoney, bool BailedOut = false, int? LastHappiness = null,
    IReadOnlyList<FieldChoice>? Fields = null);

/// <summary>A headliner who could be booked: how well known (2 to 5 stars) and what they charge.</summary>
public sealed record HeadlinerOffer(Act Act, int Stars, int Fee);

/// <summary>A piece of litter on the ground, in metres.</summary>
public readonly record struct LitterPiece(double X, double Y);

/// <summary>How the year went.</summary>
public sealed record YearResult(int Year, int Came, int Expected, int Happy, int WentHome, int In, int Out, int LitterCleared, int GrassWorn,
    string Verdict, int LicenceNow, int LicenceNext, bool Loan, bool Closed, int Treated = 0, int Untreated = 0, int TroubleSorted = 0, int TroubleIgnored = 0,
    int CrushInjuries = 0, int PulledOut = 0, bool SeriousIncident = false, int FieldRepair = 0, string Weather = "")
{
    /// <summary>How many times the music was stopped for an announcement.</summary>
    public int Announcements { get; init; }

    /// <summary>How many times security were sent into a crush.</summary>
    public int SecuritySent { get; init; }

    /// <summary>How many acts were cancelled because of a crush.</summary>
    public int ActsCancelled { get; init; }

    /// <summary>People hurt in a crush that was left alone.</summary>
    public int HurtLeftAlone { get; init; }

    public int Profit => In - Out;
}

public sealed partial class FestivalGame
{
    /// <summary>How many people the parish council lets a first festival have.</summary>
    public const int FirstLicence = 200;

    /// <summary>
    /// The licences a festival climbs through, one good year at a time. Up to 400 it stays on the
    /// village field; then it moves to Home Farm meadow, and from 3,000 to the showground.
    /// </summary>
    public static readonly IReadOnlyList<int> Licences = new[] { 200, 400, 800, 1500, 3000, 5000 };

    /// <summary>The biggest licence there is.</summary>
    public const int BiggestLicence = 5000;

    /// <summary>The licence a good year at this one earns: the next step up, or the same at the top.</summary>
    public static int LicenceAfter(int licence) => Licences.FirstOrDefault(l => l > licence, BiggestLicence);

    /// <summary>The field this year's festival is on.</summary>
    public FieldSize Ground => Planned ? FieldMap.SizeFor(Licence) : FieldMap.Sizes[0];

    /// <summary>
    /// What the field and the licence cost for the year. The village field is lent for nothing;
    /// a bigger one is rented, at £1 for each person the licence allows.
    /// </summary>
    public int FieldRent => Plan?.Rent ?? RentFor(Licence);

    /// <summary>The fields rented for this year, or null for a year on the one field its licence used to be given.</summary>
    public FieldPlan? Plan { get; private set; }

    /// <summary>The most tickets there are to sell: the licence, or the room in the music fields if that is less.</summary>
    public int OnSale => Plan is null ? Licence : Math.Max(10, Math.Min(Math.Min(Licence, Plan.MusicRoom), Math.Max(CanGetHere, TicketsSold)));

    /// <summary>
    /// The same January with different fields rented: a fresh year on the new site, with the same
    /// bank, licence and history. Null if the plan cannot be booked; <see cref="FieldPlan.Problems"/> says why.
    /// </summary>
    public FestivalGame? WithFields(FieldPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        Need(Phase.Plan);
        return plan.Problems(Licence, Year).Count > 0
            ? null
            : StartYear(Name, new YearSetup(Year, Licence, Money, BailedOut, LastHappiness, plan.Save())).TravellingLike(this);
    }

    /// <summary>Rent and licence fee for a year at a licence: nothing on the village field, then £1 a head.</summary>
    public static int RentFor(int licence) => licence <= FieldMap.Sizes[0].Holds ? 0 : licence;

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

    /// <summary>What an act with so many stars charges at a village festival. It goes up with the size of the licence.</summary>
    private static int BaseFee(int stars) => stars switch { <= 1 => 25, 2 => 45, 3 => 110, 4 => 350, _ => 750 };

    /// <summary>What an artist charges to play this festival.</summary>
    public int FeeOf(int stars) => Scaled(BaseFee(stars));

    /// <summary>Every set that has an act booked, on the stages in use.</summary>
    public IEnumerable<Slot> LineUp => SecondShow.Booked ? Show.Slots.Concat(SecondShow.Slots) : Show.Slots;

    /// <summary>What all the acts booked cost.</summary>
    public int ActFees => LineUp.Sum(s => FeeOf(s.Act.Rating));

    /// <summary>What the second stage's acts cost, booked or not.</summary>
    public int SecondStageFee => SecondShow.Slots.Sum(s => FeeOf(s.Act.Rating));

    /// <summary>True if acts are booked for a second stage.</summary>
    public bool SecondStageBooked => SecondShow.Booked;

    /// <summary>Books or cancels the second stage's acts. Its four sets keep whoever was in them.</summary>
    public void BookSecondStage(bool book)
    {
        Need(Phase.Plan);
        SecondShow.Booked = book;
    }

    /// <summary>Adds a second stage with every set empty, for the player to fill.</summary>
    public void AddEmptySecondStage()
    {
        Need(Phase.Plan);
        for (var i = 0; i < RunningOrder.SetsAStage; i++)
        {
            SecondShow.Book(RunningOrder.SecondBase + i, null);
        }

        SecondShow.Booked = true;
    }

    private RunningOrder StageOf(int set) => set >= RunningOrder.SecondBase ? SecondShow : Show;

    /// <summary>The set an artist is booked for, or null if they are not playing.</summary>
    public int? SetOf(string artist) => LineUp.FirstOrDefault(s => s.Act.Name == artist)?.Index;

    /// <summary>
    /// Books an artist from the roster for a set, or with null leaves the set empty. An artist
    /// who is already playing another set swaps places with whoever was here. False if there is no
    /// such artist or set.
    /// </summary>
    public bool BookAct(int set, string? artist)
    {
        Need(Phase.Plan);
        var place = set - StageOf(set).Base;
        if (place < 0 || place >= RunningOrder.SetsAStage || (set >= RunningOrder.SecondBase && !SecondShow.Booked))
        {
            return false;
        }

        if (artist is null)
        {
            StageOf(set).Book(set, null);
        }
        else if (Roster.Named(artist) is { } found)
        {
            var here = StageOf(set).At(set);
            if (SetOf(artist) is { } other && other != set)
            {
                Place(other, here is null ? null : Roster.Named(here.Name));
            }
            else
            {
                // Whoever the seed booked for the stage not in use must not be booked twice.
                foreach (var show in new[] { Show, SecondShow })
                {
                    foreach (var slot in show.Slots.Where(s => s.Act.Name == artist && s.Index != set).ToList())
                    {
                        show.Book(slot.Index, null);
                    }
                }
            }

            Place(set, found);
        }
        else
        {
            return false;
        }

        Crowd.Replan(ExpectedAt(TicketPrice));
        return true;
    }

    /// <summary>
    /// Moves a set some hours earlier or later (any hour from midday to nine in the evening). The
    /// other sets on the stage are pushed along to stay two hours apart. False if there is no room.
    /// </summary>
    public bool MoveSet(int set, int hours)
    {
        Need(Phase.Plan);
        if (set >= RunningOrder.SecondBase && !SecondShow.Booked)
        {
            return false;
        }

        if (!StageOf(set).Move(set, hours))
        {
            return false;
        }

        Crowd.Replan(ExpectedAt(TicketPrice));
        return true;
    }

    /// <summary>True if a set can be moved some hours earlier or later.</summary>
    public bool CanMoveSet(int set, int hours) =>
        Phase == Phase.Plan && (set < RunningOrder.SecondBase || SecondShow.Booked)
        && RunningOrder.Moved(StageOf(set).Hours, set - StageOf(set).Base, hours) is not null;

    private void Place(int set, Artist? artist)
    {
        var show = StageOf(set);
        if (artist is null)
        {
            show.Book(set, null);
            return;
        }

        var closing = !show.Second && set == RunningOrder.SetsAStage - 1;
        var act = ActGenerator.Of(artist, closing);
        show.Book(set, show.Second ? act with { Draw = act.Draw * RunningOrder.SecondDraw } : act);
    }

    /// <summary>How many stars the last act on the main stage has, or none with an empty stage.</summary>
    public int HeadlineStars => Show.Slots.LastOrDefault()?.Act.Rating ?? 0;

    /// <summary>What the farmer charges for the camping field, with its toilets and taps.</summary>
    public const int CampingCost = 2500;

    /// <summary>What a pitch costs at the start, before the player changes it.</summary>
    public const int CampingFee = 4;

    /// <summary>The dearest a pitch can be.</summary>
    public const int DearestPitch = 30;

    /// <summary>What each camper pays for their pitch, on top of their ticket. The player sets it, any time before the gates open.</summary>
    public int CampingPrice { get; private set; } = CampingFee;

    /// <summary>The share of the crowd that brings a tent at this pitch price: half when it is free, fewer as it gets dearer.</summary>
    public double CamperShare => People.Crowd.CamperShareAt(CampingPrice);

    /// <summary>Sets what a pitch costs, any time before the gates open. Cheap pitches fill the camping field; dear ones leave it emptier.</summary>
    public void SetCampingPrice(int pounds)
    {
        if (Phase is not (Phase.Plan or Phase.Sell or Phase.Build))
        {
            throw new InvalidOperationException("The pitch price is set before the gates open.");
        }

        CampingPrice = Math.Clamp(pounds, 0, DearestPitch);
        Crowd.CamperShare = CamperShare;
        if (CampingBooked && Phase == Phase.Build && Plan is null)
        {
            // On the old single field the tents were pitched when the plan was confirmed: pitch them again.
            Crowd.PitchCamp();
        }
    }

    /// <summary>The smallest licence at which camping is on offer.</summary>
    public const int CampingFrom = 5000;

    /// <summary>True if this year's field has a camping field next door and the licence is big enough to use it.</summary>
    public bool CampingOffered => Planned && Plan is null && Licence >= CampingFrom && Field.HasCamp;

    /// <summary>True if the camping field has been taken for this year.</summary>
    public bool CampingBooked { get; private set; }

    /// <summary>About how many people will camp if camping is booked.</summary>
    public int CampersExpected => (int)Math.Round(ExpectedAt(TicketPrice) * CamperShare);

    /// <summary>Takes the camping field for the night, or gives it up. Only while planning, and only when it is on offer.</summary>
    public void BookCamping(bool book)
    {
        Need(Phase.Plan);
        CampingBooked = book && CampingOffered;
    }

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

    private void MakeOffers()
    {
        var random = GameRandom.FromSeed(Seed, 21);
        var taken = new HashSet<string>();
        foreach (var slot in Show.Slots.Concat(SecondShow.Slots))
        {
            ActGenerator.Take(taken, slot.Act);
        }

        // There is always a cheap local act; one of the bigger names is busy that weekend.
        var stars = new List<int> { 2, 3, 4, 5 };
        stars.RemoveAt(1 + random.Next(3));
        foreach (var s in stars)
        {
            var act = ActGenerator.MakeOffer(random, s, taken) with { Draw = 0.55 + (0.08 * s) };
            _offers.Add(new HeadlinerOffer(act, s, FeeOf(s)));
        }

        Show.SetHeadliner(_offers[HeadlinerPicked].Act);

        Crowd.Replan(ExpectedAt(TicketPrice));
    }

    /// <summary>The share of the licence that comes at a price, before the line-up and the weather.</summary>
    private static double TurnoutShare(int price)
    {
        var prices = TicketPrices;
        if (price <= prices[0])
        {
            return TurnoutAtPrice[0];
        }

        for (var i = 1; i < prices.Count; i++)
        {
            if (price <= prices[i])
            {
                var along = (double)(price - prices[i - 1]) / (prices[i] - prices[i - 1]);
                return TurnoutAtPrice[i - 1] + ((TurnoutAtPrice[i] - TurnoutAtPrice[i - 1]) * along);
            }
        }

        return Math.Max(0.05, TurnoutAtPrice[^1] - (0.05 * (price - prices[^1])));
    }

    /// <summary>How many people would probably come at a ticket price, with the line-up as booked, if the whole year sold in one go.</summary>
    public int ExpectedAt(int price)
    {
        if (price < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(price), price, "Not a ticket price.");
        }

        var word = LastHappiness is { } h ? 0.6 + (0.5 * h / 100.0) : 1.0;
        var pull = _offers.Count > 0 ? 0.85 + (0.05 * HeadlineStars) : 1.0;
        return Math.Clamp((int)Math.Round(OnSale * TurnoutShare(price) * word * pull * ForecastPull), 10, OnSale);
    }

    public void ChooseTicket(int price)
    {
        Need(Phase.Plan);
        if (!TicketPrices.Contains(price))
        {
            throw new ArgumentOutOfRangeException(nameof(price), price, "Not a ticket price on offer.");
        }

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
        Show.SetHeadliner(_offers[index].Act);
        Crowd.Replan(ExpectedAt(TicketPrice));
    }

    /// <summary>What has to be paid on the day the plan is agreed: the acts, the field, and anything ordered.</summary>
    public int PlanCost => ActFees + FieldRent + (CampingBooked && Plan is null ? CampingCost : 0);

    private bool PayForPlan()
    {
        if (PlanCost > Money)
        {
            return false;
        }

        Spend("Act fees", ActFees);
        if (FieldRent > 0)
        {
            Spend("Field rent and licence", FieldRent);
        }

        if (CampingBooked && Plan is null)
        {
            Spend("Camping field", CampingCost);
        }

        return true;
    }

    /// <summary>
    /// Books the acts and sells the whole year's tickets in one go, to be paid for at the gate:
    /// the quick way to a build day. False if the bank cannot cover the fees.
    /// </summary>
    public bool ConfirmPlan()
    {
        Need(Phase.Plan);
        if (!PayForPlan())
        {
            return false;
        }

        ApplyPlan(Turnout());
        Phase = Phase.Build;
        return true;
    }

    /// <summary>The plan's guess with a little luck either way.</summary>
    private int Turnout() => Math.Clamp((int)Math.Round(ExpectedAt(TicketPrice) * GameRandom.FromSeed(Seed, 22).Between(0.93, 1.05)), 10, OnSale);

    private void ApplyPlan(int people)
    {
        Crowd.Replan(people);
        if (CampingBooked)
        {
            Crowd.PitchCamp();
        }
    }

    /// <summary>
    /// The build day is over: the clock starts at eight in the morning and people begin to arrive.
    /// The gates stay shut, with people queuing outside, until the player opens them.
    /// </summary>
    public void StartDay()
    {
        Need(Phase.Build);
        FinishSales();
        PayForBuses();
        ApplyTravel();
        if (CampingBooked && Plan is not null)
        {
            // Tents go up round whatever was built in the camping field during the week.
            Crowd.PitchCamp();
        }

        if (SoldAhead)
        {
            Crowd.ArriveFeeling(ArrivalMood);
        }

        Crowd.GatesShut = true;
        GatesOpenedAt = null;
        Phase = Phase.Day;
    }

    /// <summary>True while the gates are shut on the day and people are queuing outside.</summary>
    public bool GatesShut => Crowd.GatesShut;

    /// <summary>When the gates were opened today, once they have been.</summary>
    public DateTime? GatesOpenedAt { get; private set; }

    /// <summary>
    /// Opens the gates, so ticket staff start letting people in. Pressed on the build day, it
    /// starts the day with the gates open straight away.
    /// </summary>
    public void OpenGates()
    {
        if (Phase == Phase.Build)
        {
            StartDay();
        }

        Need(Phase.Day);
        if (!Crowd.GatesShut)
        {
            return;
        }

        Crowd.OpenTheGates(Clock.Now);
        GatesOpenedAt = Clock.Now;
        Note(DayNoteKind.Gates, "Gates opened", "", DayTone.Plain);
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
        TickBusking(clock.Now);
        TickSafety(clock.Now);
        TickTrouble(clock.Now);
        TickFireworks(clock.Now);
        TickWeather(clock.Now);
        TickDayLog(clock.Now);
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

        // A day that ended with the gates never opened leaves nothing shut for the tidy-up or next year.
        Crowd.GatesShut = false;
        foreach (var (pitch, party) in Crowd.Tents)
        {
            // Most campers take everything home. Some leave a bag of rubbish, and a few leave the tent.
            var mess = (uint)(party * 40503u) % 10;
            var (x, y) = FieldMap.CentreOf(pitch);
            for (var i = 0; i < (mess < 2 ? 3 : mess < 5 ? 1 : 0); i++)
            {
                // Left out in front of the tent, where it can be seen.
                _litter.Add(new LitterPiece(Math.Round(x + 1.5 + (0.4 * Math.Cos(party + (i * 2.1))), 2), Math.Round(y + (0.5 * Math.Sin(party + (i * 2.1))), 2)));
            }
        }

        LitterAtStart = _litter.Count;
        _incidents.Clear();
        EndTrouble();
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
        _sick.RemoveAll(s => ((s.X - x) * (s.X - x)) + ((s.Y - y) * (s.Y - y)) <= radius * radius);
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
        _sick.Clear();
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
    /// <summary>
    /// What the council wants to see in the bank before it lets the festival grow to a licence:
    /// enough to rent the bigger field, build on it and advertise before the ticket money comes in.
    /// </summary>
    public static int NeedsToGrowTo(int licence) => Math.Max(NeedsFor(licence), licence <= FieldMap.Sizes[0].Holds ? 0 : 5 * licence);

    public static int NeedsFor(int licence) => (((6 * 40) + 150) * licence / FirstLicence) + 200 + RentFor(licence);

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
            var bigger = LicenceAfter(Licence);
            var moving = FieldMap.SizeFor(bigger) != FieldMap.SizeFor(Licence);
            var good = Phase == Phase.Results && !_loanThisYear && happy >= HappyEnough && cleared >= TidyEnough && profit >= 0 && Untreated == 0 && !SeriousIncident && HurtLeftAlone == 0;
            var next = good && (bigger == Licence || Money >= NeedsToGrowTo(bigger)) ? bigger : Licence;
            var verdict =
                Phase == Phase.Closed ? "The money has run out a second time. The parish council will not lend again, and the festival is over." :
                _loanThisYear ? $"The bank is empty. The parish council lends £{LoanAmount:N0} to try once more. They will not do it twice." :
                SeriousIncident ? $"{CrushInjuries} people were hurt in a crush and the day had to be called off. The licence stays as it is until the crowd is kept safe." :
                HurtLeftAlone > 0 ? $"{HurtLeftAlone} {(HurtLeftAlone == 1 ? "person was" : "people were")} hurt in a crush that was left to sort itself out. The council has heard about it, and the licence stays as it is." :
                Untreated > 0 ? $"{Untreated} {(Untreated == 1 ? "person was" : "people were")} taken ill and nobody came to help. The licence stays as it is until there is proper first aid." :
                happy < HappyEnough ? "People did not enjoy it enough. The licence stays as it is until they do." :
                cleared < TidyEnough ? "The farmer is not happy with the state of the field. The licence stays as it is." :
                profit < 0 ? "A good day, but it lost money. The licence stays as it is." :
                bigger > Licence && next == Licence ? $"A good festival, but there is not yet the money in the bank for a bigger one. The council wants to see £{NeedsToGrowTo(bigger):N0} before it raises the licence to {bigger:N0}." :
                next > Licence && moving && Plan is null ? $"A good festival and a tidy field. The council raises the licence to {next:N0} people, and next year moves to {FieldMap.SizeFor(next).Name}." :
                next > Licence ? $"A good festival and a tidy field. The council raises the licence to {next:N0} people." :
                "A good festival and a tidy field. This is as big as a licence gets.";
            return new YearResult(Year, Crowd.Arrived, Crowd.Expected, happy, Crowd.WentHomeUnhappy, TotalIncome, TotalSpending, cleared, GrassWorn,
                verdict, Licence, next, _loanThisYear, Phase == Phase.Closed, Treated, Untreated, TroubleSorted, TroubleIgnored,
                CrushInjuries, PulledOut, SeriousIncident, _fieldRepair, Weather.Summary)
            {
                Announcements = ActsPaused,
                SecuritySent = SecuritySent,
                ActsCancelled = ActsCancelled,
                HurtLeftAlone = HurtLeftAlone,
            };
        }
    }

    /// <summary>
    /// What people say about the year, which sells next year's tickets: how much they enjoyed it,
    /// less for every act that was cancelled and for a crush left to hurt people.
    /// </summary>
    public static int WordOfMouth(YearResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return Math.Max(0, result.Happy - (5 * result.ActsCancelled) - (result.HurtLeftAlone > 0 ? 10 : 0));
    }

    /// <summary>Next year's festival: the bank and the licence carry over, the buildings do not.</summary>
    public FestivalGame NextYear()
    {
        Need(Phase.Results);
        var result = Result!;
        return StartYear(Name, new YearSetup(Year + 1, result.LicenceNext, Money, BailedOut, WordOfMouth(result), Plan?.Save())).TravellingLike(this);
    }

    private YearSave SaveYear() => new(Year, Licence, Phase, Day, TicketPrice, HeadlinerPicked, Crowd.Expected, BailedOut, _loanThisYear, LastHappiness,
        LitterAtStart, _litter.ToList(), _wear.ToArray())
    {
        FieldRepair = _fieldRepair,
        Fields = Plan?.Save(),
        Parking = ParkingCharge,
        Buses = BusesHired,
        SecondStage = SecondStageBooked,
        Camping = CampingBooked,
        CampingPrice = CampingPrice,
        LineUp = Enumerable.Range(0, RunningOrder.SetsAStage).Select(i => Show.At(i)?.Name)
            .Concat(Enumerable.Range(0, RunningOrder.SetsAStage).Select(i => SecondShow.At(RunningOrder.SecondBase + i)?.Name)).ToList(),
        Sales = SaveSales(),
        GatesShut = Phase == Phase.Day && Crowd.GatesShut,
        GatesOpenedAt = GatesOpenedAt,
        MainHours = Show.Hours.ToList(),
        SecondHours = SecondShow.Hours.ToList(),
    };

    private void RestoreYear(YearSave year)
    {
        TicketPrice = Math.Clamp(year.TicketPrice, 1, MaxTicketPrice);
        HeadlinerPicked = Math.Clamp(year.Headliner, 0, _offers.Count - 1);
        Show.SetHeadliner(_offers[HeadlinerPicked].Act);
        SecondShow.Booked = year.SecondStage;
        if (year.MainHours is { } mainHours)
        {
            Show.SetHours(mainHours);
        }

        if (year.SecondHours is { } secondHours)
        {
            SecondShow.SetHours(secondHours);
        }

        if (year.LineUp is { } names && names.Count == 2 * RunningOrder.SetsAStage)
        {
            for (var i = 0; i < names.Count; i++)
            {
                var set = i < RunningOrder.SetsAStage ? i : RunningOrder.SecondBase + i - RunningOrder.SetsAStage;
                // An act already in its set is left as it is, so it plays out the same after a load.
                if (StageOf(set).At(set)?.Name != names[i])
                {
                    Place(set, names[i] is { } name ? Roster.Named(name) : null);
                }
            }
        }

        ParkingCharge = Math.Clamp(year.Parking ?? ParkingCharge, 0, DearestParking);
        BusesHired = Math.Clamp(year.Buses, 0, MostBuses);
        RestoreSales(year.Sales);
        CampingPrice = Math.Clamp(year.CampingPrice ?? CampingPrice, 0, DearestPitch);
        Crowd.CamperShare = CamperShare;
        CampingBooked = Plan is null ? year.Camping && CampingOffered : CampingBooked;
        Phase = Enum.IsDefined(year.Phase) ? year.Phase : Phase.Plan;
        if (Phase != Phase.Plan)
        {
            ApplyPlan(Math.Clamp(year.Turnout, 1, Math.Max(1, Licence)));
        }
        else
        {
            Crowd.Replan(ExpectedAt(TicketPrice));
        }

        Crowd.GatesShut = Phase == Phase.Day && year.GatesShut;
        GatesOpenedAt = year.GatesOpenedAt;
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
    /// <summary>What a car pays to park.</summary>
    public int? Parking { get; init; }

    /// <summary>Shuttle buses hired.</summary>
    public int Buses { get; init; }

    /// <summary>The fields rented for the year, or null for a year on the field its licence was given.</summary>
    public List<FieldChoice>? Fields { get; init; }

    /// <summary>What the farmer charged for worn-out grass, once the field is handed back.</summary>
    public int FieldRepair { get; init; }

    /// <summary>True if acts were booked for a second stage.</summary>
    public bool SecondStage { get; init; }

    /// <summary>The hour each set starts on the main stage, or null in an older save (two, four, six and eight).</summary>
    public List<int>? MainHours { get; init; }

    /// <summary>The hour each set starts on the second stage, or null in an older save.</summary>
    public List<int>? SecondHours { get; init; }

    /// <summary>True if the camping field was taken.</summary>
    public bool Camping { get; init; }

    /// <summary>What a pitch cost, or null in an older save (four pounds).</summary>
    public int? CampingPrice { get; init; }

    /// <summary>Who is booked for each set: the main stage's four, then the second stage's four. Null is an empty set.</summary>
    public List<string?>? LineUp { get; init; }

    /// <summary>How ticket selling is going, for a year that sells its tickets ahead.</summary>
    public SalesSave? Sales { get; init; }

    /// <summary>True if the day was saved with the gates still shut and people queuing outside.</summary>
    public bool GatesShut { get; init; }

    /// <summary>When the gates were opened on the day, once they had been.</summary>
    public DateTime? GatesOpenedAt { get; init; }
}
