using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Site;

/// <summary>The two sizes of launch pad for fireworks.</summary>
public enum LaunchPadKind
{
    Small,
    Big,
}

/// <summary>
/// A size of launch pad: its hire for the day, how many tiles square it is, and how many tiles of
/// safe zone go round it on every side.
/// </summary>
public sealed record LaunchPadInfo(LaunchPadKind Kind, string Name, int Cost, int Size, int SafeTiles, string About)
{
    public bool Big => Kind == LaunchPadKind.Big;
}

/// <summary>The launch pad, where it stands, and whether its safe zone is fenced off.</summary>
public sealed record PlacedLaunchPad(LaunchPadKind Kind, int X, int Y, bool Fenced)
{
    public TileRect Rect => new(X, Y, LaunchPads.Of(Kind).Size, LaunchPads.Of(Kind).Size);

    /// <summary>The safe zone: the pad and the clear ground all round it.</summary>
    public TileRect Zone
    {
        get
        {
            var info = LaunchPads.Of(Kind);
            return new TileRect(X - info.SafeTiles, Y - info.SafeTiles, info.Size + (2 * info.SafeTiles), info.Size + (2 * info.SafeTiles));
        }
    }

    /// <summary>What fencing the safe zone costs: Heras fence all the way round it.</summary>
    public int FenceCost => LaunchPads.FenceCostFor(Kind);
}

/// <summary>
/// The launch pads. Fireworks go off from a pad, and nobody may be inside the safe zone round it
/// while they do: fence it off, or people wander in.
/// </summary>
public static class LaunchPads
{
    public static readonly IReadOnlyList<LaunchPadInfo> All = new[]
    {
        new LaunchPadInfo(LaunchPadKind.Small, "Small launch pad", 150, 6, 12, "A 3 m pad with a 6 m safe zone round it. Lets off 3 fireworks at once."),
        new LaunchPadInfo(LaunchPadKind.Big, "Big launch pad", 300, 10, 20, "A 5 m pad with a 10 m safe zone round it. Lets off 5 at once, and takes the big shells and the finale."),
    };

    public static LaunchPadInfo Of(LaunchPadKind kind) => All[(int)kind];

    /// <summary>What a metre of Heras fence round a safe zone costs.</summary>
    public const int FencePerMetre = 2;

    /// <summary>What fencing the whole of a pad's safe zone costs.</summary>
    public static int FenceCostFor(LaunchPadKind kind)
    {
        var side = Of(kind).Size + (2 * Of(kind).SafeTiles);
        return side * 4 / 2 * FencePerMetre;
    }

    /// <summary>The tiles of a zone's edge, where its fence stands.</summary>
    public static IEnumerable<TilePoint> Edge(TileRect zone)
    {
        for (var x = zone.X; x < zone.Right; x++)
        {
            yield return new TilePoint(x, zone.Y);
            yield return new TilePoint(x, zone.Bottom - 1);
        }

        for (var y = zone.Y + 1; y < zone.Bottom - 1; y++)
        {
            yield return new TilePoint(zone.X, y);
            yield return new TilePoint(zone.Right - 1, y);
        }
    }
}

public sealed partial class SiteLayout
{
    private PlacedLaunchPad? _pad;
    private HashSet<TilePoint> _padFence = new();

    /// <summary>The launch pad, if one is down.</summary>
    public PlacedLaunchPad? LaunchPad => _pad;

    /// <summary>True on the pad itself, or on the fence round its safe zone.</summary>
    public bool FireworksAt(TilePoint p) => _pad is { } pad && (pad.Rect.Contains(p) || _padFence.Contains(p));

    /// <summary>True if a place, in metres, is inside the safe zone round the launch pad.</summary>
    public bool InSafeZone(double x, double y) => _pad is { } pad && pad.Zone.Contains(FieldMap.TileAt(x, y));

