using BlueprintFestival.Simulation.Site;

namespace BlueprintFestival.Simulation;

/// <summary>Seats, hand washing and shade: the comforts the player dots about the field.</summary>
public sealed partial class FestivalGame
{
    /// <summary>Whether a comfort could go with its corner on a tile, counting the money.</summary>
    public BuildCheck CheckComfort(ComfortKind kind, int x, int y, Facing facing) => Afford(Site.CheckComfort(kind, x, y, facing));

    /// <summary>Puts out a seat, a sink or a sail and pays its hire for the day.</summary>
    public BuildCheck BuildComfort(ComfortKind kind, int x, int y, Facing facing)
    {
        var check = CheckComfort(kind, x, y, facing);
        if (check.Ok)
        {
            Spend(Comforts.Line, check.Cost);
            Site.AddComfort(kind, x, y, facing);
        }

        return check;
    }

    /// <summary>How many seats have been put out, counting every place on every bench, chair and table.</summary>
    public int Seats => Site.Comforts.Sum(c => Comforts.Of(c.Kind).Seats.Length);

    /// <summary>How well the festival looks after clean hands, as a share of its toilets and food stalls with somewhere to wash near, from 0 to 100.</summary>
    public int HygienePercent => (int)System.Math.Round(Site.Hygiene * 100);
}
