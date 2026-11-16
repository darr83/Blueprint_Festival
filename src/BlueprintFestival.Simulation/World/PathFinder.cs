namespace BlueprintFestival.Simulation.World;

/// <summary>
/// Finds walking routes across the tile grid (A*). People can step to any of the eight tiles round
/// them but never cut a corner, and routes keep a little clear of hedges and edges so that people
/// walk down the middle of a lane or a gateway rather than brushing along its side.
/// </summary>
public sealed class PathFinder
{
    private const double Diagonal = 1.41421356;

    /// <summary>How many tiles from either end a straight line may pass close to something built.</summary>
    private const int EndReach = 6;

    /// <summary>
    /// A hair's weight on the distance still to go, so that of two equally good routes the search
    /// follows the one that is nearly there instead of trying every one of them.
    /// </summary>
    private const double Keen = 1.0005;
    private static readonly (int Dx, int Dy)[] Steps = { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1) };

    /// <summary>How much further someone who keeps to paths will walk to stay on one: each step on the grass counts this much.</summary>
    public const double OffPath = 1.8;

    private readonly FieldMap _map;
    private readonly float[] _crowding;

    /// <summary>Scratch for <see cref="Refresh"/>: tiles to the nearest thing in the way, up to four.</summary>
    private readonly byte[] _near;

    /// <summary>Which tiles could be walked on at the last <see cref="Refresh"/>, and at the one before.</summary>
    private bool[] _open;
    private bool[] _was;

    /// <summary>True once the whole map has been worked out: after that only what changes is.</summary>
    private bool _swept;
    private readonly bool[] _onPath;
    private readonly int[] _pathSum;
    private bool _anyPaths;

    /// <summary>How far, in tiles, someone will look to either side of the way they are going for a path to use.</summary>
    private const int PathReach = 16;

    /// <summary>A spread of path tiles, a metre or so apart, to choose where to join and leave the paths.</summary>
    private readonly List<TilePoint> _pathPoints = new();

    // Kept between searches and marked with the search's number, so a route across a big field
    // does not have to wipe the whole map first.
    private readonly double[] _cost;
    private readonly int[] _cameFrom;
    private readonly int[] _searched;
    private int _search;

    private readonly bool _staff;
    private readonly bool _vip;

    private bool Walkable(int x, int y) => _staff ? _map.IsWalkableForStaff(x, y) : _vip ? _map.IsWalkableForVip(x, y) : _map.IsWalkable(x, y);

    private bool Walkable(TilePoint p) => Walkable(p.X, p.Y);

    /// <summary>A route finder for the public, or for staff, who can also cross ground the public are kept off.</summary>
    public PathFinder(FieldMap map, bool staff = false, bool vip = false)
    {
        _map = map;
        _staff = staff;
        _vip = vip && !staff;
        _crowding = new float[map.Width * map.Height];
        _near = new byte[_crowding.Length];
        _open = new bool[_crowding.Length];
        _was = new bool[_crowding.Length];
        _cost = new double[_crowding.Length];
        _cameFrom = new int[_crowding.Length];
        _searched = new int[_crowding.Length];
        _onPath = new bool[_crowding.Length];
        _pathSum = new int[(map.Width + 1) * (map.Height + 1)];
        Refresh();
    }

    /// <summary>True once any path has been laid.</summary>
    public bool HasPaths => _anyPaths;

    /// <summary>True if a tile is path (or the lane and track, which are a road anyway) for someone who keeps to paths.</summary>
    public bool OnPath(TilePoint p) => _map.InBounds(p.X, p.Y) && _onPath[(p.Y * _map.Width) + p.X];

    /// <summary>Says which tiles have a path on them. The lane and the track count as path too.</summary>
    public void SetPaths(IEnumerable<TilePoint> tiles)
    {
        Array.Clear(_onPath);
        _anyPaths = false;
        _pathPoints.Clear();
        foreach (var p in tiles)
        {
            if (_map.InBounds(p.X, p.Y))
            {
                _onPath[(p.Y * _map.Width) + p.X] = true;
                _anyPaths = true;
                if (p.X % 3 == 1 && p.Y % 3 == 1)
                {
                    _pathPoints.Add(p);
                }
            }
        }

        // A running count of laid path, so that how much path lies in any box can be found at once.
        var stride = _map.Width + 1;
        for (var y = 0; y < _map.Height; y++)
        {
            var row = 0;
            for (var x = 0; x < _map.Width; x++)
            {
                row += _onPath[(y * _map.Width) + x] ? 1 : 0;
                _pathSum[((y + 1) * stride) + x + 1] = _pathSum[(y * stride) + x + 1] + row;
            }
        }

        for (var y = 0; y < _map.Height; y++)
        {
            for (var x = 0; x < _map.Width; x++)
            {
                if (_map[x, y] == Ground.Lane)
                {
                    _onPath[(y * _map.Width) + x] = true;
                }
            }
        }
    }

    /// <summary>True if any laid path lies near the box between two tiles.</summary>
    private bool PathNear(TilePoint a, TilePoint b)
    {
        var x0 = Math.Max(0, Math.Min(a.X, b.X) - PathReach);
        var y0 = Math.Max(0, Math.Min(a.Y, b.Y) - PathReach);
        var x1 = Math.Min(_map.Width, Math.Max(a.X, b.X) + PathReach + 1);
        var y1 = Math.Min(_map.Height, Math.Max(a.Y, b.Y) + PathReach + 1);
        var stride = _map.Width + 1;
        return _pathSum[(y1 * stride) + x1] - _pathSum[(y0 * stride) + x1] - _pathSum[(y1 * stride) + x0] + _pathSum[(y0 * stride) + x0] > 0;
    }

    /// <summary>
    /// Works out again where the tight spots are. Call after anything is built or removed.
    /// A tile's weight comes from how many tiles it is to the nearest one that cannot be walked
    /// on (counting the edge of the map), found for the whole map in two sweeps rather than by
    /// looking all round every tile, so that putting something down on a big site is quick.
    /// </summary>
    public void Refresh()
    {
        var width = _map.Width;
        var height = _map.Height;
        _map.FillWalkable(_was, _staff, _vip);
        (_open, _was) = (_was, _open);
        if (_swept)
        {
            // Only the ground near what changed needs working out again: a tile's weight only
            // depends on the tiles up to three away from it.
            int x0 = width, y0 = height, x1 = -1, y1 = -1;
            for (var i = 0; i < _open.Length; i++)
            {
                if (_open[i] != _was[i])
                {
                    var (x, y) = (i % width, i / width);
                    x0 = Math.Min(x0, x);
                    x1 = Math.Max(x1, x);
                    y0 = Math.Min(y0, y);
                    y1 = Math.Max(y1, y);
                }
            }

            if (x1 < 0)
            {
                return;
            }

            // A big change, such as a new field, is quicker done in sweeps over the whole map.
            if ((long)(x1 - x0 + 1) * (y1 - y0 + 1) * 8 < (long)width * height)
            {
                for (var y = Math.Max(0, y0 - MostClearance); y <= Math.Min(height - 1, y1 + MostClearance); y++)
                {
                    for (var x = Math.Max(0, x0 - MostClearance); x <= Math.Min(width - 1, x1 + MostClearance); x++)
                    {
                        var d = _open[(y * width) + x] ? Clearance(x, y) : 0;
                        _near[(y * width) + x] = (byte)d;
                        _crowding[(y * width) + x] = Weight(d);
                    }
                }

                return;
            }
        }

        _swept = true;
        var near = _near;
        var open = _open;

        // Down and across: each tile is one further than the closest of the four it has already seen.
        for (var y = 0; y < height; y++)
        {
            var row = y * width;
            for (var x = 0; x < width; x++)
            {
                if (!open[row + x])
                {
                    near[row + x] = 0;
                    continue;
                }

                // Off the edge of the map counts as something in the way.
                var d = Math.Min(Math.Min(x + 1, y + 1), Math.Min(width - x, height - y));
                if (x > 0)
                {
                    d = Math.Min(d, near[row + x - 1] + 1);
                }

                if (y > 0)
                {
                    var above = row - width + x;
                    d = Math.Min(d, near[above] + 1);
                    if (x > 0)
                    {
                        d = Math.Min(d, near[above - 1] + 1);
                    }

                    if (x < width - 1)
                    {
                        d = Math.Min(d, near[above + 1] + 1);
                    }
                }

                near[row + x] = (byte)Math.Min(d, MostClearance);
            }
        }

        // Back up and across the other way, picking up what is below and to the right.
        for (var y = height - 1; y >= 0; y--)
        {
            var row = y * width;
            for (var x = width - 1; x >= 0; x--)
            {
                int d = near[row + x];
                if (d == 0)
                {
                    _crowding[row + x] = 0f;
                    continue;
                }

                if (x < width - 1)
                {
                    d = Math.Min(d, near[row + x + 1] + 1);
                }

                if (y < height - 1)
                {
                    var below = row + width + x;
                    d = Math.Min(d, near[below] + 1);
                    if (x < width - 1)
                    {
                        d = Math.Min(d, near[below + 1] + 1);
                    }

                    if (x > 0)
                    {
                        d = Math.Min(d, near[below - 1] + 1);
                    }
                }

                near[row + x] = (byte)d;
                _crowding[row + x] = Weight(d);
            }
        }
    }

    /// <summary>How much harder it is to walk somewhere this many tiles from the nearest thing in the way.</summary>
    private static float Weight(int clearance) => clearance switch { 1 => 2.0f, 2 => 0.8f, 3 => 0.3f, _ => 0f };

    /// <summary>Clearance is only counted up to here: four tiles and more is open ground.</summary>
    private const int MostClearance = 4;

    /// <summary>True if a tile could be walked on at the last <see cref="Refresh"/>. Off the map cannot.</summary>
    private bool OpenAt(int x, int y) => x >= 0 && y >= 0 && x < _map.Width && y < _map.Height && _open[(y * _map.Width) + x];

    /// <summary>
    /// How many tiles it is to the nearest tile that cannot be walked on (up to 4), as at the last
    /// <see cref="Refresh"/>, looking all round. Used near something just built or taken away.
    /// </summary>
    private int Clearance(int x, int y)
    {
        for (var r = 1; r <= 3; r++)
        {
            for (var dy = -r; dy <= r; dy++)
            {
                for (var dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r && !OpenAt(x + dx, y + dy))
                    {
                        return r;
                    }
                }
            }
        }

        return 4;
    }

    /// <summary>
    /// How many tiles it is from a tile to the nearest one that cannot be walked on, up to four, as
    /// at the last <see cref="Refresh"/>. 0 on a tile that cannot be walked on; 2 or more means all
    /// eight tiles round it can be walked on.
    /// </summary>
    public int ClearanceAt(int x, int y) => _map.InBounds(x, y) ? _near[(y * _map.Width) + x] : 0;

    /// <summary>The weight <see cref="Refresh"/> gave a tile, for the tests.</summary>
    public float CrowdingAt(int x, int y) => _crowding[(y * _map.Width) + x];

    /// <summary>
    /// The weight a tile would have if worked out the slow way, from the map as it is now, for
    /// the tests to check the quick ways in <see cref="Refresh"/> agree.
    /// </summary>
    public float CrowdingTheSlowWay(int x, int y)
    {
        if (!Walkable(x, y))
        {
            return 0f;
        }

        for (var r = 1; r <= 3; r++)
        {
            for (var dy = -r; dy <= r; dy++)
            {
                for (var dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r && !Walkable(x + dx, y + dy))
                    {
                        return Weight(r);
                    }
                }
            }
        }

        return 0f;
    }

    /// <summary>
    /// The route from one tile to another as a list of tiles to walk through in order, with the
    /// straight stretches reduced to their ends. Null if there is no way through. Someone who keeps
    /// to paths will go out of their way to walk on one; everyone else cuts straight across.
    /// </summary>
    public List<TilePoint>? FindPath(TilePoint from, TilePoint to, bool keepToPaths = false)
    {
        if (!Walkable(from) || !Walkable(to))
        {
            return null;
        }

        // With no path anywhere near the way, there is nothing to keep to.
        if (keepToPaths && _anyPaths && PathNear(from, to) && ByPath(from, to) is { } byPath)
        {
            return byPath;
        }

        if (ClearLine(from, to))
        {
            // Open ground all the way: walk straight there.
            return from == to ? new List<TilePoint> { from } : new List<TilePoint> { from, to };
        }

        // Between two rented fields the way is through the gateways, one field at a time.
        if (_map.Waypoints(from, to) is { Count: > 0 } gateways)
        {
            var route = new List<TilePoint> { from };
            var here = from;
            foreach (var next in gateways.Append(to))
            {
                var leg = here == next ? null : ClearLine(here, next) ? new List<TilePoint> { here, next } : Search(here, next, false);
                if (here != next && leg is null)
                {
                    route = null;
                    break;
                }

                route.AddRange(leg?.Skip(1) ?? Enumerable.Empty<TilePoint>());
                here = next;
            }

            if (route is not null)
            {
                return route;
            }
        }

        return Search(from, to, false);
    }

    /// <summary>
    /// The way someone who keeps to paths goes: across the grass to the best place to join a path,
    /// along the paths, and off again at the best place to leave. Null if no path is worth it, so
    /// that they go straight across like anyone else.
    /// </summary>
    private List<TilePoint>? ByPath(TilePoint from, TilePoint to)
    {
        // Join where the walk to the path, and on from there, is shortest; leave the same way.
        TilePoint? join = null, leave = null;
        var best = double.MaxValue;
        foreach (var p in _pathPoints)
        {
            var score = (OffPath * Estimate(from.X, from.Y, p)) + Estimate(p.X, p.Y, to);
            if (score < best && Walkable(p))
            {
                (best, join) = (score, p);
            }
        }

        if (join is not { } j)
        {
            return null;
        }

        best = double.MaxValue;
        foreach (var p in _pathPoints)
        {
            var score = Estimate(j.X, j.Y, p) + (OffPath * Estimate(p.X, p.Y, to));
            if (score < best && Walkable(p))
            {
                (best, leave) = (score, p);
            }
        }

        // Worth it only if the paths (with the walk on and off them on the grass counting extra)
        // beat walking straight across the grass.
        var l = leave!.Value;
        var onPath = (OffPath * Estimate(from.X, from.Y, j)) + Estimate(j.X, j.Y, l) + (OffPath * Estimate(l.X, l.Y, to));
        if (onPath >= OffPath * Estimate(from.X, from.Y, to) || Estimate(j.X, j.Y, l) < 8)
        {
            return null;
        }

        var on = from == j ? new List<TilePoint> { j } : FindPath(from, j);
        var along = Search(j, l, true);
        var off = l == to ? new List<TilePoint> { l } : FindPath(l, to);
        if (on is null || along is null || off is null)
        {
            return null;
        }

        var route = new List<TilePoint>(on);
        route.AddRange(along.Skip(1));
        route.AddRange(off.Skip(1));
        return route;
    }

    /// <summary>A* across the tiles. Kept to paths, it only steps on path.</summary>
    private List<TilePoint>? Search(TilePoint from, TilePoint to, bool pathOnly)
    {
        var width = _map.Width;
        var cost = _cost;
        var cameFrom = _cameFrom;
        var searched = _searched;
        if (++_search == int.MaxValue)
        {
            Array.Clear(searched);
            _search = 1;
        }

        var search = _search;

        var start = (from.Y * width) + from.X;
        var goal = (to.Y * width) + to.X;
        var open = new PriorityQueue<int, double>();
        cost[start] = 0;
        cameFrom[start] = -1;
        searched[start] = search;
        cost[goal] = double.MaxValue;
        cameFrom[goal] = -1;
        searched[goal] = search;
        if (goal == start)
        {
            cost[start] = 0;
        }

        open.Enqueue(start, 0);

        while (open.TryDequeue(out var current, out var priority))
        {
            if (current == goal)
            {
                break;
            }

            var cx = current % width;
            var cy = current / width;
            if (priority > cost[current] + (Keen * Estimate(cx, cy, to)) + 1e-9)
            {
                continue; // an out-of-date entry
            }

            foreach (var (dx, dy) in Steps)
            {
                var nx = cx + dx;
                var ny = cy + dy;
                if (!Walkable(nx, ny))
                {
                    continue;
                }

                if (dx != 0 && dy != 0 && (!Walkable(cx + dx, cy) || !Walkable(cx, cy + dy)))
                {
                    continue; // no cutting corners
                }

                var next = (ny * width) + nx;
                if (pathOnly && !_onPath[next])
                {
                    continue;
                }

                var stepCost = (dx != 0 && dy != 0 ? Diagonal : 1) * (pathOnly ? 1 : 1 + _crowding[next]);
                var total = cost[current] + stepCost;
                if (searched[next] != search)
                {
                    searched[next] = search;
                    cost[next] = double.MaxValue;
                    cameFrom[next] = -1;
                }

                if (total < cost[next])
                {
                    cost[next] = total;
                    cameFrom[next] = current;
                    open.Enqueue(next, total + (Keen * Estimate(nx, ny, to)));
                }
            }
        }

        if (cameFrom[goal] < 0 && goal != start)
        {
            return null;
        }

        var path = new List<TilePoint>();
        for (var at = goal; at >= 0; at = cameFrom[at])
        {
            path.Add(new TilePoint(at % width, at / width));
            if (at == start)
            {
                break;
            }
        }

        path.Reverse();
        return Straighten(path);
    }

    /// <summary>
    /// True if the straight line between two tiles crosses nothing but open ground, well clear of
    /// anything in the way. No route can be shorter than that, so there is no need to search.
    /// </summary>
    private bool ClearLine(TilePoint from, TilePoint to)
    {
        var width = _map.Width;
        var (x, y) = (from.X, from.Y);
        var dx = Math.Abs(to.X - x);
        var dy = Math.Abs(to.Y - y);
        var sx = x < to.X ? 1 : -1;
        var sy = y < to.Y ? 1 : -1;
        var error = dx - dy;
        while (true)
        {
            // Close to either end the line may brush past something: that is where it is going.
            var nearEnd = Math.Max(Math.Abs(x - from.X), Math.Abs(y - from.Y)) <= EndReach || Math.Max(Math.Abs(x - to.X), Math.Abs(y - to.Y)) <= EndReach;
            if (!Walkable(x, y) || (!nearEnd && _crowding[(y * width) + x] != 0))
            {
                return false;
            }

            if (x == to.X && y == to.Y)
            {
                return true;
            }

            var twice = 2 * error;
            var stepX = twice > -dy;
            var stepY = twice < dx;
            if (stepX && stepY && (!Walkable(x + sx, y) || !Walkable(x, y + sy)))
            {
                return false; // no cutting corners
            }

            if (stepX)
            {
                error -= dy;
                x += sx;
            }

            if (stepY)
            {
                error += dx;
                y += sy;
            }
        }
    }

    private static double Estimate(int x, int y, TilePoint to)
    {
        var dx = Math.Abs(x - to.X);
        var dy = Math.Abs(y - to.Y);
        return Math.Max(dx, dy) + ((Diagonal - 1) * Math.Min(dx, dy));
    }

    /// <summary>Drops the tiles in the middle of each straight stretch.</summary>
    private static List<TilePoint> Straighten(List<TilePoint> path)
    {
        if (path.Count < 3)
        {
            return path;
        }

        var result = new List<TilePoint> { path[0] };
        for (var i = 1; i < path.Count - 1; i++)
        {
            var before = (path[i].X - path[i - 1].X, path[i].Y - path[i - 1].Y);
            var after = (path[i + 1].X - path[i].X, path[i + 1].Y - path[i].Y);
            if (before != after)
            {
                result.Add(path[i]);
            }
        }

        result.Add(path[^1]);
        return result;
    }
}
