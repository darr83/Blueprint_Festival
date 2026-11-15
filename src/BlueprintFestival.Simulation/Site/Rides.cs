using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Site;

/// <summary>
/// The fairground: the rides and the hook a duck stall. They are stalls like any other (hired for
/// the day, a price the player sets, a queue), but a ride takes its riders round together: it
/// stands still while people get on, then runs, then everybody gets off at once.
/// </summary>
public static class Rides
{
    /// <summary>How long a ride stands still for people to get off and on, in game seconds.</summary>
    public const int BoardingSeconds = 60;

    /// <summary>The most people of one party that ride together. Each party is given this many seats.</summary>
    public const int SeatsEach = 4;

    /// <summary>The chance that a go at hook a duck wins the party a big teddy.</summary>
    public const double TeddyChance = 0.25;

    /// <summary>How much a ride lifts the mood of a party that has just been on it.</summary>
    public const double Thrill = 0.02;

    /// <summary>True for a ride people sit on: everything at the fairground but hook a duck.</summary>
    public static bool IsRide(StallKind kind) => kind is StallKind.BigWheel or StallKind.Waltzer or StallKind.Twister or StallKind.Carousel or StallKind.Skylark;

    /// <summary>True for anything from the fairground, hook a duck too.</summary>
    public static bool IsFair(StallKind kind) => IsRide(kind) || kind == StallKind.HookADuck;

    /// <summary>How long one go on a ride lasts once it starts, in game seconds.</summary>
    public static int RunSeconds(StallKind kind) => kind switch
    {
        StallKind.BigWheel => 210,
        StallKind.Carousel => 150,
        StallKind.Skylark => 120,
        _ => 150,
    };

    /// <summary>A whole turn of a ride, in game seconds: standing to load, then running.</summary>
    public static int CycleSeconds(StallKind kind) => BoardingSeconds + RunSeconds(kind);

    /// <summary>
    /// Where a ride is in its turn at a moment: whether it is standing for people to get on, how
    /// many seconds until this go ends, and how many seconds it has been running (0 while it stands).
    /// Each ride keeps its own time, so the fairground does not all start and stop together.
    /// </summary>
    public static (bool Boarding, int Left, int Running) TurnAt(PlacedStall ride, DateTime now)
    {
        var cycle = CycleSeconds(ride.Kind);
        var second = (long)now.TimeOfDay.TotalSeconds + (ride.X * 37L) + (ride.Y * 53L);
        var into = (int)(((second % cycle) + cycle) % cycle);
        return (into < BoardingSeconds, cycle - into, Math.Max(0, into - BoardingSeconds));
    }

    /// <summary>How much a kind of party fancies a ride when they think about it, before the weather and the price.</summary>
    public static double Pull(StallKind kind, bool hasChild) => kind switch
    {
        StallKind.Carousel => hasChild ? 0.16 : 0.02,
        StallKind.BigWheel => hasChild ? 0.1 : 0.07,
        StallKind.Waltzer => hasChild ? 0.03 : 0.08,
        StallKind.Twister => hasChild ? 0.05 : 0.07,
        // Too tall and too fast for little ones.
        StallKind.Skylark => hasChild ? 0 : 0.07,
        StallKind.HookADuck => hasChild ? 0.14 : 0.03,
        _ => 0,
    };
}
