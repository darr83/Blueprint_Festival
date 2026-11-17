using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

/// <summary>
/// Route finding is most of the work of a big festival day, so it heads for where it is going
/// rather than trying every way round, and the routes it finds stay very nearly the shortest.
/// </summary>
public class RouteSpeedTests
{
    /// <summary>The showground with three long rows of stalls across it, each with a way through at one end.</summary>
    private static FieldMap Showground()
    {
        var map = FieldMap.ForLicence(5000);
        var f = map.Field;
        var blocked = new List<TilePoint>();
        for (var row = 0; row < 3; row++)
        {
            var y = f.Y + 30 + (row * 40);
            var gapLeft = row % 2 == 0;
            for (var x = f.X + (gapLeft ? 30 : 0); x < f.Right - (gapLeft ? 0 : 30); x++)
            {
                for (var dy = 0; dy < 4; dy++)
                {
                    blocked.Add(new TilePoint(x, y + dy));
                }
            }
        }

        map.SetBlocked(blocked);
        return map;
    }

    private static IEnumerable<(TilePoint From, TilePoint To)> Walks(FieldMap map)
    {
        var f = map.Field;
        yield return (map.LaneStart, new TilePoint(f.X + 20, f.Bottom - 10));
        yield return (new TilePoint(f.X + 10, f.Y + 10), new TilePoint(f.Right - 10, f.Bottom - 10));
        yield return (new TilePoint(f.Right - 10, f.Y + 10), new TilePoint(f.X + 40, f.Y + 100));
        yield return (new TilePoint(f.X + 80, f.Bottom - 5), new TilePoint(f.X + 200, f.Y + 5));

        // And a spread of walks between places all over the field.
        var random = new Random(7);
        var picked = 0;
        while (picked < 30)
        {
            var a = new TilePoint(random.Next(f.X, f.Right), random.Next(f.Y, f.Bottom));
            var b = new TilePoint(random.Next(f.X, f.Right), random.Next(f.Y, f.Bottom));
            if (map.IsWalkable(a) && map.IsWalkable(b))
            {
                picked++;
                yield return (a, b);
            }
        }
    }

    [Fact]
    public void Routes_round_rows_of_stalls_are_found_without_searching_most_of_the_map()
    {
        var map = Showground();
        var finder = new PathFinder(map);
        var walks = Walks(map).ToList();
        var log = "";
        foreach (var (from, to) in walks)
        {
            var before = finder.TilesSearched;
            Assert.NotNull(finder.FindPath(from, to));
            log += $" {finder.TilesSearched - before:N0}";
        }

        // The showground map is over 220,000 tiles; before, these looked at nearly 50,000 tiles each.
        var each = finder.TilesSearched / walks.Count;
        Assert.True(each < 35000, $"{each:N0} tiles searched a route:{log}");
    }

    [Fact]
    public void Those_routes_are_hardly_any_longer_than_the_shortest_way()
    {
        var map = Showground();
        var finder = new PathFinder(map);
        foreach (var (from, to) in Walks(map))
        {
            var route = finder.FindPath(from, to)!;
            var walked = Length(route);
            var shortest = Shortest(map, from, to);
            // A little extra is allowed for keeping clear of the stalls' corners and the hedges.
            Assert.True(walked <= shortest * 1.1, $"{from} to {to}: {walked:0} tiles walked, {shortest:0} at shortest.");
        }
    }

    private static double Length(List<TilePoint> route)
    {
        var total = 0.0;
        for (var i = 1; i < route.Count; i++)
        {
            var dx = Math.Abs(route[i].X - route[i - 1].X);
            var dy = Math.Abs(route[i].Y - route[i - 1].Y);
            total += Math.Max(dx, dy) + (0.41421356 * Math.Min(dx, dy));
        }

        return total;
    }

    /// <summary>The shortest walk over open tiles, worked out the slow, sure way (Dijkstra, no corner cutting).</summary>
    private static double Shortest(FieldMap map, TilePoint from, TilePoint to)
    {
        var w = map.Width;
        var cost = new double[w * map.Height];
        Array.Fill(cost, double.MaxValue);
        var open = new PriorityQueue<int, double>();
        cost[(from.Y * w) + from.X] = 0;
        open.Enqueue((from.Y * w) + from.X, 0);
        while (open.TryDequeue(out var at, out var d))
        {
            if (d > cost[at])
            {
                continue;
            }

            var (x, y) = (at % w, at / w);
            if (x == to.X && y == to.Y)
            {
                return d;
            }

            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    if ((dx == 0 && dy == 0) || !map.IsWalkable(x + dx, y + dy)
                        || (dx != 0 && dy != 0 && (!map.IsWalkable(x + dx, y) || !map.IsWalkable(x, y + dy))))
                    {
                        continue;
                    }

                    var next = ((y + dy) * w) + x + dx;
                    var step = d + (dx != 0 && dy != 0 ? 1.41421356 : 1);
                    if (step < cost[next])
                    {
                        cost[next] = step;
                        open.Enqueue(next, step);
                    }
                }
            }
        }

        return double.MaxValue;
    }
}
