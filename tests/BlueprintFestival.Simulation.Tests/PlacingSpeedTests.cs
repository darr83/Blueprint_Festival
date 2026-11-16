using System.Diagnostics;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

/// <summary>
/// Putting things down in build mode on a big site. Every placement makes the game work out
/// again where people can walk and how tight each spot is; that used to take about half a second
/// on a 5,000 site, so every click hung the game.
/// </summary>
[Collection(BigDays.Name)]
public class PlacingSpeedTests
{
    /// <summary>Checks every tile's weight against working it out the slow way, looking all round it.</summary>
    private static void AgreesWithTheSlowWay(FieldMap map, PathFinder paths, string after)
    {
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                if (paths.CrowdingAt(x, y) != paths.CrowdingTheSlowWay(x, y))
                {
                    Assert.Fail($"After {after}: tile ({x}, {y}) weighs {paths.CrowdingAt(x, y)} but should weigh {paths.CrowdingTheSlowWay(x, y)}.");
                }
            }
        }
    }

    [Fact]
    public void The_quick_way_of_finding_tight_spots_agrees_with_looking_all_round_every_tile()
    {
        var game = GrowthTests.Built(1500, 200000);
        var map = game.Field;
        var f = map.Field;
        var finders = new[] { new PathFinder(map), new PathFinder(map, staff: true), new PathFinder(map, vip: true) };
        void Check(string after)
        {
            foreach (var paths in finders)
            {
                paths.Refresh();
                AgreesWithTheSlowWay(map, paths, after);
            }
        }

        Check("building the site");
        var (cx, cy) = (f.X + (f.Width / 2), f.Y + (f.Height / 2));
        Assert.True(game.BuildStall(StallKind.Burgers, cx, cy, Facing.South).Ok);
        Check("a stall");
        Assert.True(game.BuildComfort(ComfortKind.Bench, cx + 12, cy, Facing.East).Ok);
        Check("a bench");
        Assert.True(game.BuildFence(FenceKind.SteelMesh, new TilePoint(cx - 20, cy + 14), new TilePoint(cx + 20, cy + 14)).Ok);
        Check("a long fence");
        Assert.True(game.KeepOut(new TilePoint(cx - 10, cy + 20), new TilePoint(cx, cy + 24), true).Ok);
        Check("ground kept clear");
        Assert.True(game.VipGround(new TilePoint(cx + 10, cy + 20), new TilePoint(cx + 16, cy + 26), true).Ok);
        Check("VIP ground");
        Assert.True(game.Remove(new TilePoint(cx + 1, cy + 1)) > 0);
        Check("taking the stall away");
        Assert.True(game.Remove(new TilePoint(cx, cy + 14)) > 0);
        Check("taking a stretch of fence away");

        // Along the edge of the field, where the hedge and the edge of the map are close.
        Assert.True(game.BuildStall(StallKind.WaterPoint, f.X + 1, f.Bottom - 5, Facing.North).Ok);
        Check("a stall by the hedge");
    }

    [Fact]
    public void Putting_things_down_on_a_big_site_is_quick()
    {
        var game = GrowthTests.Built(5000, 400000, camping: true);
        var f = game.Field.Field;
        var (cx, cy) = (f.X + (f.Width / 2) - 40, f.Y + (f.Height / 2));

        // One of each first, so nothing is timed loading for the first time.
        Assert.True(game.BuildComfort(ComfortKind.Bench, cx, cy - 8, Facing.South).Ok);
        var placed = 0;
        var clock = Stopwatch.StartNew();
        for (var i = 0; i < 6; i++)
        {
            Assert.Null(game.BuildComfort(ComfortKind.Bench, cx + (i * 8), cy, Facing.South).Problem);
            Assert.Null(game.BuildLight(LightKind.PathLight, new TilePoint(cx + (i * 8), cy + 8)).Problem);
            Assert.Null(game.BuildDecor(DecorKind.FlowerPlanter, cx + (i * 8), cy + 14, Facing.South).Problem);
            Assert.Null(game.BuildStall(StallKind.Burgers, cx + (i * 12), cy + 22, Facing.South).Problem);
            Assert.Null(game.BuildFence(FenceKind.SteelMesh, new TilePoint(cx + (i * 12), cy + 36), new TilePoint(cx + (i * 12) + 6, cy + 36)).Problem);
            placed += 5;
        }

        // About 20 ms each in a debug build (it was over 500 ms). The limit leaves
        // room for a slow test machine.
        var each = clock.Elapsed.TotalMilliseconds / placed;
        Assert.True(each < 150, $"Putting something down took {each:0} ms.");
    }

    [Fact]
    public void Dragging_out_a_big_patch_of_ground_tells_the_crowd_once_and_is_quick()
    {
        var game = GrowthTests.Built(5000, 400000, camping: true);
        Assert.False(game.Crowd.HasPaths);
        var (c0, r0) = (game.WearColumns / 2, 20);
        var clock = Stopwatch.StartNew();
        game.LayGround(GroundKind.Gravel, c0, r0, c0 + 29, r0 + 2);
        var took = clock.Elapsed.TotalMilliseconds;

        // Every patch is laid and paid for, and the crowd knows there is path to keep to.
        for (var r = r0; r <= r0 + 2; r++)
        {
            for (var c = c0; c <= c0 + 29; c++)
            {
                Assert.Equal(GroundKind.Gravel, game.GroundAt(c, r));
            }
        }

        Assert.True(game.Crowd.HasPaths);

        // 90 patches: it used to tell the crowd after every one, which took nearly a second.
        Assert.True(took < 400, $"Laying 90 patches took {took:0} ms.");
    }
}
