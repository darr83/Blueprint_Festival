using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

public class PathFinderTests
{
    private static readonly FieldMap Map = FieldMap.Village();
    private static readonly PathFinder Paths = new(Map);

    private static TilePoint FieldCorner => new(Map.Field.Right - 5, Map.Field.Y + 5);

    [Fact]
    public void There_is_a_way_from_the_lane_into_the_field_and_it_goes_through_the_gap()
    {
        var path = Paths.FindPath(Map.LaneStart, FieldCorner);
        Assert.NotNull(path);
        Assert.Equal(Map.LaneStart, path![0]);
        Assert.Equal(FieldCorner, path[^1]);
        Assert.Contains(Walked(path), Map.Gap.Contains);
    }

    [Fact]
    public void Every_step_of_a_route_is_on_ground_you_can_walk_on()
    {
        var path = Paths.FindPath(Map.LaneStart, FieldCorner)!;
        Assert.All(Walked(path), p => Assert.True(Map.IsWalkable(p)));
    }

    [Fact]
    public void Routes_never_cut_the_corner_of_a_hedge()
    {
        var path = Paths.FindPath(Map.LaneStart, FieldCorner)!;
        var steps = Walked(path).ToList();
        for (var i = 1; i < steps.Count; i++)
        {
            var (a, b) = (steps[i - 1], steps[i]);
            if (a.X != b.X && a.Y != b.Y)
            {
                Assert.True(Map.IsWalkable(b.X, a.Y) && Map.IsWalkable(a.X, b.Y));
            }
        }
    }

    [Fact]
    public void People_walk_through_the_middle_of_the_gap_not_along_its_edge()
    {
        var path = Paths.FindPath(Map.LaneStart, FieldCorner)!;
        var inGap = Walked(path).Where(Map.Gap.Contains).ToList();
        Assert.NotEmpty(inGap);
        Assert.All(inGap, p => Assert.InRange(p.X, Map.Gap.X + 2, Map.Gap.Right - 3));
    }

    [Fact]
    public void Straight_stretches_are_reduced_to_their_ends()
    {
        var a = new TilePoint(Map.Field.X + 10, Map.Field.Y + 20);
        var b = new TilePoint(Map.Field.X + 60, Map.Field.Y + 20);
        Assert.Equal(new[] { a, b }, Paths.FindPath(a, b));
    }

    [Fact]
    public void A_route_to_where_you_already_are_is_just_that_tile()
    {
        Assert.Equal(new[] { FieldCorner }, Paths.FindPath(FieldCorner, FieldCorner));
    }

    [Fact]
    public void There_is_no_route_into_a_hedge_or_out_into_the_countryside()
    {
        Assert.Null(Paths.FindPath(Map.LaneStart, new TilePoint(Map.HedgeLine.X, Map.HedgeLine.Y)));
        Assert.Null(Paths.FindPath(Map.LaneStart, new TilePoint(2, 2)));
        Assert.Null(Paths.FindPath(new TilePoint(2, 2), FieldCorner));
    }

    /// <summary>Every tile stepped on along a route, filling in the straight stretches.</summary>
    private static IEnumerable<TilePoint> Walked(List<TilePoint> path)
    {
        yield return path[0];
        for (var i = 1; i < path.Count; i++)
        {
            var at = path[i - 1];
            while (at != path[i])
            {
                at = new TilePoint(at.X + Math.Sign(path[i].X - at.X), at.Y + Math.Sign(path[i].Y - at.Y));
                yield return at;
            }
        }
    }
}
