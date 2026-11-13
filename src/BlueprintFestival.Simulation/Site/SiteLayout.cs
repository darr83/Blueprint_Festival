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
public sealed class SiteLayout
{
    private readonly FieldMap _map;
    private readonly List<PlacedStage> _stages = new();
    private readonly List<PlacedGate> _gates = new();
    private readonly Dictionary<TilePoint, FenceKind> _fence = new();
    private readonly List<GateUse> _gateUses = new();
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

    /// <summary>How each gate is being used, in the same order as <see cref="Gates"/>.</summary>
    public IReadOnlyList<GateUse> GateUses => _gateUses;

    /// <summary>How many tiles of fence there are.</summary>
    public int FenceTiles => _fence.Count;

    /// <summary>True when at least one gate is the only way through the fence beside it.</summary>
    public bool HasWorkingGate { get; private set; }

    /// <summary>True when people can walk from the lane onto the field without passing a gate.</summary>
    public bool OpenWayIn { get; private set; }

    public Side SideOf(TilePoint p) => _map.InBounds(p.X, p.Y) ? _sides[(p.Y * _map.Width) + p.X] : Side.Nowhere;

    public FenceKind? FenceAt(TilePoint p) => _fence.TryGetValue(p, out var kind) ? kind : null;

    public PlacedStage? StageAt(TilePoint p) => _stages.FirstOrDefault(s => s.Rect.Contains(p));

    public PlacedGate? GateAt(TilePoint p) => _gates.FirstOrDefault(g => g.Rect.Contains(p));

    public bool IsEmpty(TilePoint p) => !_fence.ContainsKey(p) && StageAt(p) is null && GateAt(p) is null;

    private bool IsBuildable(TilePoint p) => _map[p] == Ground.Grass;

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

            if (StageAt(p) is not null || GateAt(p) is not null)
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

            if (StageAt(p) is not null || GateAt(p) is not null)
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
        if (StageAt(p) is { } stage)
        {
            return Catalogue.Of(stage.Kind).Cost;
        }

        if (GateAt(p) is { } gate)
        {
            return Catalogue.Of(gate.Kind).Cost;
        }

        return _fence.TryGetValue(p, out var kind) ? FenceCost(kind, FenceRunThrough(p).Count) : 0;
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
        if (StageAt(p) is { } stage)
        {
            _stages.Remove(stage);
        }
        else if (GateAt(p) is { } gate)
        {
            _gates.Remove(gate);
        }
        else if (_fence.ContainsKey(p))
        {
            foreach (var q in FenceRunThrough(p))
            {
                _fence.Remove(q);
            }
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

    public SiteSave Save() => new(_stages.ToList(), _gates.ToList(), FenceRuns());

    /// <summary>Puts back a saved layout, skipping anything that no longer fits.</summary>
    public void Restore(SiteSave save)
    {
        _stages.Clear();
        _gates.Clear();
        _fence.Clear();
        foreach (var s in save.Stages ?? new())
        {
            if (CheckStage(s.Kind, s.X, s.Y, s.Facing).Ok)
            {
                _stages.Add(s);
            }
        }

        foreach (var r in save.Fences ?? new())
        {
            for (var i = 0; i < r.Length; i++)
            {
                var p = new TilePoint(r.X + (r.Vertical ? 0 : i), r.Y + (r.Vertical ? i : 0));
                if (IsBuildable(p) && StageAt(p) is null)
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

        Refresh();
    }

    /// <summary>Works out again what is blocked and which ground is inside the gates.</summary>
    private void Refresh()
    {
        _map.SetBlocked(_stages.SelectMany(s => TilesOf(s.Rect)).Concat(_fence.Keys));

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
        Changed?.Invoke();
    }

    private void Flood(IEnumerable<TilePoint> from, Side side, HashSet<TilePoint> gateTiles)
    {
        var width = _map.Width;
        var queue = new Queue<TilePoint>();
        foreach (var p in from)
        {
            if (_map.IsWalkable(p) && _sides[(p.Y * width) + p.X] == Side.Nowhere && !gateTiles.Contains(p))
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
                if (!_map.IsWalkable(next) || gateTiles.Contains(next))
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
public sealed record SiteSave(List<PlacedStage>? Stages, List<PlacedGate>? Gates, List<FenceRun>? Fences);
