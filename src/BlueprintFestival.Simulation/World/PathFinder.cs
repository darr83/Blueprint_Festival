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

    private readonly FieldMap _map;
    private readonly float[] _crowding;

    // Kept between searches and marked with the search's number, so a route across a big field
    // does not have to wipe the whole map first.
    private readonly double[] _cost;
    private readonly int[] _cameFrom;
    private readonly int[] _searched;
    private int _search;

    public PathFinder(FieldMap map)
    {
        _map = map;
        _crowding = new float[map.Width * map.Height];
        _cost = new double[_crowding.Length];
        _cameFrom = new int[_crowding.Length];
        _searched = new int[_crowding.Length];
        Refresh();
    }

    /// <summary>Works out again where the tight spots are. Call after anything is built or removed.</summary>
    public void Refresh()
    {
        var map = _map;
        Array.Clear(_crowding);
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                if (map.IsWalkable(x, y))
                {
                    _crowding[(y * map.Width) + x] = Clearance(x, y) switch { 1 => 2.0f, 2 => 0.8f, 3 => 0.3f, _ => 0f };
                }
            }
        }
    }

    /// <summary>How many tiles it is to the nearest tile that cannot be walked on (up to 4).</summary>
    private int Clearance(int x, int y)
    {
        for (var r = 1; r <= 3; r++)
        {
            for (var dy = -r; dy <= r; dy++)
            {
                for (var dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r && !_map.IsWalkable(x + dx, y + dy))
                    {
                        return r;
                    }
                }
            }
        }

        return 4;
    }

    /// <summary>
    /// The route from one tile to another as a list of tiles to walk through in order, with the
    /// straight stretches reduced to their ends. Null if there is no way through.
    /// </summary>
    public List<TilePoint>? FindPath(TilePoint from, TilePoint to)
    {
        if (!_map.IsWalkable(from) || !_map.IsWalkable(to))
        {
            return null;
        }

        if (ClearLine(from, to))
        {
            // Open ground all the way: walk straight there.
            return from == to ? new List<TilePoint> { from } : new List<TilePoint> { from, to };
        }

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
                if (!_map.IsWalkable(nx, ny))
                {
                    continue;
                }

                if (dx != 0 && dy != 0 && (!_map.IsWalkable(cx + dx, cy) || !_map.IsWalkable(cx, cy + dy)))
                {
                    continue; // no cutting corners
                }

                var next = (ny * width) + nx;
                var stepCost = (dx != 0 && dy != 0 ? Diagonal : 1) * (1 + _crowding[next]);
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
            if (!_map.IsWalkable(x, y) || (!nearEnd && _crowding[(y * width) + x] != 0))
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
            if (stepX && stepY && (!_map.IsWalkable(x + sx, y) || !_map.IsWalkable(x, y + sy)))
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
