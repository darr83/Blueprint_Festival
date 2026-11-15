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
    public bool Roofed => Kind is ComfortKind.ParasolTable or ComfortKind.SeatingMarquee or ComfortKind.ShadeSail;

    /// <summary>True for a table: somewhere to sit down and eat.</summary>
    public bool Table => Kind is ComfortKind.PicnicTable or ComfortKind.ParasolTable or ComfortKind.SeatingMarquee;

    /// <summary>True for somewhere to clean your hands.</summary>
    public bool Washes => Kind is ComfortKind.HandGel or ComfortKind.HandWash;
}

/// <summary>A comfort that has been put out. It faces the way its front looks.</summary>
public sealed record PlacedComfort(ComfortKind Kind, int X, int Y, Facing Facing)
{
    public TileRect Rect
    {
        get
        {
            var info = Comforts.Of(Kind);
            return Facing is Facing.South or Facing.North ? new TileRect(X, Y, info.Width, info.Depth) : new TileRect(X, Y, info.Depth, info.Width);
        }
    }

    /// <summary>The middle of it, in metres.</summary>
    public (double X, double Y) Middle
    {
        get
        {
            var r = Rect;
            return ((r.X + (r.Width / 2.0)) * FieldMap.TileMetres, (r.Y + (r.Height / 2.0)) * FieldMap.TileMetres);
        }
    }

    /// <summary>Turns a place given from the middle of the thing when it faces south into a place in the field, in metres.</summary>
    public (double X, double Y) Out(double x, double y)
    {
        var (mx, my) = Middle;
        return Facing switch
        {
            Facing.West => (mx - y, my + x),
            Facing.North => (mx - x, my - y),
            Facing.East => (mx + y, my - x),
            _ => (mx + x, my + y),
        };
    }

    /// <summary>The way the front of it looks, in radians: 0 is south, a quarter turn is east.</summary>
    public double Heading => Facing switch { Facing.West => -Math.PI / 2, Facing.North => Math.PI, Facing.East => Math.PI / 2, _ => 0 };

    /// <summary>Where a seat is in the field, in metres, and which way whoever sits on it faces.</summary>
    public (double X, double Y, double Heading) Place(Seat seat)
    {
        var (x, y) = Out(seat.X, seat.Y);
        return (x, y, Heading - (seat.Turn * Math.PI / 180));
    }

    /// <summary>The tiles nobody can walk through: all of it, but only the tables of a marquee and nothing under a sail.</summary>
    public IEnumerable<TilePoint> Solid()
    {
        var r = Rect;
        if (Kind == ComfortKind.ShadeSail)
        {
            yield break;
        }

        for (var y = r.Y; y < r.Bottom; y++)
        {
            for (var x = r.X; x < r.Right; x++)
            {
                if (Kind != ComfortKind.SeatingMarquee || UnderATable(x, y))
                {
                    yield return new TilePoint(x, y);
                }
            }
        }
    }

    private bool UnderATable(int x, int y)
    {
        var (px, py) = ((x + 0.5) * FieldMap.TileMetres, (y + 0.5) * FieldMap.TileMetres);
        foreach (var (tx, ty) in Comforts.MarqueeTables)
        {
            var (cx, cy) = Out(tx, ty);
            if (Math.Abs(px - cx) < 1.0 && Math.Abs(py - cy) < 1.0)
            {
                return true;
            }
        }

        return false;
    }
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
    };

    /// <summary>How far from a toilet or a food stall somewhere to wash still counts, in metres, measured between their middles.</summary>
    public const double WashReach = 14;

    /// <summary>What a gel post is worth against a proper sink.</summary>
    public const double GelWorth = 0.4;

    /// <summary>How much of the day's illness clean hands everywhere takes away.</summary>
    public const double CleanHandsSave = 0.35;

    /// <summary>The line comforts go under in the accounts.</summary>
    public const string Line = "Seats and comforts";

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

public sealed partial class SiteLayout
{
    private readonly List<PlacedComfort> _comforts = new();
    private byte[] _roof = Array.Empty<byte>();
    private bool _roofStale = true;

    /// <summary>The seats, hand washing and shade that have been put out.</summary>
    public IReadOnlyList<PlacedComfort> Comforts => _comforts;

    public PlacedComfort? ComfortAt(TilePoint p) => _comforts.FirstOrDefault(c => c.Rect.Contains(p));

