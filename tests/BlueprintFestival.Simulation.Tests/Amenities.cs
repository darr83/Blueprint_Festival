using BlueprintFestival.Simulation.Site;

namespace BlueprintFestival.Simulation.Tests;

/// <summary>Food, drink and toilets down the east hedge, so a test festival is one people are happy to stay at.</summary>
internal static class Amenities
{
    public static void Add(FestivalGame game)
    {
        var f = game.Field.Field;
        var x = f.Right - 16;
        Assert.True(game.BuildStall(StallKind.FoodVan, x, f.Y + 8, Facing.West).Ok);
        Assert.True(game.BuildStall(StallKind.Bar, x, f.Y + 24, Facing.West).Ok);
        Assert.True(game.BuildStall(StallKind.Toilets, x, f.Y + 40, Facing.West).Ok);
        Assert.True(game.BuildStall(StallKind.Toilets, x, f.Y + 50, Facing.West).Ok);
        Assert.True(game.BuildStall(StallKind.WaterPoint, x, f.Y + 60, Facing.West).Ok);
    }
}
