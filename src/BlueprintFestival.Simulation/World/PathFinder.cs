namespace BlueprintFestival.Simulation.World;

/// <summary>
/// Finds walking routes across the tile grid (A*). People can step to any of the eight tiles round
/// them but never cut a corner, and routes keep a little clear of hedges and edges so that people
/// walk down the middle of a lane or a gateway rather than brushing along its side.
/// </summary>
public sealed class PathFinder
{
    private const double Diagonal = 1.41421356;
    private static readonly (int Dx, int Dy)[] Steps = { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1) };

    private readonly FieldMap _map;
    private readonly float[] _crowding;

    public PathFinder(FieldMap map)
    {
        _map = map;
        _crowding = new float[map.Width * map.Height];
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

        var width = _map.Width;
        var count = width * _map.Height;
        var cost = new double[count];
        var cameFrom = new int[count];
        Array.Fill(cost, double.MaxValue);
        Array.Fill(cameFrom, -1);

        var start = (from.Y * width) + from.X;
        var goal = (to.Y * width) + to.X;
        var open = new PriorityQueue<int, double>();
        cost[start] = 0;
        open.Enqueue(start, 0);

        while (open.TryDequeue(out var current, out var priority))
        {
            if (current == goal)
            {
                break;
            }

            var cx = current % width;
            var cy = current / width;
            if (priority > cost[current] + Estimate(cx, cy, to) + 1e-9)
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
                if (total < cost[next])
                {
                    cost[next] = total;
                    cameFrom[next] = current;
                    open.Enqueue(next, total + Estimate(nx, ny, to));
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
