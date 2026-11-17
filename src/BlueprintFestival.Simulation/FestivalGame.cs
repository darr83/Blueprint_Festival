using System.Text.Json;
using System.Text.Json.Serialization;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation;

/// <summary>
/// One game of Blueprint Festival: the festival's name, its clock and its field. Everything that
/// happens in the game happens through this class, and nothing here knows about Godot.
/// </summary>
public sealed partial class FestivalGame
{
    public const string DefaultName = "Larkmoor Festival";

    public const int MaxNameLength = 40;

    /// <summary>A festival that opens straight on the morning of the day, with nothing to plan.</summary>
    public FestivalGame(string? name = null, DateTime? start = null, int? seed = null)
        : this(name, start, seed, null)
    {
    }

    private FestivalGame(string? name, DateTime? start, int? seed, YearSetup? setup)
    {
        Name = CleanName(name);
        if (setup is not null)
        {
            Year = Math.Max(1, setup.Year);
            Licence = Math.Max(50, setup.Licence);
            Money = setup.Bank;
            BailedOut = setup.BailedOut;
            LastHappiness = setup.LastHappiness;
            EmptyBill = setup.EmptyBill;
            _daysSet = setup.Days is { } days ? Math.Clamp(days, 1, 3) : null;
            Planned = true;
            Phase = Phase.Plan;
            // A three-day festival starts on the Friday, the day before the usual Saturday.
            start ??= FestivalDay(Year).AddDays(Days == 3 ? -1 : 0) + GameClock.DefaultStart.TimeOfDay;
            seed ??= (StableHash(Name) + ((Year - 1) * 7919)) & 0x7FFFFFFF;
        }

        Seed = seed ?? StableHash(Name);
        Clock = new GameClock(start);
        Day = Clock.Now.Date;
        if (setup?.Fields is { } chosen && new FieldPlan(chosen) is { WayIn: not null } plan)
        {
            Plan = plan;
            CampingBooked = Licence >= FieldPlan.CampingFrom && plan.Of(FieldUse.Camping).Any();
        }

        Field = Plan is not null ? FieldMap.ForPlan(Plan, Licence) : Planned ? FieldMap.ForLicence(Licence) : FieldMap.Village();
        Site = new SiteLayout(Field);
        Crowd = new Crowd(Field, Site, Seed, Clock.Now);
        Show = new RunningOrder(Site, Seed, Clock.Now);
        Crowd.Show = Show;
        var shows = new List<RunningOrder> { Show };
        for (var n = 1; n < RunningOrder.MaxStages; n++)
        {
            shows.Add(new RunningOrder(Site, Seed, Clock.Now, shows, n));
        }

        Shows = shows;
        Crowd.Shows = Shows;
        Crowd.TicketsSold += people =>
        {
            // Tickets sold ahead were paid for when they were bought.
            if (!SoldAhead && DayNumber == 0)
            {
                Earn("Tickets", people * TicketPrice);
            }
        };
        Crowd.CamperArrived += PayForPitch;
        Crowd.CarParked += () =>
        {
            if (ParkingCharge > 0)
            {
                Earn("Parking", ParkingCharge);
            }
        };
        Crowd.Replanned += ApplyTravel;
        // The stallholders keep most of what they take; the festival gets its share for the pitch.
        Crowd.Served += (kind, _, paid) =>
        {
            // The festival's share, kept to the penny so a run of small sales is not rounded up or away.
            _sharePence += paid * Catalogue.PitchShare;
            var pounds = _sharePence / 100;
            _sharePence -= pounds * 100;
            if (pounds > 0)
            {
                Earn(TakingsLine(kind), pounds);
            }
        };
        Crowd.Sold += NoteSale;
        Crowd.Dropped += (x, y) => _litter.Add(new LitterPiece(Math.Round(x, 2), Math.Round(y, 2)));
        Clock.Ticked += Tick;
        _wear = new int[WearColumns * WearRows];
        StartStaff();
        StartRounds();
        StartSafety();
        StartTrouble();
        StartAreas();
        StartStands();
        StartWeather();
        if (Planned)
        {
            MakeOffers();
            if (EmptyBill)
            {
                ClearBill();
            }

            StartDays();
        }
    }

    public string Name { get; }

    /// <summary>The number everything random in this festival grows from: who comes, and when.</summary>
    public int Seed { get; }

    public Crowd Crowd { get; }

