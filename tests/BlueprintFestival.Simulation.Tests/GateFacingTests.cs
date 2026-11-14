using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

/// <summary>Gates stand across the gap they are put in, with their front (lanes, sign, ticket table) to the way people come in.</summary>
public class GateFacingTests
{
    private static FestivalGame Year(string field)
    {
        var game = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(4, 1500, 40000, false, 80))
            .WithFields(new FieldPlan(new[] { new FieldChoice(field, FieldUse.Music) }))!;
        Assert.True(game.ConfirmPlan());
        return game;
    }

    /// <summary>The middle tile of the gap in the hedge, along the line a gate goes on.</summary>
    private static (TilePoint Middle, bool Vertical) GapLine(FieldMap map)
    {
        var gap = map.Gap;
        var vertical = map.GapRunsNorthSouth(new TilePoint(gap.X + (gap.Width / 2), gap.Y + (gap.Height / 2)))!.Value;
        var middle = vertical ? new TilePoint(gap.X + 1, gap.Y + (gap.Height / 2)) : new TilePoint(gap.X + (gap.Width / 2), gap.Y + 1);
        return (middle, vertical);
    }

    [Theory]
    [InlineData("home")] // north of the lane
    [InlineData("church")] // south of the lane
    [InlineData("show")] // in off the farm track to the east
    public void Every_gate_faces_out_of_the_gap_towards_the_lane(string field)
    {
        foreach (var kind in Enum.GetValues<GateKind>())
        {
            var game = Year(field);
            var map = game.Field;
            var (middle, vertical) = GapLine(map);
            var width = Catalogue.Of(kind).Width;
            var (x, y) = vertical ? (middle.X, middle.Y - (width / 2)) : (middle.X - (width / 2), middle.Y);
            var gate = new PlacedGate(kind, x, y, vertical);

            // While it is being placed.
            var (ox, oy) = game.Site.FrontOf(gate);
            Assert.Equal(vertical, ox != 0);
            Assert.Equal(Ground.Lane, map[middle.X + (ox * 6), middle.Y + (oy * 6)]);
            Assert.Equal(Ground.Grass, map[middle.X - (ox * 6), middle.Y - (oy * 6)]);

            // And once it is up with fence closing the rest of the gap.
            var gap = map.Gap;
            var from = vertical ? new TilePoint(middle.X, gap.Y) : new TilePoint(gap.X, middle.Y);
            var to = vertical ? new TilePoint(middle.X, gap.Bottom - 1) : new TilePoint(gap.Right - 1, middle.Y);
            var fenced = game.BuildFence(FenceKind.Rope, from, to);
            Assert.True(fenced.Ok, fenced.Problem);
            Assert.True(game.BuildGate(kind, x, y, vertical).Ok);
            Assert.True(game.Site.GateUses[0].InUse);
            Assert.Equal((ox, oy), game.Site.FrontOf(game.Site.Gates[0]));
        }
    }

    [Fact]
    public void Only_tiles_in_a_gap_say_which_way_it_runs()
    {
        var map = Year("show").Field;
        Assert.True(map.GapRunsNorthSouth(new TilePoint(map.Gap.X, map.Gap.Y)));
        Assert.Null(map.GapRunsNorthSouth(new TilePoint(map.Field.X + 20, map.Field.Y + 20)));
        var home = Year("home").Field;
        Assert.False(home.GapRunsNorthSouth(new TilePoint(home.Gap.X, home.Gap.Y)));
    }
}
