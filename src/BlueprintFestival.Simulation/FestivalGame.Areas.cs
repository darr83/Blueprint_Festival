using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation;

/// <summary>
/// Crowd areas: pens the player draws in front of a stage, each with a limit on how many people
/// get in. Security on a gap count people in and close it once the area is full; with nobody on
/// its gaps the limit is not kept.
/// </summary>
public sealed partial class FestivalGame
{
    /// <summary>How close a member of security must stand to a gap to keep it, in metres.</summary>
    public const double GapReach = 3.5;

    /// <summary>Whether a crowd area could be drawn here, counting the money.</summary>
    public BuildCheck CheckArea(AreaKind kind, TilePoint from, TilePoint to) => Afford(Site.CheckArea(kind, from, to));

    /// <summary>Draws a crowd area and pays for the barrier round it, if it fits and there is the money.</summary>
    public BuildCheck BuildArea(AreaKind kind, TilePoint from, TilePoint to)
    {
        var check = CheckArea(kind, from, to);
        if (check.Ok)
        {
            Spend("Fences and gates", check.Cost);
            Site.AddArea(kind, from, to);
        }

        return check;
    }

    /// <summary>Sets how many people security let into an area. It can be changed at any time, on the day too.</summary>
    public int? SetAreaLimit(int area, int limit) => Site.SetAreaLimit(area, limit);

    /// <summary>Opens or closes a gap in an area's barrier. Only while building. Returns a problem, or null.</summary>
    public string? ToggleAreaGap(TilePoint p) => !CanBuild ? "Nothing can be built just now." : Site.ToggleGap(p);

    /// <summary>True if a member of security is at their post by one of an area's gaps, counting people in.</summary>
    public bool AreaManned(int area)
    {
        if (area < 0 || area >= Site.Areas.Count)
        {
            return false;
        }

        var a = Site.Areas[area];
        foreach (var gap in a.Gaps)
        {
            var (gx, gy) = FieldMap.CentreOf(gap);
            foreach (var s in _staff)
            {
                if (s.Kind == StaffKind.Security && s.State is StaffState.AtPost or StaffState.Holding && Distance(s.X, s.Y, gx, gy) <= GapReach)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>True for security posted by a gap of a crowd area: they stay there counting people in rather than walk a beat.</summary>
    private bool OnAGap(StaffMember member)
    {
        foreach (var area in Site.Areas)
        {
            foreach (var gap in area.Gaps)
            {
                var (gx, gy) = FieldMap.CentreOf(gap);
                if (Distance(member.PostX, member.PostY, gx, gy) <= GapReach)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>How many security an area needs: one for each gap.</summary>
    public static int SecurityFor(PlacedArea area) => area.Gaps.Count;

    /// <summary>How many of an area's gaps have security by them.</summary>
    public int GapsManned(int area)
    {
        if (area < 0 || area >= Site.Areas.Count)
        {
            return 0;
        }

        var manned = 0;
        foreach (var gap in Site.Areas[area].Gaps)
        {
            var (gx, gy) = FieldMap.CentreOf(gap);
            if (_staff.Any(s => s.Kind == StaffKind.Security && s.State is StaffState.AtPost or StaffState.Holding && Distance(s.X, s.Y, gx, gy) <= GapReach))
            {
                manned++;
            }
        }

        return manned;
    }

    /// <summary>Raised when an area fills up to its limit while security keep its gaps, with the area's number.</summary>
    public event Action<int>? AreaFull;

    private void StartAreas()
    {
        Crowd.AreaManned = AreaManned;
        Crowd.AreaFilled += area => AreaFull?.Invoke(area);
    }
}
