using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Site;

/// <summary>A stage that has been put up. X and Y are the top-left tile of the ground it covers.</summary>
public sealed record PlacedStage(StageKind Kind, int X, int Y, Facing Facing)
{
    /// <summary>The ground it covers: turned side-on, its width and depth swap.</summary>
    public TileRect Rect
    {
        get
        {
            var info = Catalogue.Of(Kind);
            return Facing is Facing.South or Facing.North ? new TileRect(X, Y, info.Width, info.Depth) : new TileRect(X, Y, info.Depth, info.Width);
        }
    }
}

/// <summary>A stall that has been put up. It serves out of its front, the way it faces.</summary>
public sealed record PlacedStall(StallKind Kind, int X, int Y, Facing Facing)
{
    public TileRect Rect
    {
        get
        {
            var info = Catalogue.Of(Kind);
            return Facing is Facing.South or Facing.North ? new TileRect(X, Y, info.Width, info.Depth) : new TileRect(X, Y, info.Depth, info.Width);
        }
    }

    /// <summary>
    /// The part of the stall people are served across: the whole stall, except in the VIP lounge,
    /// where it is the bar across the back and people queue inside the marquee.
    /// </summary>
    public TileRect ServeRect => Kind != StallKind.VipLounge ? Rect : Facing switch
    {
        Facing.North => new TileRect(Rect.X, Rect.Bottom - SiteLayout.LoungeBarDepth, Rect.Width, SiteLayout.LoungeBarDepth),
        Facing.East => new TileRect(Rect.X, Rect.Y, SiteLayout.LoungeBarDepth, Rect.Height),
        Facing.West => new TileRect(Rect.Right - SiteLayout.LoungeBarDepth, Rect.Y, SiteLayout.LoungeBarDepth, Rect.Height),
        _ => new TileRect(Rect.X, Rect.Y, Rect.Width, SiteLayout.LoungeBarDepth),
    };

    /// <summary>The prices the player has set, one for each thing on the menu. Null while everything is at its usual price.</summary>
    public int[]? Prices { get; init; }

    /// <summary>What this stall charges for one thing on its menu.</summary>
    public int PriceOf(int item)
    {
        var menu = Catalogue.MenuOf(Kind);
        return item < 0 || item >= menu.Count ? 0 : Prices is { } set && item < set.Length ? set[item] : menu[item].UsualPrice;
    }

    /// <summary>
    /// True when only VIP guests may use this stall: set by the player on the menu board, and
    /// always so for the VIP lounge and platform. A sign saying so stands outside.
    /// </summary>
    public bool VipOnly { get; init; }

    /// <summary>True when only VIP guests use it, whether the player chose that or it is a VIP building.</summary>
    public bool ForVips => VipOnly || Catalogue.AlwaysVip(Kind);

    /// <summary>How dear something is here against what people usually expect to pay: 1 is the usual price, 2 is double.</summary>
    public double Dearness(int item) => (double)PriceOf(item) / Math.Max(1, Catalogue.MenuOf(Kind)[item].UsualPrice);
}

/// <summary>A gate that has been put up: a line of tiles one tile thick that people walk through.</summary>
public sealed record PlacedGate(GateKind Kind, int X, int Y, bool Vertical)
{
    public TileRect Rect
    {
        get
        {
            var width = Catalogue.Of(Kind).Width;
            return Vertical ? new TileRect(X, Y, 1, width) : new TileRect(X, Y, width, 1);
        }
    }
}

/// <summary>A straight run of one kind of fence, for drawing and for saving.</summary>
public sealed record FenceRun(FenceKind Kind, int X, int Y, int Length, bool Vertical);

/// <summary>Whether something can be built, what it would cost, and if not, why not.</summary>
public readonly record struct BuildCheck(bool Ok, int Cost, string? Problem)
{
    public static BuildCheck No(string problem) => new(false, 0, problem);
}

/// <summary>Which side of the gates a tile is on.</summary>
public enum Side : byte
{
    /// <summary>Cannot be walked to at all.</summary>
    Nowhere = 0,

    /// <summary>Can be walked to from the lane without going through a gate.</summary>
    Outside = 1,

    /// <summary>Can only be reached through a gate.</summary>
    Inside = 2,
}

/// <summary>How a gate sits in the fence line: whether it is the only way through, and which side people queue on.</summary>
public readonly record struct GateUse(bool InUse, int OutX, int OutY);

/// <summary>
/// Everything the player has built on the field: stages, fences and gates. It keeps the map's
/// blocked tiles up to date and works out which ground is inside the gates and which outside.
/// </summary>
public sealed partial class SiteLayout
{
    private readonly FieldMap _map;
    private readonly List<PlacedStage> _stages = new();
    private readonly List<PlacedGate> _gates = new();
    private readonly List<PlacedStall> _stalls = new();
    private readonly Dictionary<TilePoint, FenceKind> _fence = new();
    private readonly List<GateUse> _gateUses = new();
    private readonly List<List<TilePoint>> _lanes = new();
    private readonly Dictionary<TilePoint, int> _laneOf = new();
    private readonly HashSet<TilePoint> _barriers = new();
    private readonly HashSet<TilePoint> _keepOut = new();
    private Side[] _sides;

    public SiteLayout(FieldMap map)
    {
        _map = map;
        _sides = new Side[map.Width * map.Height];
        Refresh();
    }

    /// <summary>Raised after anything is built or removed.</summary>
    public event Action? Changed;

    public IReadOnlyList<PlacedStage> Stages => _stages;

    public IReadOnlyList<PlacedGate> Gates => _gates;

    public IReadOnlyList<PlacedStall> Stalls => _stalls;

    /// <summary>How each gate is being used, in the same order as <see cref="Gates"/>.</summary>
    public IReadOnlyList<GateUse> GateUses => _gateUses;

