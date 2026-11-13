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
public sealed class FestivalGame
{
    public const string DefaultName = "Larkmoor Festival";

    public const int MaxNameLength = 40;

    public FestivalGame(string? name = null, DateTime? start = null, int? seed = null)
    {
        Name = CleanName(name);
        Seed = seed ?? StableHash(Name);
        Clock = new GameClock(start);
        Field = FieldMap.Village();
        Site = new SiteLayout(Field);
        Crowd = new Crowd(Field, Site, Seed, Clock.Now);
        Show = new RunningOrder(Site, Seed, Clock.Now);
        Crowd.Show = Show;
        Clock.Ticked += clock => Crowd.Tick(clock.Now);
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

    private BuildCheck Afford(BuildCheck check) =>
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
            Money -= check.Cost;
            Site.AddStage(kind, x, y, facing);
        }

        return check;
    }

    public BuildCheck BuildFence(FenceKind kind, TilePoint from, TilePoint to)
    {
        var check = CheckFence(kind, from, to);
        if (check.Ok)
        {
            Money -= check.Cost;
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

            Money += replaced.Sum(r => r.Value * Catalogue.Of(r.Key).CostPerMetre / 2);
            Money -= check.Cost;
            Site.AddGate(kind, x, y, vertical);
        }

        return check;
    }

    /// <summary>Takes down whatever stands on a tile and gives the money back. Returns how much.</summary>
    public int Remove(TilePoint tile)
    {
        var refund = Site.RemoveAt(tile);
        Money += refund;
        return refund;
    }

    public GameClock Clock { get; }

    public FieldMap Field { get; }

    public DateTime Now => Clock.Now;

    /// <summary>How light it is outside right now: 0 at night, 1 by day.</summary>
    public double Light => Daylight.At(Clock.Now);

    /// <summary>Moves the game on by an amount of real time. Returns the number of ticks run.</summary>
    public int Advance(double realSeconds) => Clock.Advance(realSeconds);

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

    public SaveData Save() => new(SaveData.CurrentVersion, Name, Clock.CaptureState(), Seed, Crowd.Save(), Site.Save(), Money);

    public static FestivalGame Load(SaveData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Version is < 1 or > SaveData.CurrentVersion)
        {
            throw new InvalidDataException($"This save is version {data.Version}; this game reads up to version {SaveData.CurrentVersion}.");
        }

        // The crowd's plan is for the day the festival is on, whatever time it was saved at.
        var game = new FestivalGame(data.Name, data.Clock.Now.Date + GameClock.DefaultStart.TimeOfDay, data.Seed);
        game.Clock.RestoreState(data.Clock);
        if (data.Site is not null)
        {
            game.Site.Restore(data.Site);
        }

        game.Money = data.Money ?? StartingMoney;
        if (data.Parties is not null)
        {
            game.Crowd.Restore(data.Parties);
        }

        return game;
    }
}

/// <summary>What is written to a save file.</summary>
public sealed record SaveData(int Version, string Name, ClockState Clock, int? Seed = null, List<PartySave>? Parties = null, SiteSave? Site = null, int? Money = null)
{
    /// <summary>Version 1 had the name and clock. Version 2 added the seed and the people. Version 3 added what is built, and money.</summary>
    public const int CurrentVersion = 3;

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
