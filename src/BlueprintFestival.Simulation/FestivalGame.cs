using System.Text.Json;
using System.Text.Json.Serialization;
using BlueprintFestival.Simulation.Core;
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

    public FestivalGame(string? name = null, DateTime? start = null)
    {
        Name = CleanName(name);
        Clock = new GameClock(start);
        Field = FieldMap.Village();
    }

    public string Name { get; }

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

    public SaveData Save() => new(SaveData.CurrentVersion, Name, Clock.CaptureState());

    public static FestivalGame Load(SaveData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Version is < 1 or > SaveData.CurrentVersion)
        {
            throw new InvalidDataException($"This save is version {data.Version}; this game reads up to version {SaveData.CurrentVersion}.");
        }

        var game = new FestivalGame(data.Name, data.Clock.Now);
        game.Clock.RestoreState(data.Clock);
        return game;
    }
}

/// <summary>What is written to a save file.</summary>
public sealed record SaveData(int Version, string Name, ClockState Clock)
{
    public const int CurrentVersion = 1;

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