    /// <summary>The day's music: who plays when.</summary>
    public RunningOrder Show { get; }

    /// <summary>The second stage's music. It plays only if its acts were booked at planning and a second stage is put up.</summary>
    public RunningOrder SecondShow => Shows[1];

    /// <summary>
    /// Every stage's music, main stage first, always <see cref="RunningOrder.MaxStages"/> long. The
    /// extra stages play only if their acts were booked at planning and that many stages are put up.
    /// </summary>
    public IReadOnlyList<RunningOrder> Shows { get; }

    /// <summary>The stages' music that is booked, main stage first.</summary>
    public IEnumerable<RunningOrder> BookedShows => Shows.Where(s => s.Booked);

    /// <summary>True while an act is on, on any stage.</summary>
    public bool MusicOn => Shows.Any(s => s.Playing(Now) is not null);

    /// <summary>True while a crowd is gathering at the front for a big act that has not come on yet.</summary>
    public bool CrowdWaiting => Shows.Any(s => s.FillingUp(Now) is not null);

    /// <summary>The line-up a set belongs to, by the set's number.</summary>
    public RunningOrder ShowOf(int set) => Shows[RunningOrder.StageOf(set)];

    /// <summary>What the player has built on the field.</summary>
    public SiteLayout Site { get; }

    /// <summary>The money in the festival's bank account, in pounds.</summary>
    public int Money { get; private set; } = StartingMoney;

    /// <summary>What a first village festival has to spend: the parish council's float.</summary>
    public const int StartingMoney = 2000;

    /// <summary>What has come in today, line by line, in the order it first came in.</summary>
    public IReadOnlyList<LedgerLine> Income => _income;

    /// <summary>What has been spent today, line by line.</summary>
    public IReadOnlyList<LedgerLine> Spending => _spending;

    public int TotalIncome => _income.Sum(l => l.Amount);

    public int TotalSpending => _spending.Sum(l => l.Amount);

    private readonly List<LedgerLine> _income = new();
    private readonly List<LedgerLine> _spending = new();

    private static void Note(List<LedgerLine> lines, string what, int amount)
    {
        var i = lines.FindIndex(l => l.What == what);
        if (i < 0)
        {
            lines.Add(new LedgerLine(what, amount));
        }
        else
        {
            lines[i] = lines[i] with { Amount = lines[i].Amount + amount };
        }

        lines.RemoveAll(l => l.Amount == 0);
    }

    private void Earn(string what, int amount)
    {
        if (amount != 0)
        {
            Money += amount;
            Note(_income, what, amount);
        }
    }

    /// <summary>Pays for something, or with a negative amount takes a refund.</summary>
    private void Spend(string what, int amount)
    {
        Money -= amount;
        Note(_spending, what, amount);
    }

    /// <summary>Whether a stall could go here, counting the money.</summary>
    public BuildCheck CheckStall(StallKind kind, int x, int y, Facing facing) => Afford(Site.CheckStall(kind, x, y, facing));

    /// <summary>Puts up a stall and pays its hire, if it fits and there is the money.</summary>
    public BuildCheck BuildStall(StallKind kind, int x, int y, Facing facing)
    {
        var check = CheckStall(kind, x, y, facing);
        if (check.Ok)
        {
            Spend("Stalls and toilets", check.Cost);
            Site.AddStall(kind, x, y, facing);
        }

        return check;
    }

    private BuildCheck Afford(BuildCheck check) =>
        check.Ok && !CanBuild ? new BuildCheck(false, check.Cost, "Nothing can be built just now.") :
        check.Ok && check.Cost > Money ? new BuildCheck(false, check.Cost, $"That costs £{check.Cost:N0} and there is £{Money:N0} in the bank.") : check;

    /// <summary>Whether a stage could go here, counting the money.</summary>
    public BuildCheck CheckStage(StageKind kind, int x, int y, Facing facing) => Afford(Site.CheckStage(kind, x, y, facing));

    public BuildCheck CheckFence(FenceKind kind, TilePoint from, TilePoint to) => Afford(Site.CheckFence(kind, from, to));

    public BuildCheck CheckGate(GateKind kind, int x, int y, bool vertical) => Afford(Site.CheckGate(kind, x, y, vertical));

    /// <summary>Puts up a stage and pays for it, if it fits and there is the money.</summary>
    public BuildCheck BuildStage(StageKind kind, int x, int y, Facing facing)
    {
        var check = CheckStage(kind, x, y, facing);
        if (check.Ok)
        {
            Spend("Stage", check.Cost);
            Site.AddStage(kind, x, y, facing);
        }

        return check;
    }

