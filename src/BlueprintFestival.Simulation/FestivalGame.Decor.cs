using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation;

/// <summary>Decorations: bunting, flags, flowers, a fire pit and the festival's name in giant letters.</summary>
public sealed partial class FestivalGame
{
    /// <summary>The word giant letters spell here: the first word of the festival's name.</summary>
    public string LettersWord => Decor.WordFor(Name);

    /// <summary>Whether a decoration could go with its corner on a tile, counting the money.</summary>
    public BuildCheck CheckDecor(DecorKind kind, int x, int y, Facing facing) => Afford(Site.CheckDecor(kind, x, y, facing, LettersWord));

    /// <summary>Puts a decoration out and pays for it.</summary>
    public BuildCheck BuildDecor(DecorKind kind, int x, int y, Facing facing)
    {
        var check = CheckDecor(kind, x, y, facing);
        if (check.Ok)
        {
            Spend(Decor.Line, check.Cost);
            Site.AddDecor(kind, x, y, facing, LettersWord);
        }

        return check;
    }

    /// <summary>Whether bunting could be strung between two tiles, counting the money.</summary>
    public BuildCheck CheckBunting(TilePoint from, TilePoint to) => Afford(Site.CheckBunting(from, to));

    /// <summary>Strings bunting between two tiles, a pole at each end, and pays for it by the metre.</summary>
    public BuildCheck BuildBunting(TilePoint from, TilePoint to)
    {
        var check = CheckBunting(from, to);
        if (check.Ok)
        {
            Spend(Decor.Line, check.Cost);
            Site.AddBunting(from, to);
        }

        return check;
    }
}
