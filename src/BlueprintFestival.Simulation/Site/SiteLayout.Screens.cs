using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Site;

/// <summary>The sizes of big screen the player can put up.</summary>
public enum ScreenKind
{
    Small,
    Medium,
    Large,
}

/// <summary>
/// What there is to know about a size of big screen: its hire for the day, the tiles it stands on
/// (across and front to back when it faces south), the size of its picture in metres, how many
/// speaker stacks hang beside it, and how far in front of it people can see and hear it well.
/// </summary>
public sealed record ScreenInfo(ScreenKind Kind, string Name, int Cost, int Width, int Depth, double PictureWidth, double PictureHeight, int Speakers, double Reach, string About);

/// <summary>A big screen that has been put up. It faces the way people watch it from.</summary>
public sealed record PlacedScreen(ScreenKind Kind, int X, int Y, Facing Facing)
{
    public TileRect Rect
    {
        get
        {
            var info = BigScreens.Of(Kind);
            return Facing is Facing.South or Facing.North ? new TileRect(X, Y, info.Width, info.Depth) : new TileRect(X, Y, info.Depth, info.Width);
        }
    }
}

/// <summary>
/// The big screens: a screen on a scaffold tower with speakers hung beside it, showing the stage.
/// Far from a stage an act is hard to see and hear, so people at the back enjoy it less; a screen
/// with its speakers brings the act to everyone in front of it.
/// </summary>
public static class BigScreens
{
    public static readonly IReadOnlyList<ScreenInfo> All = new[]
    {
        new ScreenInfo(ScreenKind.Small, "Small screen", 250, 14, 6, 4.0, 2.25, 1, 25, "A 4 m screen on a scaffold tower with a stack of speakers beside it."),
        new ScreenInfo(ScreenKind.Medium, "Medium screen", 600, 18, 6, 6.0, 3.4, 2, 40, "A 6 m screen on a scaffold tower with speakers hung either side."),
        new ScreenInfo(ScreenKind.Large, "Large screen", 1200, 24, 6, 9.0, 5.0, 4, 60, "A 9 m screen on a tall scaffold tower with big speaker hangs either side."),
    };

    public static ScreenInfo Of(ScreenKind kind) => All[(int)kind];

    /// <summary>Out to here from the front of a stage, people see and hear the act well enough without help.</summary>
    public const double ClearMetres = 20;

    /// <summary>From here back, people get the least out of an act without a screen.</summary>
    public const double FarMetres = 60;

    /// <summary>How much of an act people at the very back enjoy without a screen.</summary>
    public const double FarEnjoyment = 0.5;

    /// <summary>How wide the ground a screen covers is, in degrees either side of straight ahead.</summary>
    public const double SpreadDegrees = 60;

    /// <summary>The line screens go under in the accounts.</summary>
    public const string Line = "Big screens";

    /// <summary>How much of an act someone this far from the front of the stage gets, with no screen to help.</summary>
    public static double Enjoyment(double metresFromStage) =>
        metresFromStage <= ClearMetres ? 1
        : metresFromStage >= FarMetres ? FarEnjoyment
        : 1 - ((1 - FarEnjoyment) * (metresFromStage - ClearMetres) / (FarMetres - ClearMetres));

    /// <summary>True if a place, in metres, is in front of a screen and close enough to see and hear it well.</summary>
    public static bool Covers(PlacedScreen screen, double x, double y)
    {
        var (fx, fy, dx, dy) = SiteLayout.Front(screen.Rect, screen.Facing);
        var (vx, vy) = (x - fx, y - fy);
        var ahead = (vx * dx) + (vy * dy);
        if (ahead <= 0)
        {
            return false;
        }

        var metres = Math.Sqrt((vx * vx) + (vy * vy));
        return metres <= Of(screen.Kind).Reach && ahead / metres >= Math.Cos(SpreadDegrees * Math.PI / 180);
    }
}

public sealed partial class SiteLayout
{
    private readonly List<PlacedScreen> _screens = new();

    /// <summary>The big screens that are up.</summary>
    public IReadOnlyList<PlacedScreen> Screens => _screens;

    public PlacedScreen? ScreenAt(TilePoint p) => _screens.FirstOrDefault(s => s.Rect.Contains(p));

    /// <summary>True if a place, in metres, can see and hear a big screen well.</summary>
    public bool OnScreen(double x, double y)
    {
        foreach (var screen in _screens)
        {
            if (BigScreens.Covers(screen, x, y))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether a big screen can go with its corner on a tile, and what it costs.</summary>
    public BuildCheck CheckScreen(ScreenKind kind, int x, int y, Facing facing)
    {
        var screen = new PlacedScreen(kind, x, y, facing);
        foreach (var p in TilesOf(screen.Rect))
        {
            if (!_map.InBounds(p.X, p.Y) || !_map.Rented(p) || !IsBuildable(p))
            {
                return BuildCheck.No("A screen must stand on the grass inside the hedge.");
            }

            if (!IsEmpty(p))
            {
                return BuildCheck.No("Something is already built there.");
            }
        }

        return new BuildCheck(true, BigScreens.Of(kind).Cost, null);
    }

    public BuildCheck AddScreen(ScreenKind kind, int x, int y, Facing facing)
    {
        var check = CheckScreen(kind, x, y, facing);
        if (check.Ok)
        {
            _screens.Add(new PlacedScreen(kind, x, y, facing));
            Refresh();
        }

        return check;
    }

    /// <summary>What taking down the screen on a tile would give back, or 0 if there is none.</summary>
    public int ScreenRefundAt(TilePoint p) => ScreenAt(p) is { } screen ? BigScreens.Of(screen.Kind).Cost : 0;

    private bool RemoveScreenAt(TilePoint p) => ScreenAt(p) is { } screen && _screens.Remove(screen);

    private IEnumerable<TilePoint> ScreensInTheWay => _screens.SelectMany(s => TilesOf(s.Rect));

    private void RestoreScreens(SiteSave save)
    {
        foreach (var s in save.Screens ?? new())
        {
            if (CheckScreen(s.Kind, s.X, s.Y, s.Facing).Ok)
            {
                _screens.Add(s);
            }
        }
    }
}
