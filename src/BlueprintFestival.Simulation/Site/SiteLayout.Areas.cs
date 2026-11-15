using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Site;

/// <summary>What a crowd area in front of a stage is for.</summary>
public enum AreaKind : byte
{
    /// <summary>A pen at the front of a stage, with its own limit on how many get in.</summary>
    FrontPen,

    /// <summary>Any other standing area with a limit, further back or to one side.</summary>
    Standing,

    /// <summary>A pen for VIP guests only.</summary>
    Vip,
}

/// <summary>
/// A crowd area the player has drawn: a box with crowd barrier round its edge, gaps in the
/// barrier to get in by, and a limit on how many people security let in.
/// </summary>
/// <param name="Rect">The whole box, barrier and all.</param>
/// <param name="Limit">How many people security let in, when there is someone on the gaps.</param>
/// <param name="Gaps">The middle tile of each gap in the barrier. A gap is <see cref="CrowdAreas.GapTiles"/> tiles wide.</param>
public sealed record PlacedArea(AreaKind Kind, TileRect Rect, int Limit, List<TilePoint> Gaps)
{
    /// <summary>The ground inside the barrier, in square metres.</summary>
    public double InsideMetres => Math.Max(0, Rect.Width - 2) * Math.Max(0, Rect.Height - 2) * FieldMap.TileMetres * FieldMap.TileMetres;

    /// <summary>True for a tile inside the barrier.</summary>
    public bool IsInside(TilePoint p) => p.X > Rect.X && p.X < Rect.Right - 1 && p.Y > Rect.Y && p.Y < Rect.Bottom - 1;

    /// <summary>True for a tile on the edge of the box, where the barrier stands.</summary>
    public bool IsEdge(TilePoint p) => Rect.Contains(p) && !IsInside(p);
}

/// <summary>The sizes and numbers that go with crowd areas.</summary>
public static class CrowdAreas
{
    /// <summary>People per square metre that stay comfortable: the limit the game suggests.</summary>
    public const double ComfortablePerMetre = 2.0;

    /// <summary>People per square metre past which it is a crush risk.</summary>
    public const double CrushPerMetre = 4.0;

    /// <summary>The most people per square metre a limit can be set to: nobody can squeeze in tighter.</summary>
    public const double MostPerMetre = 6.0;

    /// <summary>How wide each gap in the barrier is, in tiles (a metre and a half).</summary>
    public const int GapTiles = 3;

    /// <summary>The smallest box, in tiles across and down (3 m, barrier and all).</summary>
    public const int Smallest = 6;

    /// <summary>The biggest box, in tiles across and down (60 m).</summary>
    public const int Biggest = 120;

    /// <summary>The most gaps one area can have.</summary>
    public const int MostGaps = 6;

    /// <summary>The most areas a site can have.</summary>
    public const int MostAreas = 24;

    /// <summary>How many the game suggests letting in: two people a square metre.</summary>
    public static int Suggested(double metres) => Math.Max(5, (int)Math.Floor(metres * ComfortablePerMetre));

    /// <summary>How many it holds at the edge of a crush: four people a square metre.</summary>
    public static int CrushAt(double metres) => (int)Math.Floor(metres * CrushPerMetre);

    /// <summary>The highest limit the player can set.</summary>
    public static int Most(double metres) => Math.Max(5, (int)Math.Floor(metres * MostPerMetre));

    public static string Name(AreaKind kind) => kind switch
    {
        AreaKind.FrontPen => "Front pen",
        AreaKind.Vip => "VIP area",
        _ => "Standing area",
    };

    /// <summary>How full a number of people would make an area: comfortable, busy, or a crush risk.</summary>
    public static string Feel(int people, double metres) =>
        people > CrushAt(metres) ? "Crush risk" : people > Suggested(metres) ? "Busy" : "Comfortable";
}