    /// <summary>How many tiles of fence there are.</summary>
    public int FenceTiles => _fence.Count;

    /// <summary>True when at least one gate is the only way through the fence beside it.</summary>
    public bool HasWorkingGate { get; private set; }

    /// <summary>True when people can walk from the lane onto the field without passing a gate.</summary>
    public bool OpenWayIn { get; private set; }

    /// <summary>
    /// Where people can get onto the festival without going through a gate, once some fence or a
    /// gate is up: the end of a fence that stops short of the hedge, say, or a gap left open. Null
    /// when the gates are the only way in, or nothing has been put up yet.
    /// </summary>
    public TilePoint? GapInTheFence { get; private set; }

    private bool _gatesShut;

    /// <summary>
    /// True while the gates are shut on the day. The public cannot walk through a shut gate, so
    /// nobody slips in before the gates are opened; ticket staff still can.
    /// </summary>
    public bool GatesShut
    {
        get => _gatesShut;
        set
        {
            if (_gatesShut != value)
            {
                _gatesShut = value;
                Refresh();
            }
        }
    }

    /// <summary>
    /// Finds a gap to show the player. Best is a hole beside a fence or gate: ground anyone can
    /// walk on, with fence or hedge on either side of it. Failing that, where the festival grass
    /// opens straight onto the lane or the track with nothing across it.
    /// </summary>
    private TilePoint? FindGap()
    {
        bool Walkable(TilePoint p) => _map.InBounds(p.X, p.Y) && _map.IsWalkableForVip(p.X, p.Y);
        bool Built(TilePoint p) => _fence.ContainsKey(p) || GateAt(p) is not null;
        bool Shut(TilePoint p) => !Walkable(p) || Built(p);
        var outside = new List<TilePoint>();
        for (var y = 0; y < _map.Height; y++)
        {
            for (var x = 0; x < _map.Width; x++)
            {
                var p = new TilePoint(x, y);
                if (SideOf(p) == Side.Outside)
                {
                    outside.Add(p);
                }
            }
        }

        foreach (var p in outside)
        {
            var left = new TilePoint(p.X - 1, p.Y);
            var right = new TilePoint(p.X + 1, p.Y);
            var up = new TilePoint(p.X, p.Y - 1);
            var down = new TilePoint(p.X, p.Y + 1);
            var acrossX = Shut(left) && Shut(right) && (Built(left) || Built(right));
            var acrossY = Shut(up) && Shut(down) && (Built(up) || Built(down));
            if (acrossX || acrossY)
            {
                return p;
            }
        }

        var field = _map.Field;
        var openings = outside
            .Where(p => field.Contains(p) && new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }.Any(d =>
            {
                var n = new TilePoint(p.X + d.Item1, p.Y + d.Item2);
                return Walkable(n) && !field.Contains(n);
            }))
            .ToList();
        if (openings.Count > 0)
        {
            return openings[openings.Count / 2];
        }

        foreach (var p in outside)
        {
            if (field.Contains(p))
            {
                return p;
            }
        }

