namespace BlueprintFestival.Simulation.World;

/// <summary>One of the fields a festival can be held on: its name, the biggest licence it takes, its size and the width of the way in.</summary>
public sealed record FieldSize(string Name, int Holds, int MetresWide, int MetresDeep, int GapMetres);

/// <summary>A position on the tile grid.</summary>
public readonly record struct TilePoint(int X, int Y);

/// <summary>A rectangle of tiles: X and Y are its top-left tile.</summary>
public readonly record struct TileRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    public bool Contains(TilePoint p) => p.X >= X && p.X < Right && p.Y >= Y && p.Y < Bottom;
}

/// <summary>What the ground is at one tile.</summary>
public enum Ground : byte
{
    /// <summary>Farmland outside the festival field. People do not walk here.</summary>
    Countryside = 0,

    /// <summary>The grass of the festival field.</summary>
    Grass = 1,

    /// <summary>The lane people arrive along, and the track from it into the field.</summary>
    Lane = 2,

    /// <summary>The hedge round the field. Nobody gets through a hedge.</summary>
    Hedge = 3,
}

/// <summary>
/// The site as a grid of half-metre tiles: a farmer's field with a hedge round it, a lane running
/// past, and a gap in the hedge where a track comes in off the lane.
/// </summary>
public sealed class FieldMap
{
    /// <summary>Tiles are half a metre square, as in Blueprint Hospital.</summary>
    public const double TileMetres = 0.5;

    private readonly Ground[] _tiles;
    private readonly bool[] _blocked;

    private FieldMap(int width, int height)
    {
        Width = width;
        Height = height;
        _tiles = new Ground[width * height];
        _blocked = new bool[width * height];
    }

    /// <summary>Width of the whole map in tiles (west to east).</summary>
    public int Width { get; }

    /// <summary>Height of the whole map in tiles (north to south).</summary>
    public int Height { get; }

    public double WidthMetres => Width * TileMetres;

    public double HeightMetres => Height * TileMetres;

    /// <summary>The grass inside the hedge.</summary>
    public TileRect Field { get; private init; }

    /// <summary>The hedge and everything inside it.</summary>
    public TileRect HedgeLine { get; private init; }

    /// <summary>The gap in the hedge where the track comes in.</summary>
    public TileRect Gap { get; private init; }

    /// <summary>The lane across the map.</summary>
    public TileRect Lane { get; private init; }

    /// <summary>The track from the lane to the gap.</summary>
    public TileRect Track { get; private init; }

    /// <summary>Where people come onto the map: the west end of the lane.</summary>
    public TilePoint LaneStart => new(0, Lane.Y + (Lane.Height / 2));

    public Ground this[int x, int y] => InBounds(x, y) ? _tiles[(y * Width) + x] : Ground.Countryside;

    public Ground this[TilePoint p] => this[p.X, p.Y];

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    /// <summary>True where people can walk: grass or lane with nothing built on it.</summary>
    public bool IsWalkable(int x, int y) => this[x, y] is Ground.Grass or Ground.Lane && !_blocked[(y * Width) + x];

    /// <summary>True where something built stands in the way.</summary>
    public bool IsBlocked(TilePoint p) => InBounds(p.X, p.Y) && _blocked[(p.Y * Width) + p.X];

    /// <summary>Says which tiles have something built on them that people must walk round.</summary>
    public void SetBlocked(IEnumerable<TilePoint> tiles)
    {
        Array.Clear(_blocked);
        foreach (var p in tiles)
        {
            if (InBounds(p.X, p.Y))
            {
                _blocked[(p.Y * Width) + p.X] = true;
            }
        }
    }

    public bool IsWalkable(TilePoint p) => IsWalkable(p.X, p.Y);

    /// <summary>The tile a point in metres falls in.</summary>
    public static TilePoint TileAt(double xMetres, double yMetres) =>
        new((int)Math.Floor(xMetres / TileMetres), (int)Math.Floor(yMetres / TileMetres));

