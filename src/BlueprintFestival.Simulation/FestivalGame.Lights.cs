using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation;

/// <summary>Lights for after dark: path lights, floodlights and fairy lights, and what the dark does to people.</summary>
public sealed partial class FestivalGame
{
    /// <summary>The line lights go under in the accounts.</summary>
    public const string LightsLine = "Lights";

    /// <summary>Whether a path light or floodlight could go here, counting the money.</summary>
    public BuildCheck CheckLight(LightKind kind, TilePoint at) => Afford(Site.CheckLight(kind, at));

    /// <summary>Puts up a path light or a floodlight and pays its hire.</summary>
    public BuildCheck BuildLight(LightKind kind, TilePoint at)
    {
        var check = CheckLight(kind, at);
        if (check.Ok)
        {
            Spend(LightsLine, check.Cost);
            Site.AddLight(kind, at);
        }

        return check;
    }

    /// <summary>Whether fairy lights could be strung between two tiles, counting the money. A drag that starts or ends by a pole ties on to it.</summary>
    public BuildCheck CheckFairyLights(TilePoint from, TilePoint to) => Afford(Site.CheckFairyLights(Site.SnapToPole(from), Site.SnapToPole(to)));

    /// <summary>Strings fairy lights between two tiles and pays for them. The poles go in by themselves.</summary>
    public BuildCheck BuildFairyLights(TilePoint from, TilePoint to)
    {
        (from, to) = (Site.SnapToPole(from), Site.SnapToPole(to));
        var check = CheckFairyLights(from, to);
        if (check.Ok)
        {
            Spend(LightsLine, check.Cost);
            Site.AddFairyLights(from, to);
        }

        return check;
    }

    /// <summary>How dark a place on the festival's ground is right now, from 0 (daylight, or well lit) to 1 (night with no light near).</summary>
    public double DarknessAt(double x, double y)
    {
        var night = 1 - Light;
        if (night <= 0.01)
        {
            return 0;
        }

        // The road outside is not the festival's to light, and nobody blames the festival for it.
        var tile = FieldMap.TileAt(x, y);
        return Field.InBounds(tile.X, tile.Y) && Field.Rented(tile) ? night * (1 - Site.LitAt(tile)) : 0;
    }

    /// <summary>How dark it has to be somewhere before people there count as being in the dark.</summary>
    public const double TooDark = 0.5;

    /// <summary>How many people on the site are somewhere dark right now.</summary>
    public int InTheDark => Light > 1 - TooDark ? 0 : Crowd.Parties
        .Where(p => People.Crowd.MindsTheDark(p) && DarknessAt(p.X, p.Y) >= TooDark)
        .Sum(p => p.Members.Count);
}
