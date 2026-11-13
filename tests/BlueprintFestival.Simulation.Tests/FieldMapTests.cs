using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

public class FieldMapTests
{
    [Fact]
    public void The_village_field_is_sixty_by_forty_metres_of_grass()
    {
        var map = FieldMap.Village();
        Assert.Equal(60, map.Field.Width * FieldMap.TileMetres);
        Assert.Equal(40, map.Field.Height * FieldMap.TileMetres);
        for (var y = map.Field.Y; y < map.Field.Bottom; y++)
        {
            for (var x = map.Field.X; x < map.Field.Right; x++)
            {
                Assert.Equal(Ground.Grass, map[x, y]);
            }
        }
    }

    [Fact]
    public void It_has_room_for_a_village_festival()
    {
        Assert.True(FieldMap.Village().ComfortableCapacity >= 200);
    }

    [Fact]
    public void A_hedge_runs_all_the_way_round_except_at_the_gap()
    {
        var map = FieldMap.Village();
        var ring = map.HedgeLine;
        var openings = 0;
        for (var x = ring.X; x < ring.Right; x++)
        {
            Assert.Equal(Ground.Hedge, map[x, ring.Y]);
            if (map[x, ring.Bottom - 1] != Ground.Hedge)
            {
                openings++;
                Assert.True(map.Gap.Contains(new TilePoint(x, ring.Bottom - 1)));
            }
        }

        for (var y = ring.Y; y < ring.Bottom; y++)
        {
            Assert.Equal(Ground.Hedge, map[ring.X, y]);
            Assert.Equal(Ground.Hedge, map[ring.Right - 1, y]);
        }

        Assert.Equal(map.Gap.Width, openings);
    }

    [Fact]
    public void Hedge_and_countryside_cannot_be_walked_on()
    {
        var map = FieldMap.Village();
        Assert.False(map.IsWalkable(map.HedgeLine.X, map.HedgeLine.Y));
        Assert.False(map.IsWalkable(0, 0));
        Assert.False(map.IsWalkable(-5, 3));
        Assert.Equal(Ground.Countryside, map[map.Width, 0]);
    }

    [Fact]
    public void You_can_walk_from_the_lane_to_every_part_of_the_field_and_only_through_the_gap()
    {
        var map = FieldMap.Village();
        Assert.True(map.IsWalkable(map.LaneStart));
        var reached = Flood(map, map.LaneStart, blocked: null);
        Assert.Contains(new TilePoint(map.Field.X, map.Field.Y), reached);
        Assert.Contains(new TilePoint(map.Field.Right - 1, map.Field.Bottom - 1), reached);

        var withGapShut = Flood(map, map.LaneStart, blocked: map.Gap);
        Assert.DoesNotContain(new TilePoint(map.Field.X, map.Field.Y), withGapShut);
    }

    [Fact]
    public void The_lane_crosses_the_whole_map()
    {
        var map = FieldMap.Village();
        var y = map.Lane.Y + 1;
        for (var x = 0; x < map.Width; x++)
        {
            Assert.Equal(Ground.Lane, map[x, y]);
        }
    }

    [Fact]
    public void Tiles_and_metres_convert_both_ways()
    {
        Assert.Equal(new TilePoint(4, 7), FieldMap.TileAt(2.2, 3.9));
        Assert.Equal((2.25, 3.75), FieldMap.CentreOf(new TilePoint(4, 7)));
        Assert.Equal(new TilePoint(-1, -1), FieldMap.TileAt(-0.1, -0.1));
    }

    [Fact]
    public void A_field_too_small_to_hold_a_festival_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FieldMap.Create(10, 40));
    }

    private static HashSet<TilePoint> Flood(FieldMap map, TilePoint from, TileRect? blocked)
    {
        var seen = new HashSet<TilePoint> { from };
        var queue = new Queue<TilePoint>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            foreach (var next in new[] { p with { X = p.X + 1 }, p with { X = p.X - 1 }, p with { Y = p.Y + 1 }, p with { Y = p.Y - 1 } })
            {
                if (map.IsWalkable(next) && blocked?.Contains(next) != true && seen.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return seen;
    }
}