    public BuildCheck BuildFence(FenceKind kind, TilePoint from, TilePoint to)
    {
        var check = CheckFence(kind, from, to);
        if (check.Ok)
        {
            Spend("Fences and gates", check.Cost);
            Site.AddFence(kind, from, to);
        }

        return check;
    }

    /// <summary>Whether a queue lane could be dragged out here, counting the money.</summary>
    public BuildCheck CheckLane(TilePoint from, TilePoint to) => Afford(Site.CheckLane(from, to));

    /// <summary>Lays a queue lane (or carries one on from its end) and pays for it. Its barriers go up by themselves.</summary>
    public BuildCheck BuildLane(TilePoint from, TilePoint to)
    {
        var check = CheckLane(from, to);
        if (check.Ok)
        {
            Spend("Fences and gates", check.Cost);
            Site.AddLane(from, to);
        }

        return check;
    }

    /// <summary>Whether the box between two tiles can be marked "no customers", or cleared again.</summary>
    public BuildCheck CheckKeepOut(TilePoint from, TilePoint to, bool keepOut)
    {
        var check = Site.CheckKeepOut(from, to, keepOut);
        return check.Ok && !CanBuild ? BuildCheck.No("Nothing can be built just now.") : check;
    }

    /// <summary>Marks ground "no customers" (or clears it). It costs nothing; staff still go there.</summary>
    public BuildCheck KeepOut(TilePoint from, TilePoint to, bool keepOut)
    {
        var check = CheckKeepOut(from, to, keepOut);
        return check.Ok ? Site.SetKeepOut(from, to, keepOut) : check;
    }

    public BuildCheck CheckVipGround(TilePoint from, TilePoint to, bool vip)
    {
        var check = Site.CheckVipGround(from, to, vip);
        return check.Ok && !CanBuild ? BuildCheck.No("Nothing can be built just now.") : check;
    }

    /// <summary>Marks ground for VIP guests only (or clears it). It costs nothing; staff still go there.</summary>
    public BuildCheck VipGround(TilePoint from, TilePoint to, bool vip)
    {
        var check = CheckVipGround(from, to, vip);
        return check.Ok ? Site.SetVipGround(from, to, vip) : check;
    }

    public BuildCheck BuildGate(GateKind kind, int x, int y, bool vertical)
    {
        var check = CheckGate(kind, x, y, vertical);
        if (check.Ok)
        {
            // The fence the gate replaces is paid back.
            var replaced = new Dictionary<FenceKind, int>();
            for (var i = 0; i < Catalogue.Of(kind).Width; i++)
            {
                if (Site.FenceAt(new TilePoint(x + (vertical ? 0 : i), y + (vertical ? i : 0))) is { } fence)
                {
                    replaced[fence] = replaced.GetValueOrDefault(fence) + 1;
                }
            }

            Spend("Fences and gates", check.Cost - replaced.Sum(r => r.Value * Catalogue.Of(r.Key).CostPerMetre / 2));
            Site.AddGate(kind, x, y, vertical);
        }

        return check;
    }

    /// <summary>Takes down whatever stands on a tile and gives the money back. Returns how much.</summary>
    public int Remove(TilePoint tile)
    {
        if (!CanBuild)
        {
            return 0;
        }

        var line = Site.PadRefundAt(tile) > 0 ? Fireworks.Line : Site.ScreenRefundAt(tile) > 0 ? BigScreens.Line : Site.LightRefundAt(tile) > 0 ? LightsLine : Site.ComfortRefundAt(tile) > 0 ? Comforts.Line : Site.SignRefundAt(tile) > 0 ? Signs.Line : Site.DecorRefundAt(tile) > 0 ? Decor.Line : Site.StageAt(tile) is not null ? "Stage" : Site.StallAt(tile) is not null ? "Stalls and toilets" : Site.StandAt(tile) >= 0 ? StandsLine : "Fences and gates";
        var refund = Site.RemoveAt(tile);
        if (refund != 0)
        {
            Spend(line, -refund);
        }
        else if (PatchAt(tile) is var (column, row) && GroundAt(column, row) is not null)
        {
            // Nothing stands here, so it is the straw or trackway that comes up.
            return LiftGround(column, row);
        }

        return refund;
    }

