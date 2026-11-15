namespace BlueprintFestival.Simulation.World;

/// <summary>One of the fields a festival can be held on: its name, the biggest licence it takes, its size and the width of the way in.</summary>
public sealed record FieldSize(string Name, int Holds, int MetresWide, int MetresDeep, int GapMetres, int CampMetres = 0);

/// <summary>A rented field on the day's map: which field, what it is for, and its grass in tiles.</summary>
public sealed record MapField(EstateField Info, FieldUse Use, TileRect Grass);

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
    private readonly bool[] _staffOnly;
    private readonly bool[] _vipOnly;

    private FieldMap(int width, int height)
    {
        Width = width;
        Height = height;
        _tiles = new Ground[width * height];
        _blocked = new bool[width * height];
        _staffOnly = new bool[width * height];
        _vipOnly = new bool[width * height];
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

    /// <summary>The grass of the camping field next door, or an empty rectangle if this field has none.</summary>
    public TileRect Camp { get; private init; }

    /// <summary>The hedge round the camping field, sharing the main field's east hedge.</summary>
    public TileRect CampHedge { get; private init; }

    /// <summary>The gateway through the east hedge into the camping field.</summary>
    public TileRect CampGap { get; private init; }

    public bool HasCamp => Camp.Width > 0;

    /// <summary>The rented fields this map was made from, or none for a single field made the old way.</summary>
    public IReadOnlyList<MapField> Fields { get; private init; } = Array.Empty<MapField>();

    /// <summary>True for a map made from a plan of rented fields.</summary>
    public bool FromPlan => Fields.Count > 0;

    /// <summary>The gateways cut through the hedges between rented fields.</summary>
    public IReadOnlyList<TileRect> Gateways { get; private init; } = Array.Empty<TileRect>();

    /// <summary>Every stretch of track off the lane: the way in, and the way out of each car park.</summary>
    public IReadOnlyList<TileRect> Tracks { get; private init; } = Array.Empty<TileRect>();

    private readonly List<(TileRect Camp, TilePoint Door, bool AlongX)> _campDoors = new();

    /// <summary>
    /// For a tile in a gap through a hedge (the way in, a gateway between fields or the way into
    /// the camping field): true if the gap runs north to south, so a gate across it stands
    /// up and down the map. Null anywhere else.
    /// </summary>
    public bool? GapRunsNorthSouth(TilePoint p)
    {
        foreach (var gap in Gateways.Append(Gap).Append(CampGap))
        {
            if (gap.Width > 0 && gap.Height > 0 && gap.Contains(p))
            {
                return gap.Height > gap.Width;
            }
        }

        return null;
    }

    /// <summary>What the field at a tile is for, or null off the rented grass.</summary>
    public FieldUse? UseAt(TilePoint p)
    {
        if (!FromPlan)
        {
            return Field.Contains(p) ? FieldUse.Music : HasCamp && Camp.Contains(p) ? FieldUse.Camping : null;
        }

        foreach (var f in Fields)
        {
            if (f.Grass.Contains(p))
            {
                return f.Use;
            }
        }

        return null;
    }

    /// <summary>True on the grass of the festival itself: the fields for music and food, and the gateways between them.</summary>
    public bool InFestival(TilePoint p)
    {
        if (!FromPlan)
        {
            return Field.Contains(p);
        }

        if (UseAt(p) is { } use)
        {
            return use is FieldUse.Music or FieldUse.Food;
        }

        return this[p] == Ground.Grass && Field.Contains(p) && Gateways.Any(g => g.Contains(p));
    }

    private readonly List<(int A, int B, TileRect Door)> _doors = new();

    /// <summary>Where the bus stops on the lane, or null on a map with no bus stop.</summary>
    public TilePoint? BusStop { get; private init; }

    /// <summary>A parking bay is 2.5 m wide and 5 m long.</summary>
    public const int BayWide = 5;

    public const int BayLong = 10;

    /// <summary>
    /// The parking bays in every car park, nearest its way out first: the middle tile of each.
    /// Bays are in double rows with a lane to drive along between them, and each car park has
    /// as many as its field holds.
    /// </summary>
    public IReadOnlyList<TilePoint> ParkingBays()
    {
        var bays = new List<TilePoint>();
        foreach (var park in Fields.Where(f => f.Use == FieldUse.CarPark && f.Info.OnLane))
        {
            var g = park.Grass;
            var mine = new List<TilePoint>();
            // Two rows nose to nose, then a 6 m lane, all the way up the field, and one last row if there is room.
            for (var y = g.Y + 2; y + BayLong <= g.Bottom - 2; y += BayLong)
            {
                var row = (y - g.Y - 2) % ((2 * BayLong) + 12);
                if (row >= 2 * BayLong)
                {
                    y += 12 - BayLong;
                    continue;
                }

                for (var x = g.X + 2; x + BayWide <= g.Right - 2; x += BayWide)
                {
                    mine.Add(new TilePoint(x + (BayWide / 2), y + (BayLong / 2)));
                }
            }

            var north = park.Info.Bottom <= Estate.LaneTop;
            var door = new TilePoint(g.X + (g.Width / 2), north ? g.Bottom : g.Y);
            bays.AddRange(mine.OrderBy(b => Math.Abs(b.X - door.X) + Math.Abs(b.Y - door.Y)).ThenBy(b => b.Y).ThenBy(b => b.X).Take(park.Info.RoomFor(FieldUse.CarPark)));
        }

        return bays;
    }

    /// <summary>Which rented field a tile is in, counting a gateway as one of the fields it joins; -1 out on the lane.</summary>
    private int RegionOf(TilePoint p)
    {
        for (var i = 0; i < Fields.Count; i++)
        {
            if (Fields[i].Grass.Contains(p))
            {
                return i;
            }
        }

        foreach (var (a, _, door) in _doors)
        {
            if (door.Contains(p))
            {
                return a;
            }
        }

        return -1;
    }

    /// <summary>
    /// The gateways to walk through, in order, to get from one tile to another on a map made from
    /// a plan: none if both are in the same field. Each is crossed where the straight line would
    /// cross it, kept inside the gateway, so a crowd spreads across its width.
    /// </summary>
    public IReadOnlyList<TilePoint> Waypoints(TilePoint from, TilePoint to)
    {
        if (!FromPlan)
        {
            return Array.Empty<TilePoint>();
        }

        var (start, end) = (RegionOf(from), RegionOf(to));
        if (start == end)
        {
            return Array.Empty<TilePoint>();
        }

        // Breadth first over the fields, from the far end, so the doors come out in walking order.
        var via = new Dictionary<int, (int Next, TileRect Door)>();
        var waiting = new Queue<int>();
        waiting.Enqueue(end);
        var seen = new HashSet<int> { end };
        while (waiting.Count > 0 && !via.ContainsKey(start))
        {
            var at = waiting.Dequeue();
            foreach (var (a, b, door) in _doors)
            {
                var other = a == at ? b : b == at ? a : int.MinValue;
                if (other != int.MinValue && seen.Add(other))
                {
                    via[other] = (at, door);
                    waiting.Enqueue(other);
                }
            }
        }

        var points = new List<TilePoint>();
        var here = from;
        for (var region = start; region != end && via.TryGetValue(region, out var step); region = step.Next)
        {
            var d = step.Door;
            TilePoint through;
            if (d.Height >= d.Width)
            {
                // A gateway in a hedge that runs north to south: cross at the height the straight line would.
                var x = d.X + (d.Width / 2);
                var t = to.X == here.X ? 0.5 : Math.Clamp((x - here.X) / (double)(to.X - here.X), 0, 1);
                through = new TilePoint(x, Math.Clamp((int)Math.Round(here.Y + (t * (to.Y - here.Y))), d.Y + 2, d.Bottom - 3));
            }
            else
            {
                var y = d.Y + (d.Height / 2);
                var t = to.Y == here.Y ? 0.5 : Math.Clamp((y - here.Y) / (double)(to.Y - here.Y), 0, 1);
                through = new TilePoint(Math.Clamp((int)Math.Round(here.X + (t * (to.X - here.X))), d.X + 2, d.Right - 3), y);
            }

            points.Add(through);
            here = through;
        }

        return points;
    }

    /// <summary>True on any grass that has been rented, whatever it is for: where stalls can be built and staff can stand.</summary>
    public bool Rented(TilePoint p) => FromPlan ? UseAt(p) is not null : Field.Contains(p);

    /// <summary>The hedges as a few rectangles, for drawing.</summary>
    public IReadOnlyList<TileRect> HedgeRuns()
    {
        var runs = new List<TileRect>();
        var done = new bool[Width * Height];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                if (done[(y * Width) + x] || _tiles[(y * Width) + x] != Ground.Hedge)
                {
                    continue;
                }

                var w = 0;
                while (x + w < Width && !done[(y * Width) + x + w] && _tiles[(y * Width) + x + w] == Ground.Hedge)
                {
                    w++;
                }

                var h = 1;
                while (y + h < Height && Enumerable.Range(x, w).All(c => !done[((y + h) * Width) + c] && _tiles[((y + h) * Width) + c] == Ground.Hedge)
                    && (x == 0 || _tiles[((y + h) * Width) + x - 1] != Ground.Hedge || done[((y + h) * Width) + x - 1])
                    && (x + w >= Width || _tiles[((y + h) * Width) + x + w] != Ground.Hedge))
                {
                    h++;
                }

                for (var yy = y; yy < y + h; yy++)
                {
                    for (var xx = x; xx < x + w; xx++)
                    {
                        done[(yy * Width) + xx] = true;
                    }
                }

                runs.Add(new TileRect(x, y, w, h));
            }
        }

        return runs;
    }

    /// <summary>Where people come onto the map: the west end of the lane.</summary>
    public TilePoint LaneStart => new(0, Lane.Y + (Lane.Height / 2));

    public Ground this[int x, int y] => InBounds(x, y) ? _tiles[(y * Width) + x] : Ground.Countryside;

    public Ground this[TilePoint p] => this[p.X, p.Y];

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    /// <summary>True where people can walk: grass or lane with nothing built on it.</summary>
    public bool IsWalkable(int x, int y) => this[x, y] is Ground.Grass or Ground.Lane && !_blocked[(y * Width) + x] && !_staffOnly[(y * Width) + x] && !_vipOnly[(y * Width) + x];

    /// <summary>True where VIP guests can walk: anywhere the public can, and the ground marked for VIPs.</summary>
    public bool IsWalkableForVip(int x, int y) => this[x, y] is Ground.Grass or Ground.Lane && !_blocked[(y * Width) + x] && !_staffOnly[(y * Width) + x];

    public bool IsWalkableForVip(TilePoint p) => IsWalkableForVip(p.X, p.Y);

    /// <summary>True on ground marked for VIP guests only.</summary>
    public bool IsVipOnly(TilePoint p) => InBounds(p.X, p.Y) && _vipOnly[(p.Y * Width) + p.X];

    /// <summary>Says which tiles only VIP guests (and staff) may walk on.</summary>
    public void SetVipOnly(IEnumerable<TilePoint> tiles)
    {
        Array.Clear(_vipOnly);
        HasVipOnly = false;
        foreach (var p in tiles)
        {
            if (InBounds(p.X, p.Y))
            {
                _vipOnly[(p.Y * Width) + p.X] = true;
                HasVipOnly = true;
            }
        }
    }

    /// <summary>True if any tile at all is kept for VIP guests.</summary>
    public bool HasVipOnly { get; private set; }

    /// <summary>True where staff can walk: anywhere the public can, and the ground the public are kept off.</summary>
    public bool IsWalkableForStaff(int x, int y) => this[x, y] is Ground.Grass or Ground.Lane && !_blocked[(y * Width) + x];

    public bool IsWalkableForStaff(TilePoint p) => IsWalkableForStaff(p.X, p.Y);

    /// <summary>Says which tiles the public are kept off.</summary>
    public void SetStaffOnly(IEnumerable<TilePoint> tiles)
    {
        Array.Clear(_staffOnly);
        foreach (var p in tiles)
        {
            if (InBounds(p.X, p.Y))
            {
                _staffOnly[(p.Y * Width) + p.X] = true;
            }
        }
    }

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

    /// <summary>The closest two tents are pitched, centre to centre, in tiles (3.4 m).</summary>
    public const double PitchSpacing = 6.8;

    /// <summary>
    /// The pitches in the camping field, nearest the gateway first: one tent each, dotted about the
    /// field wherever there is room, never closer than <see cref="PitchSpacing"/> to the next one.
    /// The way in from the gateway is kept clear right across the field. The same field always
    /// gets the same pitches.
    /// </summary>
    public IReadOnlyList<TilePoint> Pitches()
    {
        if (FromPlan)
        {
            var all = new List<TilePoint>();
            foreach (var (camp, door, alongX) in _campDoors)
            {
                all.AddRange(PitchesIn(camp, door, alongX));
            }

            return all;
        }

        return HasCamp ? PitchesIn(Camp, new TilePoint(Camp.X, CampGap.Y + (CampGap.Height / 2)), true) : new List<TilePoint>();
    }

    /// <summary>
    /// The pitches in one camping field, nearest its gateway first. The way in from the gateway is
    /// kept clear right across the field: along X for a gateway in an east or west hedge, along Y
    /// for one in a north or south hedge.
    /// </summary>
    private static List<TilePoint> PitchesIn(TileRect camp, TilePoint door, bool alongX)
    {
        var pitches = new List<TilePoint>();
        var middle = alongX ? door.Y : door.X;
        var random = Core.GameRandom.FromSeed((camp.Width * 7919) + camp.Height, 31);
        var (left, top, right, bottom) = (camp.X + 5, camp.Y + 5, camp.Right - 5, camp.Bottom - 5);

        // Tents are tried in random places over and over; one goes up wherever it is not too close
        // to another. A grid of buckets, each a little bigger than the spacing, keeps the looking quick.
        var bucket = (int)Math.Ceiling(PitchSpacing);
        var across = ((right - left) / bucket) + 1;
        var down = ((bottom - top) / bucket) + 1;
        var buckets = new List<TilePoint>?[across * down];
        var tries = (right - left) * (bottom - top) / 2;
        for (var i = 0; i < tries; i++)
        {
            var x = left + random.Next(right - left);
            var y = top + random.Next(bottom - top);

            // The way in from the gateway wanders a little across the field, with a ragged edge where the tents stop.
            var (along, side) = alongX ? (x, y) : (y, x);
            if (Math.Abs(side - middle - (3 * Math.Sin(along * 0.09))) < 9 + ((along * 7) % 4))
            {
                continue;
            }

            var bx = (x - left) / bucket;
            var by = (y - top) / bucket;
            var near = false;
            for (var cy = Math.Max(0, by - 1); cy <= Math.Min(down - 1, by + 1) && !near; cy++)
            {
                for (var cx = Math.Max(0, bx - 1); cx <= Math.Min(across - 1, bx + 1) && !near; cx++)
                {
                    foreach (var p in buckets[(cy * across) + cx] ?? Enumerable.Empty<TilePoint>())
                    {
                        if (((p.X - x) * (p.X - x)) + ((p.Y - y) * (p.Y - y)) < PitchSpacing * PitchSpacing)
                        {
                            near = true;
                            break;
                        }
                    }
                }
            }

            if (!near)
            {
                var pitch = new TilePoint(x, y);
                (buckets[(by * across) + bx] ??= new List<TilePoint>()).Add(pitch);
                pitches.Add(pitch);
            }
        }

        return pitches.OrderBy(p => Math.Abs(p.X - door.X) + Math.Abs(p.Y - door.Y)).ThenBy(p => p.Y).ThenBy(p => p.X).ToList();
    }

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
        new FieldSize("the showground", 5000, 180, 120, 24, 140),
    };

    /// <summary>The smallest field that a licence for so many people fits on.</summary>
    public static FieldSize SizeFor(int licence) => Sizes.FirstOrDefault(s => licence <= s.Holds) ?? Sizes[^1];

    /// <summary>The field for a licence: bigger crowds are given more grass and a wider way in.</summary>
    public static FieldMap ForLicence(int licence)
    {
        var size = SizeFor(licence);
        return Create(size.MetresWide, size.MetresDeep, size.GapMetres, size.CampMetres);
    }

    public static FieldMap Create(int fieldMetresWide, int fieldMetresDeep) => Create(fieldMetresWide, fieldMetresDeep, 6);

    public static FieldMap Create(int fieldMetresWide, int fieldMetresDeep, int gapMetres) => Create(fieldMetresWide, fieldMetresDeep, gapMetres, 0);

    /// <summary>A field with, if asked for, a camping field through the hedge on its east side.</summary>
    public static FieldMap Create(int fieldMetresWide, int fieldMetresDeep, int gapMetres, int campMetresWide)
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
        var campW = Math.Max(0, campMetresWide) * 2;
        var width = fieldW + (2 * (hedge + margin)) + (campW > 0 ? campW + hedge : 0);
        var height = margin + hedge + fieldH + hedge + laneGap + laneWidth + margin;

        var field = new TileRect(margin + hedge, margin + hedge, fieldW, fieldH);
        var hedgeLine = new TileRect(margin, margin, fieldW + (2 * hedge), fieldH + (2 * hedge));
        var lane = new TileRect(0, hedgeLine.Bottom + laneGap, width, laneWidth);
        var gap = new TileRect(field.X + (fieldW / 5), field.Bottom, gapWidth, hedge);
        var track = new TileRect(gap.X, hedgeLine.Bottom, gapWidth, laneGap);

        // The camping field shares the east hedge, with a gateway through it halfway up.
        const int campGate = 16;    // an 8 m gateway
        var camp = campW > 0 ? new TileRect(hedgeLine.Right, field.Y, campW, fieldH) : default;
        var campHedge = campW > 0 ? new TileRect(field.Right, margin, campW + (2 * hedge), fieldH + (2 * hedge)) : default;
        var campGap = campW > 0 ? new TileRect(field.Right, field.Y + ((fieldH - campGate) / 2), hedge, campGate) : default;

        var map = new FieldMap(width, height) { Field = field, HedgeLine = hedgeLine, Gap = gap, Lane = lane, Track = track, Camp = camp, CampHedge = campHedge, CampGap = campGap };
        map.Fill(campHedge, Ground.Hedge);
        map.Fill(hedgeLine, Ground.Hedge);
        map.Fill(camp, Ground.Grass);
        map.Fill(field, Ground.Grass);
        map.Fill(gap, Ground.Grass);
        map.Fill(campGap, Ground.Grass);
        map.Fill(lane, Ground.Lane);
        map.Fill(track, Ground.Lane);
        return map;
    }

    /// <summary>
    /// The site for a plan of rented fields: each field's grass with a hedge round it, a gateway
    /// through every hedge two festival fields share, the lane, and a track from the lane to the
    /// way in. Car parks have a hedge and a way out to the lane of their own. Fields not rented are
    /// left as farmland. The map is cut to the rented fields and the lane beside them.
    /// </summary>
    public static FieldMap ForPlan(FieldPlan plan, int licence)
    {
        var rented = plan.Fields.ToList();
        if (rented.Count == 0 || plan.WayIn is not { } wayIn)
        {
            throw new ArgumentException("A plan needs a festival field that touches the lane or the farm track.", nameof(plan));
        }

        const int margin = 24;      // 12 m of countryside round everything
        const int hedge = Estate.HedgeMetres * 2;
        const int laneY = 248;      // the lane is 5 m wide with a verge either side
        const int laneDeep = 5;
        const int stripX = 248;     // the farm track is 6 m wide
        const int stripWide = 6;

        var byTrack = !wayIn.OnLane;
        var x0 = rented.Min(f => f.X);
        var x1 = Math.Max(rented.Max(f => f.Right), Estate.BusStopX + 12);
        x0 = Math.Min(x0, Estate.BusStopX - 12);
        var y0 = Math.Min(rented.Min(f => f.Y), Estate.LaneTop);
        var y1 = Math.Max(rented.Max(f => f.Bottom), Estate.LaneBottom);
        if (byTrack)
        {
            x0 = Math.Min(x0, Estate.TrackLeft);
            x1 = Math.Max(x1, Estate.TrackRight);
        }

        var ox = x0 - Estate.HedgeMetres - (margin / 2);
        var oy = y0 - Estate.HedgeMetres - (margin / 2);
        var width = ((x1 - x0) * 2) + (2 * (hedge + margin));
        var height = ((y1 - y0) * 2) + (2 * (hedge + margin));
        TileRect Tiles(int mx, int my, int mw, int mh) => new((mx - ox) * 2, (my - oy) * 2, mw * 2, mh * 2);
        static TileRect Grow(TileRect r, int by) => new(r.X - by, r.Y - by, r.Width + (2 * by), r.Height + (2 * by));
        static TileRect Around(IEnumerable<TileRect> rects)
        {
            var list = rects.ToList();
            var (l, t) = (list.Min(r => r.X), list.Min(r => r.Y));
            return new TileRect(l, t, list.Max(r => r.Right) - l, list.Max(r => r.Bottom) - t);
        }

        var fields = rented.Select(f => new MapField(f, plan.UseOf(f.Id)!.Value, Tiles(f.X, f.Y, f.MetresWide, f.MetresDeep))).ToList();
        var grassOf = fields.ToDictionary(f => f.Info.Id, f => f.Grass);
        var lane = new TileRect(0, (laneY - oy) * 2, width, laneDeep * 2);
        var gateways = new List<TileRect>();
        var tracks = new List<TileRect>();
        var gapWide = Math.Clamp(licence <= 400 ? 6 : licence <= 1500 ? 12 : 24, 4, wayIn.MetresWide / 2) * 2;

        // A gateway through every hedge two festival fields share, wider for a bigger crowd.
        var festival = plan.Festival.ToList();
        var campDoors = new List<(TileRect Camp, TilePoint Door, bool AlongX)>();
        var doors = new List<(int A, int B, TileRect Door)>();
        for (var i = 0; i < festival.Count; i++)
        {
            for (var j = i + 1; j < festival.Count; j++)
            {
                var (a, b) = (festival[i], festival[j]);
                if (Estate.Gateway(a, b) is not { } g)
                {
                    continue;
                }

                var shared = g.NorthSouth ? Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Y, b.Y) : Math.Min(a.Right, b.Right) - Math.Max(a.X, b.X);
                var wide = Math.Clamp(licence / 200, 6, Math.Min(24, shared - 4)) * 2;
                var cx = (int)Math.Round((g.X - ox) * 2);
                var cy = (int)Math.Round((g.Y - oy) * 2);
                var gate = g.NorthSouth ? new TileRect(cx - (hedge / 2), cy - (wide / 2), hedge, wide) : new TileRect(cx - (wide / 2), cy - (hedge / 2), wide, hedge);
                gateways.Add(gate);
                doors.Add((rented.IndexOf(a), rented.IndexOf(b), gate));
                foreach (var camp in new[] { a, b }.Where(f => plan.UseOf(f.Id) == FieldUse.Camping && campDoors.All(d => d.Camp != grassOf[f.Id])))
                {
                    var grass = grassOf[camp.Id];
                    var door = new TilePoint(Math.Clamp(cx, grass.X, grass.Right - 1), Math.Clamp(cy, grass.Y, grass.Bottom - 1));
                    campDoors.Add((grass, door, g.NorthSouth));
                }
            }
        }

        // The way in: a gap in the hedge by the lane with a track to it, or a gap onto the farm track.
        var inGrass = grassOf[wayIn.Id];
        TileRect gap;
        TileRect track;
        if (!byTrack)
        {
            var north = wayIn.Bottom <= Estate.LaneTop;
            var gx = inGrass.X + (inGrass.Width / 5);
            gap = north ? new TileRect(gx, inGrass.Bottom, gapWide, hedge) : new TileRect(gx, inGrass.Y - hedge, gapWide, hedge);
            track = north ? new TileRect(gx, gap.Bottom, gapWide, lane.Y - gap.Bottom) : new TileRect(gx, lane.Bottom, gapWide, gap.Y - lane.Bottom);
            tracks.Add(track);
        }
        else
        {
            var west = wayIn.Right <= Estate.TrackLeft;
            var tall = Math.Min(gapWide, inGrass.Height / 2);
            var gy = inGrass.Bottom - (inGrass.Height / 5) - tall;
            gap = west ? new TileRect(inGrass.Right, gy, hedge, tall) : new TileRect(inGrass.X - hedge, gy, hedge, tall);
            var strip = Tiles(stripX, 0, stripWide, 1);
            var farm = new TileRect(strip.X, gy, strip.Width, lane.Y - gy);
            track = west ? new TileRect(gap.Right, gy, farm.X - gap.Right, tall) : new TileRect(farm.Right, gy, gap.X - farm.Right, tall);
            tracks.Add(track);
            tracks.Add(farm);
        }

        // Each car park has its own way out onto the lane.
        foreach (var park in fields.Where(f => f.Use == FieldUse.CarPark && f.Info.OnLane))
        {
            var north = park.Info.Bottom <= Estate.LaneTop;
            var px = park.Grass.X + (park.Grass.Width / 2) - 8;
            var exit = north ? new TileRect(px, park.Grass.Bottom, 16, hedge) : new TileRect(px, park.Grass.Y - hedge, 16, hedge);
            gateways.Add(exit);
            doors.Add((fields.IndexOf(park), -1, exit));
            tracks.Add(north ? new TileRect(px, park.Grass.Bottom + hedge, 16, lane.Y - park.Grass.Bottom - hedge) : new TileRect(px, lane.Bottom, 16, park.Grass.Y - hedge - lane.Bottom));
        }

        // A camping field with no festival neighbour is the way in itself.
        foreach (var camp in fields.Where(f => f.Use == FieldUse.Camping && campDoors.All(d => d.Camp != f.Grass)))
        {
            campDoors.Add((camp.Grass, new TilePoint(Math.Clamp(gap.X + (gap.Width / 2), camp.Grass.X, camp.Grass.Right - 1), Math.Clamp(gap.Y, camp.Grass.Y, camp.Grass.Bottom - 1)), byTrack));
        }

        var festivalGrass = Around(fields.Where(f => f.Use != FieldUse.CarPark).Select(f => f.Grass));
        var map = new FieldMap(width, height)
        {
            Field = festivalGrass,
            HedgeLine = Grow(festivalGrass, hedge),
            Gap = gap,
            Lane = lane,
            Track = track,
            Fields = fields,
            Gateways = gateways,
            Tracks = tracks,
            BusStop = new TilePoint((Estate.BusStopX - ox) * 2, lane.Y + 1),
        };
        map._campDoors.AddRange(campDoors);
        doors.Add((rented.IndexOf(wayIn), -1, gap));
        map._doors.AddRange(doors);
        foreach (var f in fields)
        {
            map.Fill(Grow(f.Grass, hedge), Ground.Hedge);
        }

        foreach (var f in fields)
        {
            map.Fill(f.Grass, Ground.Grass);
        }

        foreach (var g in gateways)
        {
            map.Fill(g, Ground.Grass);
        }

        map.Fill(gap, Ground.Grass);
        map.Fill(lane, Ground.Lane);
        foreach (var t in tracks)
        {
            map.Fill(t, Ground.Lane);
        }

        return map;
    }

    private void Fill(TileRect area, Ground ground)
    {
        for (var y = area.Y; y < area.Bottom; y++)
        {
            for (var x = area.X; x < area.Right; x++)
            {
                if (!InBounds(x, y))
                {
                    continue;
                }

                _tiles[(y * Width) + x] = ground;
            }
        }
    }
}