    /// <summary>Whether a comfort can go with its corner on a tile, and what it costs.</summary>
    public BuildCheck CheckComfort(ComfortKind kind, int x, int y, Facing facing)
    {
        var comfort = new PlacedComfort(kind, x, y, facing);
        foreach (var p in TilesOf(comfort.Rect))
        {
            if (!_map.InBounds(p.X, p.Y) || !_map.Rented(p) || !IsBuildable(p))
            {
                return BuildCheck.No("It must stand on the grass inside the hedge.");
            }

            if (!IsEmpty(p))
            {
                return BuildCheck.No("Something is already built there.");
            }
        }

        return new BuildCheck(true, Site.Comforts.Of(kind).Cost, null);
    }

    public BuildCheck AddComfort(ComfortKind kind, int x, int y, Facing facing)
    {
        var check = CheckComfort(kind, x, y, facing);
        if (check.Ok)
        {
            _comforts.Add(new PlacedComfort(kind, x, y, facing));
            Refresh();
        }

        return check;
    }

    /// <summary>What taking away the comfort on a tile would give back, or 0 if there is none.</summary>
    public int ComfortRefundAt(TilePoint p) => ComfortAt(p) is { } comfort ? Site.Comforts.Of(comfort.Kind).Cost : 0;

    private bool RemoveComfortAt(TilePoint p) => ComfortAt(p) is { } comfort && _comforts.Remove(comfort);

    private IEnumerable<TilePoint> ComfortsInTheWay => _comforts.SelectMany(c => c.Solid());

    private void RestoreComforts(SiteSave save)
    {
        foreach (var c in save.Comforts ?? new())
        {
            if (Enum.IsDefined(c.Kind) && CheckComfort(c.Kind, c.X, c.Y, c.Facing).Ok)
            {
                _comforts.Add(c);
            }
        }
    }

    /// <summary>True if a tile is under a parasol, a shade sail or the roof of the seating marquee.</summary>
    public bool RoofOver(TilePoint p)
    {
        if (_comforts.Count == 0 || !_map.InBounds(p.X, p.Y))
        {
            return false;
        }

        if (_roofStale)
        {
            _roofStale = false;
            if (_roof.Length != _map.Width * _map.Height)
            {
                _roof = new byte[_map.Width * _map.Height];
            }
            else
            {
                Array.Clear(_roof);
            }

            foreach (var c in _comforts)
            {
                if (Site.Comforts.Of(c.Kind).Roofed)
                {
                    foreach (var t in TilesOf(c.Rect))
                    {
                        _roof[(t.Y * _map.Width) + t.X] = 1;
                    }
                }
            }
        }

        return _roof[(p.Y * _map.Width) + p.X] != 0;
    }

    /// <summary>True if a place, in metres, is under a roof the player has put up.</summary>
    public bool RoofOverPlace(double x, double y) => RoofOver(FieldMap.TileAt(x, y));

    /// <summary>
    /// How clean hands are kept at a place, in metres: 1 with a hand wash station within reach, part of
    /// that with only a gel post, 0 with neither.
    /// </summary>
    public double CleanHandsNear(double x, double y)
    {
        var best = 0.0;
        foreach (var c in _comforts)
        {
            var worth = c.Kind == ComfortKind.HandWash ? 1.0 : c.Kind == ComfortKind.HandGel ? Site.Comforts.GelWorth : 0;
            if (worth > best)
            {
                var (mx, my) = c.Middle;
                if (Math.Sqrt(((mx - x) * (mx - x)) + ((my - y) * (my - y))) <= Site.Comforts.WashReach)
                {
                    best = worth;
                }
            }
        }

        return best;
    }

    /// <summary>
    /// How well the festival looks after clean hands, from 0 to 1: the share of its toilets and food
    /// stalls with somewhere to wash within reach (a gel post counts for less than a sink).
    /// </summary>
    public double Hygiene
    {
        get
        {
            var places = 0;
            var clean = 0.0;
            foreach (var s in _stalls)
            {
                var info = Catalogue.Of(s.Kind);
                if (info.Serves is Need.Toilet or Need.Food)
                {
                    places++;
                    var r = s.Rect;
                    clean += CleanHandsNear((r.X + (r.Width / 2.0)) * FieldMap.TileMetres, (r.Y + (r.Height / 2.0)) * FieldMap.TileMetres);
                }
            }

            return places == 0 ? 0 : clean / places;
        }
    }
}