    public GameClock Clock { get; }

    public FieldMap Field { get; }

    public DateTime Now => Clock.Now;

    /// <summary>How light it is outside right now: 0 at night, 1 by day.</summary>
    public double Light => Daylight.At(Clock.Now);

    /// <summary>Moves the game on by an amount of real time. Returns the number of ticks run.</summary>
    public int Advance(double realSeconds)
    {
        if (Phase != Phase.Day)
        {
            SkippingTo = null;
            return 0;
        }

        return SkippingTo is null ? Clock.Advance(realSeconds) : RunSkip();
    }

    /// <summary>
    /// The most ticks a skip runs in one frame, and the longest it may take over them, so the game
    /// keeps drawing. The crowd stands still on screen while a skip runs, so nearly all of each
    /// frame can go on the rules: about thirty frames a second.
    /// </summary>
    public const int SkipTicksAFrame = 1200;

    public static readonly TimeSpan SkipFrameBudget = TimeSpan.FromMilliseconds(30);

    /// <summary>The start of the next set on any stage, or null once the last has begun or with no music to come.</summary>
    public Slot? NextAct
    {
        get
        {
            var next = Shows.Where(s => s.Stage is not null).Select(s => s.Next(Now)).Where(s => s is not null).ToList();
            return next.Count == 0 ? null : next.MinBy(s => s!.Start);
        }
    }

    /// <summary>While the player is skipping to the next act, the time the game is hurrying on to.</summary>
    public DateTime? SkippingTo { get; private set; }

    /// <summary>
    /// Hurries the day on to the start of the next act. Nothing is missed out: every second in
    /// between still happens, only as fast as the computer can run it, whatever the speed and even
    /// when paused. False if there is no act still to come.
    /// </summary>
    public bool SkipToNextAct()
    {
        if (Phase != Phase.Day || NextAct is not { } next)
        {
            return false;
        }

        SkippingTo = next.Start;
        _skipFromCrush = CrushBuilding;
        return true;
    }

    /// <summary>Stops a skip where it has got to.</summary>
    public void StopSkipping() => SkippingTo = null;

    private bool _skipFromCrush;

    private int RunSkip()
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        var ticks = 0;
        while (SkippingTo is { } until && ticks < SkipTicksAFrame && (ticks < 8 || started.Elapsed < SkipFrameBudget))
        {
            if (Phase != Phase.Day || Clock.Now >= until || (CrushBuilding && !_skipFromCrush))
            {
                // There, or something needs the player: a crush that starts to build is not to be hurried past.
                SkippingTo = null;
                break;
            }

            Clock.Step();
            ticks++;
        }