/// <summary>
/// Crowd areas: pens in front of the stages with a limit on how many get in. The barrier round
/// each one goes up by itself, leaving its gaps open; a side that meets a fence already there
/// shares it.
/// </summary>
public sealed partial class SiteLayout
{
    private readonly List<PlacedArea> _areas = new();
    private readonly HashSet<TilePoint> _areaEdges = new();
    private readonly Dictionary<TilePoint, int> _areaInside = new();

    /// <summary>Raised when an area's limit changes. Nothing on the ground moves, so the site is not worked out again.</summary>
    public event Action? AreasChanged;

    public IReadOnlyList<PlacedArea> Areas => _areas;

    /// <summary>The tiles where the barrier round the crowd areas stands.</summary>
    public IReadOnlyCollection<TilePoint> AreaBarriers => _areaEdges;

    public bool IsAreaBarrier(TilePoint p) => _areaEdges.Contains(p);

    /// <summary>The area a tile is inside (not on its barrier), or -1.</summary>
    public int AreaInside(TilePoint p) => _areaInside.TryGetValue(p, out var area) ? area : -1;

    /// <summary>The area whose box a tile is in, barrier and gaps included, or -1.</summary>
    public int AreaAt(TilePoint p)
    {
        if (_areaInside.TryGetValue(p, out var inside))
        {
            return inside;
        }

        for (var i = 0; i < _areas.Count; i++)
        {
            if (_areas[i].Rect.Contains(p))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The box between two tiles, corner to corner.</summary>
    public static TileRect BoxOf(TilePoint from, TilePoint to) =>
        new(Math.Min(from.X, to.X), Math.Min(from.Y, to.Y), Math.Abs(from.X - to.X) + 1, Math.Abs(from.Y - to.Y) + 1);

    /// <summary>The tiles of one gap, along the edge it is in.</summary>
    public static IEnumerable<TilePoint> GapTilesOf(PlacedArea area, TilePoint middle)
    {
        var r = area.Rect;
        var alongX = middle.Y == r.Y || middle.Y == r.Bottom - 1;
        for (var k = -(CrowdAreas.GapTiles / 2); k <= CrowdAreas.GapTiles / 2; k++)
        {
            yield return alongX ? new TilePoint(middle.X + k, middle.Y) : new TilePoint(middle.X, middle.Y + k);
        }
    }

    /// <summary>The way out of an area through a gap: the step from the gap to the ground outside it.</summary>
    public static (int DirX, int DirY) Outward(PlacedArea area, TilePoint gap)
    {
        var r = area.Rect;
        return gap.Y == r.Y ? (0, -1) : gap.Y == r.Bottom - 1 ? (0, 1) : gap.X == r.X ? (-1, 0) : (1, 0);
    }

    /// <summary>The stage an area belongs to: the one whose front is nearest its middle, within 60 m. -1 if none is near.</summary>
    public int StageOfArea(PlacedArea area)
    {
        var (cx, cy) = ((area.Rect.X + (area.Rect.Width / 2.0)) * FieldMap.TileMetres, (area.Rect.Y + (area.Rect.Height / 2.0)) * FieldMap.TileMetres);
        var best = -1;
        var bestMetres = 60.0;
        for (var i = 0; i < _stages.Count; i++)
        {
            var (fx, fy, _, _) = Front(_stages[i].Rect, _stages[i].Facing);
            var metres = Math.Sqrt(((fx - cx) * (fx - cx)) + ((fy - cy) * (fy - cy)));
            if (metres < bestMetres)
            {
                (best, bestMetres) = (i, metres);
            }
        }

        return best;
    }

    /// <summary>Whether an area can be drawn over the box between two tiles, and what its barrier costs.</summary>
    public BuildCheck CheckArea(AreaKind kind, TilePoint from, TilePoint to)
    {
        var rect = BoxOf(from, to);
        if (rect.Width < CrowdAreas.Smallest || rect.Height < CrowdAreas.Smallest)
        {
            return BuildCheck.No("An area needs to be at least 3 m by 3 m.");
        }

        if (rect.Width > CrowdAreas.Biggest || rect.Height > CrowdAreas.Biggest)
        {
            return BuildCheck.No("An area can be at most 60 m by 60 m.");
        }

        if (_areas.Count >= CrowdAreas.MostAreas)
        {
            return BuildCheck.No($"There can be at most {CrowdAreas.MostAreas} areas.");
        }

        var probe = new PlacedArea(kind, rect, 0, new());
        var open = new HashSet<TilePoint>(FirstGaps(probe).SelectMany(g => GapTilesOf(probe, g)));
        var barrier = 0;
        foreach (var p in TilesOf(rect))
        {
            if (!_map.InBounds(p.X, p.Y) || _map[p] != Ground.Grass || !_map.InFestival(p))
            {
                return BuildCheck.No("An area must be on the grass of the festival field.");
            }

            if (AreaAt(p) >= 0)
            {
                return BuildCheck.No("Areas cannot overlap.");
            }

            if (StageAt(p) is not null || StallAt(p) is not null || GateAt(p) is not null || _laneOf.ContainsKey(p) || _barriers.Contains(p) || FireworksAt(p) || _pitches.Contains(p))
            {
                return BuildCheck.No("Something is in the way.");
            }

            if (_fence.ContainsKey(p))
            {
                if (!probe.IsEdge(p))
                {
                    return BuildCheck.No("A fence runs through it.");
                }
            }
            else if (probe.IsEdge(p))
            {
                if (!IsEmpty(p) && !_keepOut.Contains(p))
                {
                    return BuildCheck.No("Something is in the way of the barrier.");
                }

                barrier += open.Contains(p) ? 0 : 1;
            }
        }

        return new BuildCheck(true, FenceCost(FenceKind.CrowdBarrier, barrier), null);
    }

    /// <summary>
    /// Draws a crowd area over the box between two tiles. It lets in two people a square metre to
    /// start with, and has its gaps on the side away from the nearest stage.
    /// </summary>
    public BuildCheck AddArea(AreaKind kind, TilePoint from, TilePoint to)
    {
        var check = CheckArea(kind, from, to);
        if (!check.Ok)
        {
            return check;
        }

        var rect = BoxOf(from, to);
        var area = new PlacedArea(kind, rect, 0, new());
        area = area with { Limit = CrowdAreas.Suggested(area.InsideMetres), Gaps = FirstGaps(area) };
        _areas.Add(area);
        Refresh();
        return check;
    }

    /// <summary>Where the gaps go to start with: on the side facing away from the nearest stage, near both ends of a long side.</summary>
    private List<TilePoint> FirstGaps(PlacedArea area)
    {
        var r = area.Rect;
        var (dx, dy) = (0, 1);
        if (StageOfArea(area) is >= 0 and var stage)
        {
            (_, _, dx, dy) = Front(_stages[stage].Rect, _stages[stage].Facing);
        }

        // The back edge is the one furthest from the stage, along the way the stage faces.
        var (fixedX, fixedY) = (dx > 0 ? r.Right - 1 : r.X, dy > 0 ? r.Bottom - 1 : r.Y);
        var length = dx == 0 ? r.Width : r.Height;
        var at = length >= 16 ? new[] { 1 + (length / 6) + 1, length - 3 - (length / 6) } : new[] { length / 2 };
        return at.Select(k => Math.Clamp(k, 2, length - 3))
            .Distinct()
            .Select(k => dx == 0 ? new TilePoint(r.X + k, fixedY) : new TilePoint(fixedX, r.Y + k))
            .ToList();
    }

    /// <summary>Changes how many people security let into an area. Returns the limit kept, or null if there is no such area.</summary>
    public int? SetAreaLimit(int area, int limit)
    {
        if (area < 0 || area >= _areas.Count)
        {
            return null;
        }

        var a = _areas[area];
        var kept = Math.Clamp(limit, 5, CrowdAreas.Most(a.InsideMetres));
        _areas[area] = a with { Limit = kept };
        AreasChanged?.Invoke();
        return kept;
    }

    /// <summary>
    /// Opens a gap in an area's barrier at a tile on its edge, or closes the gap that is there.
    /// An area always keeps at least one gap. Returns a problem, or null if it worked.
    /// </summary>
    public string? ToggleGap(TilePoint p)
    {
        var index = AreaAt(p);
        if (index < 0 || !_areas[index].IsEdge(p))
        {
            return "Click on the barrier round an area.";
        }

        var area = _areas[index];
        var gaps = area.Gaps.ToList();
        var hit = gaps.FindIndex(g => GapTilesOf(area, g).Contains(p));
        if (hit >= 0)
        {
            if (gaps.Count == 1)
            {
                return "An area needs at least one way in.";
            }

            gaps.RemoveAt(hit);
        }
        else
        {
            var r = area.Rect;
            var onTop = p.Y == r.Y || p.Y == r.Bottom - 1;
            var onSide = p.X == r.X || p.X == r.Right - 1;
            if (onTop && onSide)
            {
                return "A gap cannot go on a corner.";
            }

            if (gaps.Count >= CrowdAreas.MostGaps)
            {
                return $"An area can have at most {CrowdAreas.MostGaps} gaps.";
            }

            // Keep the whole gap clear of the corners.
            var middle = onTop ? new TilePoint(Math.Clamp(p.X, r.X + 2, r.Right - 3), p.Y) : new TilePoint(p.X, Math.Clamp(p.Y, r.Y + 2, r.Bottom - 3));
            if (GapTilesOf(area, middle).Any(t => gaps.Any(g => GapTilesOf(area, g).Contains(t))))
            {
                return "There is a gap there already.";
            }

            gaps.Add(middle);
        }

        _areas[index] = area with { Gaps = gaps };
        Refresh();
        return null;
    }

    /// <summary>What taking down an area would give back: its barrier.</summary>
    private int AreaRefundAt(TilePoint p) => AreaAt(p) is >= 0 and var area ? FenceCost(FenceKind.CrowdBarrier, _areaEdges.Count(e => _areas[area].Rect.Contains(e))) : 0;

    private bool RemoveAreaAt(TilePoint p)
    {
        if (AreaAt(p) is not (>= 0 and var area))
        {
            return false;
        }

        _areas.RemoveAt(area);
        return true;
    }

    /// <summary>Works out where the barrier round every area stands and which tiles are inside each.</summary>
    private void LayAreas()
    {
        _areaEdges.Clear();
        _areaInside.Clear();
        for (var i = 0; i < _areas.Count; i++)
        {
            var area = _areas[i];
            var open = new HashSet<TilePoint>(area.Gaps.SelectMany(g => GapTilesOf(area, g)));
            foreach (var p in TilesOf(area.Rect))
            {
                if (area.IsInside(p))
                {
                    _areaInside[p] = i;
                }
                else if (!open.Contains(p) && _map.InBounds(p.X, p.Y) && IsEmpty(p))
                {
                    _areaEdges.Add(p);
                }
            }
        }
    }

    /// <summary>The tiles inside the VIP areas: only VIP guests (and staff) go in.</summary>
    private IEnumerable<TilePoint> VipAreaInside() => _areaInside.Where(kv => _areas[kv.Value].Kind == AreaKind.Vip).Select(kv => kv.Key);

    private void RestoreAreas(SiteSave save)
    {
        _areas.Clear();
        foreach (var area in save.Areas ?? new())
        {
            var gaps = area.Gaps ?? new();
            if (area.Rect.Width >= CrowdAreas.Smallest && area.Rect.Height >= CrowdAreas.Smallest
                && TilesOf(area.Rect).All(p => _map.InBounds(p.X, p.Y) && AreaAt(p) < 0 && StageAt(p) is null && StallAt(p) is null && GateAt(p) is null))
            {
                _areas.Add(area with { Gaps = gaps.Count > 0 ? gaps : FirstGaps(area) });
            }
        }
    }
}
