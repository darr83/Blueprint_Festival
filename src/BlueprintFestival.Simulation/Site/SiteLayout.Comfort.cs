using System;
using System.Collections.Generic;
using System.Linq;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Site;

/// <summary>Seats and other comforts the player dots about the field. New kinds go on the end, so old saves still load.</summary>
public enum ComfortKind
{
    /// <summary>A straw bale to perch on. Nobody sits on it once it is wet.</summary>
    StrawBale,

    /// <summary>A wooden bench with a back, for three.</summary>
    Bench,

    /// <summary>Four striped deckchairs in a row: a sunny day favourite.</summary>
    Deckchairs,

    /// <summary>A picnic table with a bench down each side, for six.</summary>
    PicnicTable,

    /// <summary>A round table with four chairs under a big parasol.</summary>
    ParasolTable,

    /// <summary>An open-sided tent with four picnic tables in it: dry in the rain.</summary>
    SeatingMarquee,

    /// <summary>A post with a bottle of hand gel on it.</summary>
    HandGel,

    /// <summary>Two sinks with soap and a water tank.</summary>
    HandWash,

    /// <summary>A square of cloth on four poles: shade to stand under.</summary>
    ShadeSail,

    /// <summary>A little tent with lockers where a phone can be charged for a pound or two.</summary>
    ChargingTent,
}

/// <summary>
/// A place to sit on a comfort: where it is from the middle of the thing in metres when the thing
/// faces south (X east, Y south), which way the sitter faces in degrees clockwise from south, and
/// how high the seat is.
/// </summary>
public sealed record Seat(double X, double Y, double Turn, double Height);

/// <summary>
/// What there is to know about a comfort: its price, the tiles it stands on (across and front to
/// back when it faces south), its seats, whether people can walk under it, and a line for the player.
/// </summary>
public sealed record ComfortInfo(ComfortKind Kind, string Name, int Cost, int Width, int Depth, Seat[] Seats, string About)
{
    /// <summary>True for the things with a roof: a seat under one is dry in the rain and shady in the sun.</summary>
    public bool Roofed => Kind is ComfortKind.ParasolTable or ComfortKind.SeatingMarquee or ComfortKind.ShadeSail or ComfortKind.ChargingTent;

    /// <summary>True if people walk through it: only its poles are in the way, and those are thin enough to step round.</summary>
    public bool WalkUnder => Kind is ComfortKind.ShadeSail;
}

public static class Comforts
{
    private static readonly Seat[] None = Array.Empty<Seat>();

    /// <summary>Where the four tables stand in the seating marquee, in metres from its middle.</summary>
    public static readonly (double X, double Y)[] MarqueeTables = { (-2, -1.3), (2, -1.3), (-2, 1.3), (2, 1.3) };

    private static readonly ComfortInfo[] All =
    {
        new(ComfortKind.StrawBale, "Straw bale seat", 4, 2, 2, new[] { new Seat(-0.25, 0.05, 0, 0.44), new Seat(0.25, 0.05, 0, 0.44) },
            "A bale to perch on, for two. Nobody sits on wet straw."),
        new(ComfortKind.Bench, "Bench", 12, 4, 2, Row(3, 0.55, 0.02, 0, 0.45),
            "A wooden bench for three. A sit down lifts people's mood."),
        new(ComfortKind.Deckchairs, "Deckchairs", 15, 7, 3, Row(4, 0.8, 0.0, 0, 0.3),
            "Four deckchairs. Everybody wants one in the sun, and nobody in the rain."),
        new(ComfortKind.PicnicTable, "Picnic table", 25, 4, 4, Row(3, 0.6, -0.68, 0, 0.45).Concat(Row(3, 0.6, 0.68, 180, 0.45)).ToArray(),
            "Seats six. People who buy food nearby sit down to eat it, and leave their rubbish on the table."),
        new(ComfortKind.ParasolTable, "Parasol table", 40, 5, 5, new[] { new Seat(0, -0.8, 0, 0.44), new Seat(0.8, 0, 90, 0.44), new Seat(0, 0.8, 180, 0.44), new Seat(-0.8, 0, 270, 0.44) },
            "Seats four in the shade: a good place to cool down on a hot day."),
        new(ComfortKind.SeatingMarquee, "Seating marquee", 250, 16, 12, MarqueeSeats(),
            "Four picnic tables under a roof, for twenty-four. Dry in the rain, so fewer people give up and go home."),
        new(ComfortKind.HandGel, "Hand gel post", 8, 1, 1, None,
            "People passing clean their hands. A few less tummy upsets."),
        new(ComfortKind.HandWash, "Hand wash station", 30, 4, 2, None,
            "Two sinks with soap. Beside the toilets or the food it cuts sickness a good deal."),
        new(ComfortKind.ShadeSail, "Shade sail", 60, 10, 10, None,
            "A patch of shade to stand under on a hot day."),
        new(ComfortKind.ChargingTent, "Phone charging tent", 120, 6, 6, None,
            "People pay a little to charge their phones, and go away happier."),
    };

    public static IReadOnlyList<ComfortInfo> Everything => All;

    public static ComfortInfo Of(ComfortKind kind) => All[(int)kind];

    /// <summary>A row of seats side by side, centred, all facing the same way.</summary>
    private static Seat[] Row(int count, double apart, double y, double turn, double height) =>
        Enumerable.Range(0, count).Select(i => new Seat((i - ((count - 1) / 2.0)) * apart, y, turn, height)).ToArray();

    private static Seat[] MarqueeSeats() =>
        MarqueeTables
            .SelectMany(t => Row(3, 0.6, -0.68, 0, 0.45).Concat(Row(3, 0.6, 0.68, 180, 0.45)).Select(s => s with { X = s.X + t.X, Y = s.Y + t.Y }))
            .ToArray();
}
