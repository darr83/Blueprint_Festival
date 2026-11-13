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
            Planned = true;
            Phase = Phase.Plan;
            start ??= FestivalDay(Year) + GameClock.DefaultStart.TimeOfDay;
            seed ??= (StableHash(Name) + ((Year - 1) * 7919)) & 0x7FFFFFFF;
        }

        Seed = seed ?? StableHash(Name);
        Clock = new GameClock(start);
        Day = Clock.Now.Date;
        Field = FieldMap.Village();
        Site = new SiteLayout(Field);
        Crowd = new Crowd(Field, Site, Seed, Clock.Now);
        Show = new RunningOrder(Site, Seed, Clock.Now);
        Crowd.Show = Show;
        Crowd.TicketsSold += people => Earn("Tickets", people * TicketPrice);
        // The stallholders keep most of what they take; the festival gets its share for the pitch.
        Crowd.Served += (kind, _, paid) => Earn(Catalogue.Of(kind).Name, ((paid * Catalogue.PitchShare) + 50) / 100);
        Crowd.Dropped += (x, y) => _litter.Add(new LitterPiece(Math.Round(x, 2), Math.Round(y, 2)));
        Clock.Ticked += Tick;
        _wear = new int[WearColumns * WearRows];
        StartStaff();
        if (Planned)
        {
            MakeOffers();
        }
    }

    public string Name { get; }

    /// <summary>The number everything random in this festival grows from: who comes, and when.</summary>
    public int Seed { get; }

    public Crowd Crowd { get; }

    /// <summary>The day's music: who plays when.</summary>
    public RunningOrder Show { get; }

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

        var line = Site.StageAt(tile) is not null ? "Stage" : Site.StallAt(tile) is not null ? "Stalls and toilets" : "Fences and gates";
        var refund = Site.RemoveAt(tile);
        if (refund != 0)
        {
            Spend(line, -refund);
        }

        return refund;
    }

    public GameClock Clock { get; }

    public FieldMap Field { get; }

    public DateTime Now => Clock.Now;

    /// <summary>How light it is outside right now: 0 at night, 1 by day.</summary>
    public double Light => Daylight.At(Clock.Now);

    /// <summary>Moves the game on by an amount of real time. Returns the number of ticks run.</summary>
    public int Advance(double realSeconds) => Phase == Phase.Day ? Clock.Advance(realSeconds) : 0;

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
            ? new FestivalGame(data.Name, year.Day.Date + GameClock.DefaultStart.TimeOfDay, data.Seed, new YearSetup(year.Number, year.Licence, 0, year.BailedOut, year.LastHappiness))
            : new FestivalGame(data.Name, data.Clock.Now.Date + GameClock.DefaultStart.TimeOfDay, data.Seed);
        if (data.Year is not null)
        {
            game.RestoreYear(data.Year);
        }

        game.Clock.RestoreState(data.Clock);
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

        return game;
    }
}

/// <summary>What is written to a save file.</summary>
/// <summary>One line of the day's money: what it was for and how many pounds.</summary>
public sealed record LedgerLine(string What, int Amount);

public sealed record SaveData(int Version, string Name, ClockState Clock, int? Seed = null, List<PartySave>? Parties = null, SiteSave? Site = null, int? Money = null,
    List<LedgerLine>? Income = null, List<LedgerLine>? Spending = null)
{
    /// <summary>Version 1 had the name and clock. Version 2 added the seed and the people. Version 3 added what is built, and money. Version 4 added stalls, needs and the day's takings. Version 5 added the festival year. Version 6 added staff and first aid.</summary>
    public const int CurrentVersion = 6;

    /// <summary>Where the festival is in its year. Missing from a festival with nothing to plan.</summary>
    public YearSave? Year { get; init; }

    /// <summary>Who is hired and what they are dealing with.</summary>
    public StaffSave? Staff { get; init; }

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