        return null;
    }

    public Side SideOf(TilePoint p) => _map.InBounds(p.X, p.Y) ? _sides[(p.Y * _map.Width) + p.X] : Side.Nowhere;

    /// <summary>
    /// The way a gate faces: a step (OutX, OutY) from the gate towards the side people queue on.
    /// A working gate faces the outside it lets people in from. Otherwise (while it is being
    /// placed, say) it faces whichever side is off the festival grass, such as the track or the
    /// lane, and failing that the side nearer the way in off the lane.
    /// </summary>
    public (int OutX, int OutY) FrontOf(PlacedGate gate)
    {
        var index = _gates.IndexOf(gate);
        if (index >= 0 && index < _gateUses.Count && _gateUses[index].InUse)
        {
            return (_gateUses[index].OutX, _gateUses[index].OutY);
        }

        var rect = gate.Rect;
        var (nx, ny) = gate.Vertical ? (1, 0) : (0, 1);
        var tiles = TilesOf(rect).ToList();
        // Hedges are a few tiles thick, so look a little way out on each side, not just one step.
        const int lookOut = 8;
        var offBefore = 0;
        var offAfter = 0;
        for (var d = 1; d <= lookOut; d++)
        {
            offBefore += tiles.Count(p => OffTheGrass(new TilePoint(p.X - (nx * d), p.Y - (ny * d))));
            offAfter += tiles.Count(p => OffTheGrass(new TilePoint(p.X + (nx * d), p.Y + (ny * d))));
        }

        if (offBefore != offAfter)
        {
            return offBefore > offAfter ? (-nx, -ny) : (nx, ny);
        }

        // Both sides alike: face the way in, where people come from.
        var gap = _map.Gap;
        var toward = gate.Vertical
            ? (gap.X + (gap.Width / 2.0)) - (rect.X + 0.5)
            : (gap.Y + (gap.Height / 2.0)) - (rect.Y + 0.5);
        return toward < 0 ? (-nx, -ny) : (nx, ny);
    }

    /// <summary>True for ground that is not festival grass: the hedge, the track, the lane or farmland.</summary>
    private bool OffTheGrass(TilePoint p)
    {
        if (!_map.InBounds(p.X, p.Y))
        {
            return true;
        }

        var ground = _map[p];
        return ground == Ground.Lane || ground == Ground.Hedge || ground == Ground.Countryside || _map.UseAt(p) is null;
    }

    public FenceKind? FenceAt(TilePoint p) => _fence.TryGetValue(p, out var kind) ? kind : null;

    public PlacedStage? StageAt(TilePoint p) => _stages.FirstOrDefault(s => s.Rect.Contains(p));

    public PlacedGate? GateAt(TilePoint p) => _gates.FirstOrDefault(g => g.Rect.Contains(p));

    public PlacedStall? StallAt(TilePoint p) => _stalls.FirstOrDefault(s => s.Rect.Contains(p));

    public bool IsEmpty(TilePoint p) => !_fence.ContainsKey(p) && StageAt(p) is null && GateAt(p) is null && StallAt(p) is null && !_laneOf.ContainsKey(p) && !_barriers.Contains(p) && !HasLight(p) && ScreenAt(p) is null && !FireworksAt(p) && ComfortAt(p) is null && SignAt(p) is null && DecorAt(p) is null && !IsBuntingPole(p) && !_pitches.Contains(p) && !_areaEdges.Contains(p);

    /// <summary>The queue lanes the player has laid: each is the tiles people stand along, in order from one end to the other.</summary>
    public IReadOnlyList<IReadOnlyList<TilePoint>> Lanes => _lanes;

    /// <summary>The tiles with a queue lane's barrier on them. The barriers go up by themselves down both sides of every lane.</summary>
    public IReadOnlyCollection<TilePoint> LaneBarriers => _barriers;

    /// <summary>Which queue lane a tile is part of, or -1.</summary>
    public int LaneAt(TilePoint p) => _laneOf.TryGetValue(p, out var lane) ? lane : _barriers.Contains(p) ? BarrierLane(p) : -1;

    /// <summary>True if a tile is somewhere people queue in a lane (not its barrier).</summary>
    public bool IsLane(TilePoint p) => _laneOf.ContainsKey(p);

    /// <summary>True if a queue lane's barrier stands on a tile.</summary>
    public bool IsBarrier(TilePoint p) => _barriers.Contains(p);

    /// <summary>The tiles the public are kept off. Staff can still walk and stand there.</summary>
    public IReadOnlyCollection<TilePoint> KeepOut => _keepOut;

    public bool IsKeptClear(TilePoint p) => _keepOut.Contains(p);

    /// <summary>What a queue lane costs for each metre, barriers and all.</summary>
    public const int LaneCostPerMetre = 6;

    /// <summary>The fewest tiles a queue lane can be: room for a couple of people.</summary>
    public const int ShortestLane = 4;

    public static int LaneCost(int tiles) => ((LaneCostPerMetre * tiles) + 1) / 2;

    private static bool Touching(TilePoint a, TilePoint b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) == 1;

    private static bool Near(TilePoint a, TilePoint b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)) <= 1;

    private int BarrierLane(TilePoint p)
    {
        for (var i = 0; i < _lanes.Count; i++)
        {
            if (_lanes[i].Any(q => Near(p, q)))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// The lane a drag starting on a tile would carry on, and from which end: a drag that starts on
    /// the last tile of a lane, or right beside it, makes that lane longer (round a corner if it
    /// goes off sideways). Otherwise it starts a new lane.
    /// </summary>
    private (int Lane, bool AtStart) Joins(TilePoint from)
    {
        for (var i = 0; i < _lanes.Count; i++)
        {
            var lane = _lanes[i];
            if (from == lane[^1] || (Touching(from, lane[^1]) && !_laneOf.ContainsKey(from)))
            {
                return (i, false);
            }

            if (from == lane[0] || (Touching(from, lane[0]) && !_laneOf.ContainsKey(from)))
            {
                return (i, true);
            }
        }

        return (-1, false);
    }

    /// <summary>The tiles a drag would add as queue lane, in order, and the lane they would be added to.</summary>
    private (List<TilePoint> Fresh, int Lane, bool AtStart) LaneDrag(TilePoint from, TilePoint to)
    {
        var (lane, atStart) = Joins(from);
        var fresh = Line(from, to).ToList();
        if (lane >= 0 && _laneOf.ContainsKey(from))
        {
            fresh.RemoveAt(0);
        }

        return (fresh, lane, atStart);
    }

    /// <summary>
    /// Whether a queue lane can be dragged out between two tiles. It needs clear grass, and a tile
    /// between it and any other stretch of lane so that there is room for the barrier.
    /// </summary>
    public BuildCheck CheckLane(TilePoint from, TilePoint to)
    {
        var (fresh, lane, atStart) = LaneDrag(from, to);
        if (fresh.Count == 0)
        {
            return BuildCheck.No("There is a queue lane there already.");
        }

        if (lane < 0 && fresh.Count < ShortestLane)
        {
            return BuildCheck.No("Drag further: a queue lane is at least 2 m long.");
        }

        // The tiles already in the lane nearest the join, which the new ones are allowed to be beside.
        var chain = lane < 0 ? new List<TilePoint>() : atStart ? _lanes[lane].Take(2).Reverse().ToList() : _lanes[lane].Skip(Math.Max(0, _lanes[lane].Count - 2)).ToList();
        if (chain.Count > 0 && !Touching(chain[^1], fresh[0]))
        {
            return BuildCheck.No("Start from the very end of the lane to carry it on.");
        }

        foreach (var p in fresh)
        {
            if (!TakesLane(p))
            {
                return BuildCheck.No("A queue lane can only go on the grass, or on the track up to the field.");
            }

            if (_fence.ContainsKey(p) || StageAt(p) is not null || GateAt(p) is not null || StallAt(p) is not null || _laneOf.ContainsKey(p) || ScreenAt(p) is not null || FireworksAt(p))
            {
                return BuildCheck.No("Something is already built there.");
            }

            var allowed = chain.Skip(Math.Max(0, chain.Count - 2)).ToList();
            foreach (var q in _laneOf.Keys)
            {
                if (Near(p, q) && !allowed.Contains(q))
                {
                    return BuildCheck.No("Leave a tile between the rows of a queue for the barrier.");
                }
            }

            chain.Add(p);
        }

        return new BuildCheck(true, LaneCost(fresh.Count), null);
    }

    /// <summary>Lays a queue lane, or carries one on from its end. The barriers either side go up by themselves.</summary>
    public BuildCheck AddLane(TilePoint from, TilePoint to)
    {
        var check = CheckLane(from, to);
        if (check.Ok)
        {
            var (fresh, lane, atStart) = LaneDrag(from, to);
            if (lane < 0)
            {
                _lanes.Add(fresh);
            }
            else if (atStart)
            {
                fresh.Reverse();
                _lanes[lane].InsertRange(0, fresh);
            }
            else
            {
                _lanes[lane].AddRange(fresh);
            }

            Refresh();
        }

        return check;
    }

    private static IEnumerable<TilePoint> Box(TilePoint from, TilePoint to)
    {
        for (var y = Math.Min(from.Y, to.Y); y <= Math.Max(from.Y, to.Y); y++)
        {
            for (var x = Math.Min(from.X, to.X); x <= Math.Max(from.X, to.X); x++)
            {
                yield return new TilePoint(x, y);
            }
        }
    }

    /// <summary>Whether the box between two tiles can be marked "no customers" (or cleared again). It costs nothing.</summary>
    public BuildCheck CheckKeepOut(TilePoint from, TilePoint to, bool keepOut)
    {
        var any = Box(from, to).Any(p => keepOut ? CanKeepClear(p) && !_keepOut.Contains(p) : _keepOut.Contains(p));
        return any ? new BuildCheck(true, 0, null)
            : BuildCheck.No(keepOut ? "Customers can only be kept off clear grass." : "No ground here is marked.");
    }

    private bool CanKeepClear(TilePoint p) => _map[p] == Ground.Grass && IsEmpty(p);

    /// <summary>Marks the box between two tiles "no customers", or clears the mark. Staff still go there.</summary>
    public BuildCheck SetKeepOut(TilePoint from, TilePoint to, bool keepOut)
    {
        var check = CheckKeepOut(from, to, keepOut);
        if (check.Ok)
        {
            foreach (var p in Box(from, to))
            {
                if (!keepOut)
                {
                    _keepOut.Remove(p);
                }
                else if (CanKeepClear(p))
                {
                    _keepOut.Add(p);
                    _vipGround.Remove(p);
                }
            }

            Refresh();
        }

        return check;
    }

    /// <summary>The tiles in a set as straight runs: across first, then whatever is left as runs up and down.</summary>
    public static List<(int X, int Y, int Length, bool Vertical)> RunsOf(IReadOnlyCollection<TilePoint> tiles)
    {
        var runs = new List<(int, int, int, bool)>();
        var all = tiles as HashSet<TilePoint> ?? new HashSet<TilePoint>(tiles);
        var used = new HashSet<TilePoint>();
        var ordered = all.OrderBy(p => p.Y).ThenBy(p => p.X).ToList();
        foreach (var vertical in new[] { false, true })
        {
            var (dx, dy) = vertical ? (0, 1) : (1, 0);
            foreach (var p in ordered)
            {
                if (used.Contains(p))
                {
                    continue;
                }

                var length = 1;
                for (var q = new TilePoint(p.X + dx, p.Y + dy); !used.Contains(q) && all.Contains(q); q = new TilePoint(q.X + dx, q.Y + dy))
                {
                    length++;
                }

                if (!vertical && length == 1)
                {
                    continue;
                }

                for (var i = 0; i < length; i++)
                {
                    used.Add(new TilePoint(p.X + (i * dx), p.Y + (i * dy)));
                }

                runs.Add((p.X, p.Y, length, vertical));
            }
        }

        return runs;
    }

    /// <summary>The middle of the front edge of something in metres, and the way it faces.</summary>
    public static (double X, double Y, int DirX, int DirY) Front(TileRect r, Facing facing)
    {
        const double t = FieldMap.TileMetres;
        var midX = (r.X + (r.Width / 2.0)) * t;
        var midY = (r.Y + (r.Height / 2.0)) * t;
        return facing switch
        {
            Facing.North => (midX, r.Y * t, 0, -1),
            Facing.East => (r.Right * t, midY, 1, 0),
            Facing.West => (r.X * t, midY, -1, 0),
            _ => (midX, r.Bottom * t, 0, 1),
        };
    }

    /// <summary>Where someone stands in a stall's queue, in metres: which server's line, and how far out from the counter.</summary>
    public static (double X, double Y) ServingPlace(PlacedStall stall, int lane, double metresOut)
    {
        var info = Catalogue.Of(stall.Kind);
        var (fx, fy, dx, dy) = Front(stall.ServeRect, stall.Facing);
        var across = (((lane + 0.5) / info.Servers) - 0.5) * info.Width * FieldMap.TileMetres * (stall.Kind == StallKind.VipLounge ? 0.4 : 0.75);
        return (fx + (dx * metresOut) - (dy * across), fy + (dy * metresOut) + (dx * across));
    }

    /// <summary>How far apart the cabins of a toilet stand, in metres.</summary>
    public const double CubicleMetres = 1.25;

    /// <summary>A place in line with the door of one cubicle of a toilet, in metres: so far out from the front (or in, if negative).</summary>
    public static (double X, double Y) CubicleDoor(PlacedStall stall, int cubicle, double metresOut)
    {
        var info = Catalogue.Of(stall.Kind);
        var (fx, fy, dx, dy) = Front(stall.Rect, stall.Facing);
        var across = (cubicle - ((info.Servers - 1) / 2.0)) * CubicleMetres;
        return (fx + (dx * metresOut) - (dy * across), fy + (dy * metresOut) + (dx * across));
    }

    public BuildCheck CheckStall(StallKind kind, int x, int y, Facing facing)
    {
        var stall = new PlacedStall(kind, x, y, facing);
        foreach (var p in TilesOf(stall.Rect))
        {
            if (!_map.Rented(p) || !IsBuildable(p))
            {
                return BuildCheck.No("A stall must stand on the grass inside the hedge.");
            }

            if (!IsEmpty(p))
            {
                return BuildCheck.No("Something is already built there.");
            }
        }

        return new BuildCheck(true, Catalogue.Of(kind).Cost, null);
    }

    public BuildCheck AddStall(StallKind kind, int x, int y, Facing facing)
    {
        var check = CheckStall(kind, x, y, facing);
        if (check.Ok)
        {
            _stalls.Add(new PlacedStall(kind, x, y, facing));
            Refresh();
        }

        return check;
    }

    /// <summary>Raised when the price of something on a stall's menu changes.</summary>
    public event Action? PricesChanged;

    /// <summary>
    /// Sets what a stall charges for one thing on its menu, kept between £1 and three times the
    /// usual price. Returns the price it ended up at, or null if there is no such stall or thing.
    /// </summary>
    public int? SetPrice(int stall, int item, int price)
    {
        if (stall < 0 || stall >= _stalls.Count)
        {
            return null;
        }

        var placed = _stalls[stall];
        var menu = Catalogue.MenuOf(placed.Kind);
        if (item < 0 || item >= menu.Count)
        {
            return null;
        }

        var prices = new int[menu.Count];
        for (var i = 0; i < prices.Length; i++)
        {
            prices[i] = placed.PriceOf(i);
        }

        prices[item] = Math.Clamp(price, Catalogue.LowestPrice(menu[item]), Catalogue.HighestPrice(menu[item]));
        _stalls[stall] = placed with { Prices = prices };
        PricesChanged?.Invoke();
        return prices[item];
    }

    private bool IsBuildable(TilePoint p) => _map[p] == Ground.Grass;

    /// <summary>A queue lane can go on the grass, or on a track off the lane (people queue down the track to a gate in the gap).</summary>
    private bool TakesLane(TilePoint p) => IsBuildable(p) || (_map[p] == Ground.Lane && (_map.Track.Contains(p) || _map.Tracks.Any(t => t.Contains(p))));

    private static IEnumerable<TilePoint> TilesOf(TileRect r)
    {
        for (var y = r.Y; y < r.Bottom; y++)
        {
            for (var x = r.X; x < r.Right; x++)
            {
                yield return new TilePoint(x, y);
            }
        }
    }

    /// <summary>The tiles of a straight fence between two tiles: it follows whichever way is longer.</summary>
    public static IEnumerable<TilePoint> Line(TilePoint from, TilePoint to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        if (Math.Abs(dx) >= Math.Abs(dy))
        {
            for (var i = 0; i <= Math.Abs(dx); i++)
            {
                yield return new TilePoint(from.X + (i * Math.Sign(dx)), from.Y);
            }
        }
        else
        {
            for (var i = 0; i <= Math.Abs(dy); i++)
            {
                yield return new TilePoint(from.X, from.Y + (i * Math.Sign(dy)));
            }
        }
    }

    public BuildCheck CheckStage(StageKind kind, int x, int y, Facing facing)
    {
        var stage = new PlacedStage(kind, x, y, facing);
        foreach (var p in TilesOf(stage.Rect))
        {
            if (_map.FromPlan && _map.UseAt(p) is { } use && use != FieldUse.Music)
            {
                return BuildCheck.No("A stage can only go in a music field.");
            }

            if (!_map.Field.Contains(p) || !IsBuildable(p))
            {
                return BuildCheck.No("A stage must stand on the grass inside the hedge.");
            }

            if (!IsEmpty(p))
            {
                return BuildCheck.No("Something is already built there.");
            }
        }

        return new BuildCheck(true, Catalogue.Of(kind).Cost, null);
    }

    public BuildCheck CheckFence(FenceKind kind, TilePoint from, TilePoint to)
    {
        var fresh = 0;
        foreach (var p in Line(from, to))
        {
            if (!IsBuildable(p))
            {
                return BuildCheck.No("Fence can only go on the grass.");
            }

            if (StageAt(p) is not null || GateAt(p) is not null || StallAt(p) is not null || _laneOf.ContainsKey(p) || _barriers.Contains(p) || HasLight(p) || ScreenAt(p) is not null || FireworksAt(p))
            {
                return BuildCheck.No("Something is already built there.");
            }

            if (!_fence.ContainsKey(p))
            {
                fresh++;
            }
        }

        return fresh == 0
            ? BuildCheck.No("There is already fence there.")
            : new BuildCheck(true, FenceCost(kind, fresh), null);
    }

    /// <summary>What a number of tiles of fence costs: tiles are half a metre, and part pounds round up.</summary>
    public static int FenceCost(FenceKind kind, int tiles) => ((Catalogue.Of(kind).CostPerMetre * tiles) + 1) / 2;

    public BuildCheck CheckGate(GateKind kind, int x, int y, bool vertical)
    {
        var gate = new PlacedGate(kind, x, y, vertical);
        foreach (var p in TilesOf(gate.Rect))
        {
            if (!IsBuildable(p))
            {
                return BuildCheck.No("A gate can only go on the grass.");
            }

            if (StageAt(p) is not null || GateAt(p) is not null || StallAt(p) is not null || _laneOf.ContainsKey(p) || _barriers.Contains(p) || HasLight(p) || ScreenAt(p) is not null || FireworksAt(p))
            {
                return BuildCheck.No("Something is already built there.");
            }
        }

        return new BuildCheck(true, Catalogue.Of(kind).Cost, null);
    }

    public BuildCheck AddStage(StageKind kind, int x, int y, Facing facing)
    {
        var check = CheckStage(kind, x, y, facing);
        if (check.Ok)
        {
            _stages.Add(new PlacedStage(kind, x, y, facing));
            Refresh();
        }

        return check;
    }

    public BuildCheck AddFence(FenceKind kind, TilePoint from, TilePoint to)
    {
        var check = CheckFence(kind, from, to);
        if (check.Ok)
        {
            foreach (var p in Line(from, to))
            {
                _fence.TryAdd(p, kind);
            }

            Refresh();
        }

        return check;
    }

    /// <summary>Puts up a gate. Any fence where it stands comes down to make the opening.</summary>
    public BuildCheck AddGate(GateKind kind, int x, int y, bool vertical)
    {
        var check = CheckGate(kind, x, y, vertical);
        if (check.Ok)
        {
            var gate = new PlacedGate(kind, x, y, vertical);
            foreach (var p in TilesOf(gate.Rect))
            {
                _fence.Remove(p);
            }

            _gates.Add(gate);
            Refresh();
        }

        return check;
    }

    /// <summary>What taking down whatever stands on a tile would give back, or 0 if nothing is there.</summary>
    public int RefundAt(TilePoint p)
    {
        if (PadRefundAt(p) is > 0 and var pad)
        {
            return pad;
        }

        if (ScreenRefundAt(p) is > 0 and var screen)
        {
            return screen;
        }

        if (LightRefundAt(p) is > 0 and var lights)
        {
            return lights;
        }

        if (ComfortRefundAt(p) is > 0 and var comfort)
        {
            return comfort;
        }

        if (SignRefundAt(p) is > 0 and var sign)
        {
            return sign;
        }

        if (DecorRefundAt(p) is > 0 and var decor)
        {
            return decor;
        }

        if (StageAt(p) is { } stage)
        {
            return Catalogue.Of(stage.Kind).Cost;
        }

        if (GateAt(p) is { } gate)
        {
            return Catalogue.Of(gate.Kind).Cost;
        }

        if (StallAt(p) is { } stall)
        {
            return Catalogue.Of(stall.Kind).Cost;
        }

        if (LaneAt(p) is >= 0 and var lane && !_fence.ContainsKey(p))
        {
            return LaneCost(_lanes[lane].Count);
        }

        return _fence.TryGetValue(p, out var kind) ? FenceCost(kind, FenceRunThrough(p).Count) : AreaRefundAt(p);
    }

    /// <summary>The whole straight stretch of one kind of fence that a tile is part of.</summary>
    private List<TilePoint> FenceRunThrough(TilePoint p)
    {
        var kind = _fence[p];
        bool Same(TilePoint q) => _fence.TryGetValue(q, out var k) && k == kind;
        var across = Same(new TilePoint(p.X - 1, p.Y)) || Same(new TilePoint(p.X + 1, p.Y));
        var (dx, dy) = across ? (1, 0) : (0, 1);
        var run = new List<TilePoint> { p };
        for (var q = new TilePoint(p.X - dx, p.Y - dy); Same(q); q = new TilePoint(q.X - dx, q.Y - dy))
        {
            run.Add(q);
        }

        for (var q = new TilePoint(p.X + dx, p.Y + dy); Same(q); q = new TilePoint(q.X + dx, q.Y + dy))
        {
            run.Add(q);
        }

        return run;
    }

    /// <summary>Takes down whatever stands on a tile (a whole stage, gate or stretch of fence). Returns the money back.</summary>
    public int RemoveAt(TilePoint p)
    {
        var refund = RefundAt(p);
        if (RemovePadAt(p))
        {
            // The launch pad, and the fence round its safe zone.
        }
        else if (RemoveScreenAt(p))
        {
            // A big screen.
        }
        else if (RemoveLightsAt(p))
        {
            // A light, or the fairy lights on a pole.
        }
        else if (RemoveComfortAt(p))
        {
            // A seat, a sink or a sail.
        }
        else if (RemoveSignAt(p))
        {
            // A sign.
        }
        else if (RemoveDecorAt(p))
        {
            // A decoration, or the bunting on a pole.
        }
        else if (StageAt(p) is { } stage)
        {
            _stages.Remove(stage);
        }
        else if (GateAt(p) is { } gate)
        {
            _gates.Remove(gate);
        }
        else if (StallAt(p) is { } stall)
        {
            _stalls.Remove(stall);
        }
        else if (_fence.ContainsKey(p))
        {
            foreach (var q in FenceRunThrough(p))
            {
                _fence.Remove(q);
            }
        }
        else if (LaneAt(p) is >= 0 and var lane)
        {
            _lanes.RemoveAt(lane);
        }
        else if (RemoveAreaAt(p))
        {
            // A crowd area, and the barrier round it.
        }
        else
        {
            return 0;
        }

        Refresh();
        return refund;
    }

    /// <summary>The fence as straight runs: across first, then whatever is left as runs up and down.</summary>
    public List<FenceRun> FenceRuns()
    {
        var runs = new List<FenceRun>();
        var used = new HashSet<TilePoint>();
        var ordered = _fence.Keys.OrderBy(p => p.Y).ThenBy(p => p.X).ToList();
        foreach (var vertical in new[] { false, true })
        {
            var (dx, dy) = vertical ? (0, 1) : (1, 0);
            foreach (var p in ordered)
            {
                if (used.Contains(p))
                {
                    continue;
                }

                var kind = _fence[p];
                var length = 1;
                for (var q = new TilePoint(p.X + dx, p.Y + dy); !used.Contains(q) && _fence.TryGetValue(q, out var k) && k == kind; q = new TilePoint(q.X + dx, q.Y + dy))
                {
                    length++;
                }

                // On the first pass keep only real runs across; single tiles wait to join a run down.
                if (!vertical && length == 1)
                {
                    continue;
                }

                for (var i = 0; i < length; i++)
                {
                    used.Add(new TilePoint(p.X + (i * dx), p.Y + (i * dy)));
                }

                runs.Add(new FenceRun(kind, p.X, p.Y, length, vertical));
            }
        }

        return runs;
    }

    /// <summary>Where a gate's lane meets the outside, in metres, and the tile there.</summary>
    public (double X, double Y) LaneMouth(int gateIndex, int lane, double metresOut)
    {
        var gate = _gates[gateIndex];
        var use = _gateUses[gateIndex];
        var info = Catalogue.Of(gate.Kind);
        var along = info.Width * (lane + 0.5) / info.Lanes;
        var x = (gate.X + (gate.Vertical ? 0.5 : along)) * FieldMap.TileMetres;
        var y = (gate.Y + (gate.Vertical ? along : 0.5)) * FieldMap.TileMetres;
        return (x + (use.OutX * metresOut), y + (use.OutY * metresOut));
    }

    public SiteSave Save() => new(_stages.ToList(), _gates.ToList(), FenceRuns(), _stalls.ToList(),
        _lanes.Count == 0 ? null : _lanes.Select(l => l.ToList()).ToList(),
        _keepOut.Count == 0 ? null : RunsOf(_keepOut).Select(r => r.Vertical ? new TileRect(r.X, r.Y, 1, r.Length) : new TileRect(r.X, r.Y, r.Length, 1)).ToList(),
        _lights.Count == 0 ? null : _lights.ToList(), _fairy.Count == 0 ? null : _fairy.ToList(), _screens.Count == 0 ? null : _screens.ToList(), _pad, _comforts.Count == 0 ? null : _comforts.ToList(), _signs.Count == 0 ? null : _signs.ToList(), _decor.Count == 0 ? null : _decor.ToList(), _bunting.Count == 0 ? null : _bunting.ToList(), SaveVipGround(), _areas.Count == 0 ? null : _areas.ToList());

    /// <summary>Puts back a saved layout, skipping anything that no longer fits.</summary>
    public void Restore(SiteSave save)
    {
        _stages.Clear();
        _gates.Clear();
        _fence.Clear();
        _stalls.Clear();
        _lanes.Clear();
        _laneOf.Clear();
        _barriers.Clear();
        _keepOut.Clear();
        _lights.Clear();
        _fairy.Clear();
        _screens.Clear();
        _pad = null;
        _padFence.Clear();
        _areas.Clear();
        foreach (var s in save.Stages ?? new())
        {
            if (CheckStage(s.Kind, s.X, s.Y, s.Facing).Ok)
            {
                _stages.Add(s);
            }
        }

        foreach (var s in save.Stalls ?? new())
        {
            if (CheckStall(s.Kind, s.X, s.Y, s.Facing).Ok)
            {
                _stalls.Add(s);
            }
        }

        foreach (var r in save.Fences ?? new())
        {
            for (var i = 0; i < r.Length; i++)
            {
                var p = new TilePoint(r.X + (r.Vertical ? 0 : i), r.Y + (r.Vertical ? i : 0));
                if (IsBuildable(p) && StageAt(p) is null && StallAt(p) is null)
                {
                    _fence.TryAdd(p, r.Kind);
                }
            }
        }

        foreach (var g in save.Gates ?? new())
        {
            if (CheckGate(g.Kind, g.X, g.Y, g.Vertical).Ok)
            {
                foreach (var p in TilesOf(g.Rect))
                {
                    _fence.Remove(p);
                }

                _gates.Add(g);
            }
        }

        foreach (var lane in save.Lanes ?? new())
        {
            if (lane.Count >= 2 && Enumerable.Range(1, lane.Count - 1).All(i => Touching(lane[i - 1], lane[i])) && lane.All(p => TakesLane(p) && IsEmpty(p)))
            {
                _lanes.Add(lane.ToList());
                foreach (var p in lane)
                {
                    _laneOf[p] = _lanes.Count - 1;
                }
            }
        }

        foreach (var r in save.KeepOut ?? new())
        {
            foreach (var p in TilesOf(r))
            {
                if (_map.InBounds(p.X, p.Y) && _map[p] == Ground.Grass)
                {
                    _keepOut.Add(p);
                }
            }
        }

        RestoreLights(save);
        RestoreScreens(save);
        RestorePad(save);
        RestoreComforts(save);
        RestoreSigns(save);
        RestoreDecor(save);
        RestoreVipGround(save);
        RestoreAreas(save);
        Refresh();
    }

    private readonly HashSet<TilePoint> _pitches = new();

    /// <summary>The ground the entertainers and their instruments stand on, which people walk round.</summary>
    public IReadOnlyCollection<TilePoint> Pitches => _pitches;

    /// <summary>Says where the entertainers stand. People walk round them like anything else built.</summary>
    public void SetPitches(IEnumerable<TilePoint> tiles)
    {
        var wanted = new HashSet<TilePoint>(tiles);
        if (wanted.SetEquals(_pitches))
        {
            return;
        }

        _pitches.Clear();
        _pitches.UnionWith(wanted);
        Refresh();
    }

    /// <summary>Works out again what is blocked and which ground is inside the gates.</summary>
    private void Refresh()
    {
        LayBarriers();
        LayAreas();
        _litStale = true;
        _roofStale = true;
        _charmStale = true;
        _poles.Clear();
        _keepOut.RemoveWhere(p => _fence.ContainsKey(p) || StageAt(p) is not null || StallAt(p) is not null || GateAt(p) is not null || _laneOf.ContainsKey(p) || _barriers.Contains(p) || ScreenAt(p) is not null || FireworksAt(p) || ComfortAt(p) is not null || SignAt(p) is not null || DecorAt(p) is not null || IsBuntingPole(p) || _areaEdges.Contains(p));
        _map.SetBlocked(_stages.SelectMany(s => TilesOf(s.Rect)).Concat(_stalls.SelectMany(BlockedBy)).Concat(_fence.Keys).Concat(_laneOf.Keys).Concat(_barriers).Concat(LightsInTheWay).Concat(ScreensInTheWay).Concat(PadInTheWay).Concat(ComfortsInTheWay).Concat(SignsInTheWay).Concat(DecorInTheWay).Concat(_pitches).Concat(_areaEdges));
        RefreshVipGround();
        // Shut gates are closed to the public: nobody walks through one until the gates are opened.
        _map.SetStaffOnly(_gatesShut ? _keepOut.Concat(_gates.SelectMany(g => TilesOf(g.Rect))) : _keepOut);

        var width = _map.Width;
        _sides = new Side[width * _map.Height];
        var gateTiles = new HashSet<TilePoint>(_gates.SelectMany(g => TilesOf(g.Rect)));

        // Outside is everywhere you can walk to from the lane without stepping through a gate.
        Flood(new[] { _map.LaneStart }, Side.Outside, gateTiles);

        _gateUses.Clear();
        var innerTiles = new List<TilePoint>();
        foreach (var gate in _gates)
        {
            var rect = gate.Rect;
            var (nx, ny) = gate.Vertical ? (1, 0) : (0, 1);
            var before = TilesOf(rect).Select(p => new TilePoint(p.X - nx, p.Y - ny)).ToList();
            var after = TilesOf(rect).Select(p => new TilePoint(p.X + nx, p.Y + ny)).ToList();
            var beforeOut = before.Any(p => SideOf(p) == Side.Outside);
            var afterOut = after.Any(p => SideOf(p) == Side.Outside);
            var inUse = beforeOut != afterOut && (beforeOut ? after : before).Any(_map.IsWalkable);
            _gateUses.Add(inUse ? new GateUse(true, beforeOut ? -nx : nx, beforeOut ? -ny : ny) : default);
            if (inUse)
            {
                innerTiles.AddRange((beforeOut ? after : before).Where(_map.IsWalkable));
            }
        }

        Flood(innerTiles, Side.Inside, gateTiles);
        HasWorkingGate = _gateUses.Any(u => u.InUse);
        OpenWayIn = !HasWorkingGate && TilesOf(_map.Field).Any(p => SideOf(p) == Side.Outside);
        GapInTheFence = OpenWayIn && (_gates.Count > 0 || _fence.Count > 0) ? FindGap() : null;
        Changed?.Invoke();
    }

    /// <summary>
    /// Works out where the barriers of the queue lanes stand: on every tile round a lane, leaving
    /// both ends open. Two rows of a lane a tile apart share the barrier between them.
    /// </summary>
    private void LayBarriers()
    {
        _laneOf.Clear();
        _barriers.Clear();
        for (var i = 0; i < _lanes.Count; i++)
        {
            foreach (var p in _lanes[i])
            {
                _laneOf[p] = i;
            }
        }

        foreach (var lane in _lanes)
        {
            var open = new HashSet<TilePoint>();
            foreach (var (end, next) in new[] { (lane[0], lane[Math.Min(1, lane.Count - 1)]), (lane[^1], lane[Math.Max(0, lane.Count - 2)]) })
            {
                var (ex, ey) = (end.X - next.X, end.Y - next.Y);
                for (var side = -1; side <= 1; side++)
                {
                    open.Add(new TilePoint(end.X + ex - (ey * side), end.Y + ey + (ex * side)));
                }
            }

            foreach (var p in lane)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        var q = new TilePoint(p.X + dx, p.Y + dy);
                        if (!_laneOf.ContainsKey(q) && !open.Contains(q) && TakesLane(q) && StageAt(q) is null && StallAt(q) is null && GateAt(q) is null && !_fence.ContainsKey(q))
                        {
                            _barriers.Add(q);
                        }
                    }
                }
            }
        }
    }

    private void Flood(IEnumerable<TilePoint> from, Side side, HashSet<TilePoint> gateTiles)
    {
        var width = _map.Width;
        var queue = new Queue<TilePoint>();
        foreach (var p in from)
        {
            if (_map.IsWalkableForVip(p.X, p.Y) && _sides[(p.Y * width) + p.X] == Side.Nowhere && !gateTiles.Contains(p))
            {
                _sides[(p.Y * width) + p.X] = side;
                queue.Enqueue(p);
            }
        }

        while (queue.TryDequeue(out var at))
        {
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var next = new TilePoint(at.X + dx, at.Y + dy);
                if (!_map.IsWalkableForVip(next.X, next.Y) || gateTiles.Contains(next))
                {
                    continue;
                }

                var index = (next.Y * width) + next.X;
                if (_sides[index] == Side.Nowhere)
                {
                    _sides[index] = side;
                    queue.Enqueue(next);
                }
            }
        }
    }
}

/// <summary>The layout as written to a save file.</summary>
public sealed record SiteSave(List<PlacedStage>? Stages, List<PlacedGate>? Gates, List<FenceRun>? Fences, List<PlacedStall>? Stalls = null,
    List<List<TilePoint>>? Lanes = null, List<TileRect>? KeepOut = null,
    List<PlacedLight>? Lights = null, List<FairyString>? FairyLights = null, List<PlacedScreen>? Screens = null, PlacedLaunchPad? LaunchPad = null, List<PlacedComfort>? Comforts = null, List<PlacedSign>? Signs = null, List<PlacedDecor>? Decor = null, List<BuntingRun>? Bunting = null, List<TileRect>? VipGround = null, List<PlacedArea>? Areas = null);
