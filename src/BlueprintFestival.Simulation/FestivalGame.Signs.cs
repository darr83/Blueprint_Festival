using System.Collections.Generic;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation;

/// <summary>Signs the player puts up and writes on.</summary>
public sealed partial class FestivalGame
{
    /// <summary>Whether a sign could go with its corner on a tile, counting the money.</summary>
    public BuildCheck CheckSign(SignKind kind, int x, int y, Facing facing) => Afford(Site.CheckSign(kind, x, y, facing));

    /// <summary>Puts a sign up and pays for it. It comes with a few words on, to be written over.</summary>
    public BuildCheck BuildSign(SignKind kind, int x, int y, Facing facing)
    {
        var check = CheckSign(kind, x, y, facing);
        if (check.Ok)
        {
            Spend(Signs.Line, check.Cost);
            Site.AddSign(kind, x, y, facing);
        }

        return check;
    }

    /// <summary>Writes new words on the sign standing on a tile, paints it and points its fingers. Free, at any time.</summary>
    public bool WriteSign(TilePoint at, IEnumerable<string?>? lines, int colour, int points) => Site.WriteSign(at, lines, colour, points);
}