        return ticks;
    }

    /// <summary>Tidies a name the player typed; an empty one becomes the default.</summary>
    public static string CleanName(string? name)
    {
        var clean = string.Join(' ', (name ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (clean.Length == 0)
        {
            return DefaultName;
        }

        return clean.Length > MaxNameLength ? clean[..MaxNameLength].TrimEnd() : clean;
    }

    /// <summary>The same name always gives the same number, on every computer.</summary>
    private static int StableHash(string text)
    {
        unchecked
        {
            var hash = (int)2166136261;
            foreach (var c in text)
            {
                hash = (hash ^ c) * 16777619;
            }

            return hash & 0x7FFFFFFF;
        }
    }

    public SaveData Save() => new(SaveData.CurrentVersion, Name, Clock.CaptureState(), Seed, Crowd.Save(), Site.Save(), Money, _income.ToList(), _spending.ToList())
    {
        Year = Planned ? SaveYear() : null,
        Staff = _staff.Count > 0 || _incidents.Count > 0 || Planned ? SaveStaff() : null,
        Safety = Planned ? SaveSafety() : null,
        Weather = SaveWeather(),
        StallSales = _stallSales.Count > 0 ? SaveStallSales() : null,
        SatDown = Crowd.SatDown,
        WashedHands = Crowd.WashedHands,
        PricedOut = Crowd.PricedOut,
        SharePence = _sharePence,
        Fireworks = SaveFireworks(),
        DayLog = Planned ? SaveDayLog() : null,
        CampLife = SaveCampLife(),
    };

    public static FestivalGame Load(SaveData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Version is < 1 or > SaveData.CurrentVersion)
        {
            throw new InvalidDataException($"This save is version {data.Version}; this game reads up to version {SaveData.CurrentVersion}.");
        }

        // The crowd's plan is for the day the festival is on, whatever time it was saved at.
        var game = data.Year is { } year
            ? new FestivalGame(data.Name, year.Day.Date + GameClock.DefaultStart.TimeOfDay, data.Seed, new YearSetup(year.Number, year.Licence, 0, year.BailedOut, year.LastHappiness, year.Fields) { EmptyBill = year.EmptyBill, Days = year.DaysSet })
            : new FestivalGame(data.Name, data.Clock.Now.Date + GameClock.DefaultStart.TimeOfDay, data.Seed);
        // The clock comes back first so the rain is right, and the weather before the crowd is planned again.
        game.Clock.RestoreState(data.Clock);
        if (data.Weather is not null)
        {
            game.RestoreWeather(data.Weather);
        }

        if (data.Year is not null)
        {
            game.RestoreYear(data.Year);
        }
        else if (data.Weather is not null)
        {
            game.Crowd.Replan(game.Crowd.Expected);
        }

        if (data.Site is not null)
        {
            game.Site.Restore(data.Site);
        }

        game.Money = data.Money ?? StartingMoney;
        game._income.AddRange(data.Income ?? new());
        game._spending.AddRange(data.Spending ?? new());
        if (data.Parties is not null)
        {
            game.Crowd.Restore(data.Parties);
        }

        if (data.Staff is not null)
        {
            game.RestoreStaff(data.Staff);
        }

        if (data.Safety is not null)
        {
            game.RestoreSafety(data.Safety);
        }

        game.RestoreStallSales(data.StallSales, data.PricedOut);
        game.Crowd.SatDown = Math.Max(0, data.SatDown);
        game.Crowd.WashedHands = Math.Max(0, data.WashedHands);
        game._sharePence = data.SharePence;
        game.RestoreFireworks(data.Fireworks);
        game.LoadDayLog(data.DayLog);
        game.RestoreCampLife(data.CampLife);

        return game;
    }
}

/// <summary>What is written to a save file.</summary>
/// <summary>One line of the day's money: what it was for and how many pounds.</summary>
public sealed record LedgerLine(string What, int Amount);

public sealed record SaveData(int Version, string Name, ClockState Clock, int? Seed = null, List<PartySave>? Parties = null, SiteSave? Site = null, int? Money = null,
    List<LedgerLine>? Income = null, List<LedgerLine>? Spending = null)
{
    /// <summary>Version 1 had the name and clock. Version 2 added the seed and the people. Version 3 added what is built, and money. Version 4 added stalls, needs and the day's takings. Version 5 added the festival year. Version 6 added staff and first aid. Version 7 added crowd safety. Version 8 added weather, mud and ground cover.</summary>
    public const int CurrentVersion = 8;

    /// <summary>What each stall has sold today, and how many people the prices turned away.</summary>
    public List<StallSalesSave>? StallSales { get; init; }

    public int PricedOut { get; init; }

    /// <summary>How many people have sat down, and how many have washed their hands, so far today.</summary>
    public int SatDown { get; init; }

    public int WashedHands { get; init; }

    /// <summary>The pennies of the festival's share of stall takings not yet made up to a pound.</summary>
    public int SharePence { get; init; }

    /// <summary>The day's firework shows and how they went.</summary>
    public FireworksSave? Fireworks { get; init; }

    /// <summary>The day's log and happiness line for the event day panel; null in older saves.</summary>
    public DayLogSave? DayLog { get; init; }

    /// <summary>The camping field's night and morning after; null when there was none, and in older saves.</summary>
    public CampLifeSave? CampLife { get; init; }

    /// <summary>Where the festival is in its year. Missing from a festival with nothing to plan.</summary>
    public YearSave? Year { get; init; }

    /// <summary>Who is hired and what they are dealing with.</summary>
    public StaffSave? Staff { get; init; }

    /// <summary>Crush injuries and anything else about crowd safety on the day.</summary>
    public SafetySave? Safety { get; init; }

    /// <summary>The day's weather, how wet the ground is and what is laid on it.</summary>
    public WeatherSave? Weather { get; init; }

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    public static SaveData FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<SaveData>(json, Options) is { Clock: not null, Name: not null } data
                ? data
                : throw new InvalidDataException("The save file is empty or incomplete.");
        }
        catch (JsonException e)
        {
            throw new InvalidDataException("The save file could not be read.", e);
        }
    }
}
