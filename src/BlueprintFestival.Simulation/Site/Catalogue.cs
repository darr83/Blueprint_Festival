namespace BlueprintFestival.Simulation.Site;

/// <summary>The kinds of small stage a village festival can put up.</summary>
public enum StageKind
{
    /// <summary>A farm trailer with straw bales: nearly free, room for a duo.</summary>
    FarmTrailer,

    /// <summary>A wooden deck under a striped canvas roof: the village fete bandstand.</summary>
    Bandstand,

    /// <summary>A hired stage with a steel frame, a roof, lights and proper speakers.</summary>
    HiredStage,
}

/// <summary>The kinds of fence.</summary>
public enum FenceKind
{
    /// <summary>Wooden stakes and rope.</summary>
    Rope,

    /// <summary>Chestnut paling: waist-high slats on wire.</summary>
    Paling,

    /// <summary>Low steel crowd barriers that hook together.</summary>
    CrowdBarrier,

    /// <summary>Tall steel mesh panels in blocks.</summary>
    SteelMesh,
}

/// <summary>The kinds of way in.</summary>
public enum GateKind
{
    /// <summary>Two posts, a painted board and a table to check tickets at.</summary>
    VillageGate,

    /// <summary>A steel arch with a banner, two lanes and a ticket hut.</summary>
    ArchGate,
}

/// <summary>Which way something faces. A stage faces its audience.</summary>
public enum Facing
{
    South,
    West,
    North,
    East,
}

/// <summary>What there is to know about a kind of stage. Sizes are in half-metre tiles.</summary>
public sealed record StageInfo(StageKind Kind, string Name, int Width, int Depth, int Cost, int Performers, string About);

/// <summary>What there is to know about a kind of fence. The cost is for each metre.</summary>
public sealed record FenceInfo(FenceKind Kind, string Name, int CostPerMetre, string About);

/// <summary>What there is to know about a kind of gate: how wide, how many lanes, how fast each lane checks one person.</summary>
public sealed record GateInfo(GateKind Kind, string Name, int Width, int Lanes, int SecondsPerPerson, int Cost, string About);

/// <summary>Everything the player can build, with its size and what it costs to hire for the day.</summary>
public static class Catalogue
{
    public static readonly IReadOnlyList<StageInfo> Stages = new[]
    {
        new StageInfo(StageKind.FarmTrailer, "Farm trailer", 16, 6, 50, 2, "Lent by the farmer for the price of the diesel. Room for a duo."),
        new StageInfo(StageKind.Bandstand, "Bandstand", 12, 11, 400, 4, "A deck under a striped roof. Room for a small band."),
        new StageInfo(StageKind.HiredStage, "Hired stage", 24, 12, 1500, 5, "A proper stage with a roof, lights and big speakers."),
    };

    public static readonly IReadOnlyList<FenceInfo> Fences = new[]
    {
        new FenceInfo(FenceKind.Rope, "Rope and stakes", 1, "Marks a line. Cheap and friendly."),
        new FenceInfo(FenceKind.Paling, "Chestnut paling", 3, "Waist-high wooden slats on wire."),
        new FenceInfo(FenceKind.CrowdBarrier, "Crowd barrier", 5, "Steel barriers for queues and the front of a stage."),
        new FenceInfo(FenceKind.SteelMesh, "Steel mesh fence", 8, "Tall panels: the proper festival fence."),
    };

    public static readonly IReadOnlyList<GateInfo> Gates = new[]
    {
        new GateInfo(GateKind.VillageGate, "Village gate", 8, 1, 30, 40, "Two posts and a ticket table. One queue."),
        new GateInfo(GateKind.ArchGate, "Arch gate", 12, 2, 10, 600, "A steel arch with two lanes and a ticket hut."),
    };

    public static StageInfo Of(StageKind kind) => Stages[(int)kind];

    public static FenceInfo Of(FenceKind kind) => Fences[(int)kind];

    public static GateInfo Of(GateKind kind) => Gates[(int)kind];
}