    /// <summary>Whether a launch pad can go with its corner on a tile: its whole safe zone must be clear grass.</summary>
    public BuildCheck CheckLaunchPad(LaunchPadKind kind, int x, int y)
    {
        if (_pad is not null)
        {
            return BuildCheck.No("There is already a launch pad. Take it down first to move it.");
        }

        var pad = new PlacedLaunchPad(kind, x, y, false);
        foreach (var p in TilesOf(pad.Zone))
        {
            if (!_map.InBounds(p.X, p.Y) || !_map.Rented(p) || !IsBuildable(p))
            {
                return BuildCheck.No("The pad and its safe zone must be on the grass inside the hedge.");
            }
        }

        if (WhatIsIn(pad.Zone) is { } inTheWay)
        {
            return BuildCheck.No($"The safe zone must be clear: {inTheWay} is in the way.");
        }

        return new BuildCheck(true, LaunchPads.Of(kind).Cost, null);
    }

    public BuildCheck AddLaunchPad(LaunchPadKind kind, int x, int y)
    {
        var check = CheckLaunchPad(kind, x, y);
        if (check.Ok)
        {
            _pad = new PlacedLaunchPad(kind, x, y, false);
            Refresh();
        }

        return check;
    }

    /// <summary>Whether the fence can go round the safe zone: nothing may stand on its line.</summary>
    public BuildCheck CheckPadFence()
    {
        if (_pad is not { } pad)
        {
            return BuildCheck.No("Put down a launch pad first.");
        }

        if (pad.Fenced)
        {
            return BuildCheck.No("The safe zone is already fenced.");
        }

        foreach (var p in LaunchPads.Edge(pad.Zone))
        {
            if (!IsEmpty(p))
            {
                return BuildCheck.No("Something stands on the line of the fence.");
            }
        }

        return new BuildCheck(true, pad.FenceCost, null);
    }

    /// <summary>Fences the safe zone off, or takes the fence down again.</summary>
    public bool SetPadFence(bool fenced)
    {
        if (_pad is not { } pad || pad.Fenced == fenced || (fenced && !CheckPadFence().Ok))
        {
            return false;
        }

        _pad = pad with { Fenced = fenced };
        Refresh();
        return true;
    }

    /// <summary>What stands inside a zone, in words, or null if it is clear.</summary>
    public string? WhatIsIn(TileRect zone)
    {
        foreach (var p in TilesOf(zone))
        {
            if (_pad is { } pad && (pad.Rect.Contains(p) || _padFence.Contains(p)))
            {
                continue;
            }

            var what = StageAt(p) is not null ? "a stage"
                : StallAt(p) is { } stall ? $"the {Catalogue.Of(stall.Kind).Name.ToLowerInvariant()}"
                : GateAt(p) is not null ? "a gate"
                : ScreenAt(p) is not null ? "a big screen"
                : _laneOf.ContainsKey(p) || _barriers.Contains(p) ? "a queue lane"
                : _fence.ContainsKey(p) ? "a fence"
                : HasLight(p) ? "a light"
                : null;
            if (what is not null)
            {
                return what;
            }
        }

        return null;
    }

    /// <summary>What taking down the pad would give back, with its fence, if a tile is on it or its fence; else 0.</summary>
    public int PadRefundAt(TilePoint p) => FireworksAt(p) && _pad is { } pad ? LaunchPads.Of(pad.Kind).Cost + (pad.Fenced ? pad.FenceCost : 0) : 0;

    private bool RemovePadAt(TilePoint p)
    {
        if (!FireworksAt(p))
        {
            return false;
        }

        _pad = null;
        return true;
    }

    private IEnumerable<TilePoint> PadInTheWay
    {
        get
        {
            _padFence = _pad is { Fenced: true } pad ? LaunchPads.Edge(pad.Zone).ToHashSet() : new HashSet<TilePoint>();
            return _pad is { } placed ? TilesOf(placed.Rect).Concat(_padFence) : Array.Empty<TilePoint>();
        }
    }

    private void RestorePad(SiteSave save)
    {
        if (save.LaunchPad is { } pad && CheckLaunchPad(pad.Kind, pad.X, pad.Y).Ok)
        {
            _pad = pad with { Fenced = false };
            _padFence.Clear();
            if (pad.Fenced && CheckPadFence().Ok)
            {
                _pad = pad;
            }
        }
    }
}
