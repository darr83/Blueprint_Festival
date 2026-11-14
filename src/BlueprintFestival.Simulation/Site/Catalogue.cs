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

    MainStage,
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

    TurnstileRow,
}

/// <summary>The stalls and services people use.</summary>
public enum StallKind
{
    /// <summary>A trestle table under a garden gazebo, run by the village hall committee.</summary>
    TeaAndCake,

    /// <summary>A catering van with a hatch: hot food.</summary>
    FoodVan,

    /// <summary>A striped marquee with a wooden bar across the front.</summary>
    Bar,

    /// <summary>Three portable cabins in a row.</summary>
    Toilets,

    /// <summary>A standpipe with two taps. Free.</summary>
    WaterPoint,

    /// <summary>A white tent with a green cross, where people who are unwell can lie down.</summary>
    FirstAidTent,

    /// <summary>Nine toilet cabins in a row.</summary>
    ToiletBlock,

    /// <summary>A big white tent with a green cross and six beds, for a big festival.</summary>
    MedicalTent,
}

/// <summary>What a person can need during the day.</summary>
public enum Need
{
    Food,
    Drink,
    Toilet,
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

/// <summary>
/// What there is to know about a kind of stall: its size in tiles, what it costs to hire, the need
/// it answers, what each person pays, how long each takes to serve and how many are served at once.
/// </summary>
public sealed record StallInfo(StallKind Kind, string Name, int Width, int Depth, int Cost, Need? Serves, int Price, int SecondsPerPerson, int Servers, string About);

/// <summary>Everything the player can build, with its size and what it costs to hire for the day.</summary>
/// <summary>The kinds of staff a festival hires for the day.</summary>
public enum StaffKind
{
    TicketStaff,
    Security,
    FirstAider,
}

/// <summary>One kind of staff: what they are called, what they cost for the day, and what they do.</summary>
public sealed record StaffInfo(StaffKind Kind, string Name, int Wage, string About);

public static class Catalogue
{
    public static readonly IReadOnlyList<StageInfo> Stages = new[]
    {
        new StageInfo(StageKind.FarmTrailer, "Farm trailer", 16, 6, 50, 2, "Lent by the farmer for the price of the diesel. Room for a duo."),
        new StageInfo(StageKind.Bandstand, "Bandstand", 12, 11, 400, 4, "A deck under a striped roof. Room for a small band."),
        new StageInfo(StageKind.HiredStage, "Hired stage", 24, 12, 1500, 5, "A proper stage with a roof, lights and big speakers."),
        new StageInfo(StageKind.MainStage, "Main stage", 40, 20, 6000, 6, "A festival main stage: a big roof, a wall of speakers and room for any band."),
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
        new GateInfo(GateKind.TurnstileRow, "Turnstile row", 24, 4, 6, 2000, "Four fast lanes under one long arch, for when hundreds arrive at once."),
    };

    public static readonly IReadOnlyList<StallInfo> Stalls = new[]
    {
        new StallInfo(StallKind.TeaAndCake, "Tea and cake table", 6, 6, 30, Need.Food, 3, 60, 1, "Run by the village hall committee. Cheap, slow, one queue."),
        new StallInfo(StallKind.FoodVan, "Food van", 12, 5, 120, Need.Food, 6, 45, 2, "Hot food from a hatch. Serves two at a time."),
        new StallInfo(StallKind.Bar, "Bar tent", 12, 8, 200, Need.Drink, 4, 25, 3, "Drinks from a long bar. Serves three at a time."),
        new StallInfo(StallKind.Toilets, "Toilets", 8, 3, 40, Need.Toilet, 0, 75, 3, "Three cabins. People will not wait for ever."),
        new StallInfo(StallKind.WaterPoint, "Water point", 3, 2, 15, Need.Drink, 0, 20, 2, "Two taps. Free, so it earns nothing."),
        new StallInfo(StallKind.FirstAidTent, "First aid tent", 8, 6, 80, null, 0, 1200, 2, "Two beds under a green cross. First-aiders bring people to the nearest free bed to rest."),
        new StallInfo(StallKind.ToiletBlock, "Toilet block", 24, 3, 110, Need.Toilet, 0, 75, 9, "Nine cabins in a row, for a bigger crowd."),
        new StallInfo(StallKind.MedicalTent, "Medical tent", 16, 10, 450, null, 0, 600, 6, "Six beds under a green cross. People are back on their feet in half the time."),
    };

    /// <summary>The festival's share of what a stall takes, in pounds out of every hundred.</summary>
    public const int PitchShare = 30;

    /// <summary>What one person pays at the gate.</summary>
    public const int TicketPrice = 5;

    public static StallInfo Of(StallKind kind) => Stalls[(int)kind];

    public static readonly IReadOnlyList<StaffInfo> Staff = new[]
    {
        new StaffInfo(StaffKind.TicketStaff, "Ticket staff", 40, "Checks tickets at a gate. One for each lane."),
        new StaffInfo(StaffKind.Security, "Security", 60, "Deals with trouble nearby. Behind a front barrier each one holds another 8 m of crowd."),
        new StaffInfo(StaffKind.FirstAider, "First-aider", 50, "Goes to anyone taken ill or hurt and looks after them."),
    };

    public static StaffInfo Of(StaffKind kind) => Staff[(int)kind];

    public static StageInfo Of(StageKind kind) => Stages[(int)kind];

    public static FenceInfo Of(FenceKind kind) => Fences[(int)kind];

    public static GateInfo Of(GateKind kind) => Gates[(int)kind];
}