    /// <summary>The middle of a tile, in metres.</summary>
    public static (double X, double Y) CentreOf(TilePoint tile) =>
        ((tile.X + 0.5) * TileMetres, (tile.Y + 0.5) * TileMetres);

    /// <summary>How many people the field's licence could allow at two square metres each.</summary>
    public int ComfortableCapacity => (int)(Field.Width * TileMetres * Field.Height * TileMetres / 2);

    /// <summary>
    /// The village field for a first festival: 60 m by 40 m of grass (room for a few hundred people),
    /// with the lane along the south side and the way in near the south-west corner.
    /// </summary>
    public static FieldMap Village() => Create(fieldMetresWide: 60, fieldMetresDeep: 40);

    /// <summary>The three fields a festival can be held on, smallest first.</summary>
    public static readonly IReadOnlyList<FieldSize> Sizes = new[]
    {
        new FieldSize("the village field", 400, 60, 40, 6),
        new FieldSize("Home Farm meadow", 1500, 110, 70, 12),
        new FieldSize("the showground", 5000, 180, 120, 24),
    };

    /// <summary>The smallest field that a licence for so many people fits on.</summary>
    public static FieldSize SizeFor(int licence) => Sizes.FirstOrDefault(s => licence <= s.Holds) ?? Sizes[^1];

    /// <summary>The field for a licence: bigger crowds are given more grass and a wider way in.</summary>
    public static FieldMap ForLicence(int licence)
    {
        var size = SizeFor(licence);
        return Create(size.MetresWide, size.MetresDeep, size.GapMetres);
    }

    public static FieldMap Create(int fieldMetresWide, int fieldMetresDeep) => Create(fieldMetresWide, fieldMetresDeep, 6);

    public static FieldMap Create(int fieldMetresWide, int fieldMetresDeep, int gapMetres)
    {
        if (fieldMetresWide < 20 || fieldMetresDeep < 20)
        {
            throw new ArgumentOutOfRangeException(nameof(fieldMetresWide), "A festival field must be at least 20 m each way.");
        }

        const int margin = 24;      // 12 m of countryside round the hedge
        const int hedge = 3;        // a 1.5 m thick hedge
        const int laneWidth = 10;   // a 5 m lane
        var gapWidth = Math.Clamp(gapMetres, 4, fieldMetresWide / 2) * 2;
        const int laneGap = 16;     // 8 m of verge between hedge and lane

        var fieldW = fieldMetresWide * 2;
        var fieldH = fieldMetresDeep * 2;
        var width = fieldW + (2 * (hedge + margin));
        var height = margin + hedge + fieldH + hedge + laneGap + laneWidth + margin;

        var field = new TileRect(margin + hedge, margin + hedge, fieldW, fieldH);
        var hedgeLine = new TileRect(margin, margin, fieldW + (2 * hedge), fieldH + (2 * hedge));
        var lane = new TileRect(0, hedgeLine.Bottom + laneGap, width, laneWidth);
        var gap = new TileRect(field.X + (fieldW / 5), field.Bottom, gapWidth, hedge);
        var track = new TileRect(gap.X, hedgeLine.Bottom, gapWidth, laneGap);

        var map = new FieldMap(width, height) { Field = field, HedgeLine = hedgeLine, Gap = gap, Lane = lane, Track = track };
        map.Fill(hedgeLine, Ground.Hedge);
        map.Fill(field, Ground.Grass);
        map.Fill(gap, Ground.Grass);
        map.Fill(lane, Ground.Lane);
        map.Fill(track, Ground.Lane);
        return map;
    }

    private void Fill(TileRect area, Ground ground)
    {
        for (var y = area.Y; y < area.Bottom; y++)
        {
            for (var x = area.X; x < area.Right; x++)
            {
                _tiles[(y * Width) + x] = ground;
            }
        }
    }
}
