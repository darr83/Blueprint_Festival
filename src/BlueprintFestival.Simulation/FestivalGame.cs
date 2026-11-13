using System.Text.Json;
using System.Text.Json.Serialization;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
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
        Crowd = new Crowd(Field, Seed, Clock.Now);
        Clock.Ticked += clock => Crowd.Tick(clock.Now);
    }

    public string Name { get; }

    /// <summary>The number everything random in this festival grows from: who comes, and when.</summary>
    public int Seed { get; }

    public Crowd Crowd { get; }

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

    public SaveData Save() => new(SaveData.CurrentVersion, Name, Clock.CaptureState(), Seed, Crowd.Save());

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
        if (data.Parties is not null)
        {
            game.Crowd.Restore(data.Parties);
        }

        return game;
    }
}

/// <summary>What is written to a save file.</summary>
public sealed record SaveData(int Version, string Name, ClockState Clock, int? Seed = null, List<PartySave>? Parties = null)
{
    /// <summary>Version 1 had the name and clock. Version 2 added the seed and the people.</summary>
    public const int CurrentVersion = 2;

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
