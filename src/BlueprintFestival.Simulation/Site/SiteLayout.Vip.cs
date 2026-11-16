using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Site;

/// <summary>
/// VIP ground: tiles the player marks for VIP guests only. Everyone else walks round it, the way
/// they walk round "no customers" ground; staff and VIP guests go anywhere on it.
/// </summary>
public sealed partial class SiteLayout
{
    private readonly HashSet<TilePoint> _vipGround = new();
    private readonly HashSet<TilePoint> _vipInside = new();
    private readonly Dictionary<TilePoint, double> _heights = new();

    /// <summary>How deep the bar across the back of the VIP lounge is, in tiles.</summary>
    public const int LoungeBarDepth = 3;

    /// <summary>How high the deck of the VIP viewing platform is, in metres.</summary>
    public const double PlatformHeight = 1.2;

    /// <summary>What one tile of a building people can walk into is.</summary>
    private enum WalkIn
    {
        /// <summary>Walls, the bar, sofas, rails: nobody walks there.</summary>
        Solid,

        /// <summary>Inside, for VIP guests only.</summary>
        Vip,

        /// <summary>Empty grass inside the building's ground, open to anyone.</summary>
        Open,
    }

    /// <summary>True for the VIP buildings people walk into, rather than queue at the front of.</summary>
    public static bool WalkedInto(StallKind kind) => kind is StallKind.VipLounge or StallKind.VipPlatform;

    /// <summary>
    /// A tile of a stall's ground, counted across its front (u, from 0) and from its back to its
    /// front (v, from 0), whichever way it faces.
    /// </summary>
    public static TilePoint Local(PlacedStall stall, int u, int v)
    {
        var r = stall.Rect;
        return stall.Facing switch
        {
            Facing.North => new TilePoint(r.X + u, r.Bottom - 1 - v),
            Facing.East => new TilePoint(r.X + v, r.Y + u),
            Facing.West => new TilePoint(r.Right - 1 - v, r.Y + u),
            _ => new TilePoint(r.X + u, r.Y + v),
        };
    }

    /// <summary>Every tile of a stall's ground with where it is across (u) and back to front (v).</summary>
    private static IEnumerable<(int U, int V, TilePoint P)> LocalTiles(PlacedStall stall)
    {
        var info = Catalogue.Of(stall.Kind);
        for (var v = 0; v < info.Depth; v++)
        {
            for (var u = 0; u < info.Width; u++)
            {
                yield return (u, v, Local(stall, u, v));
            }
        }
    }

    /// <summary>
    /// What each tile of the lounge and the platform is. The lounge (16 by 12) has the bar across
    /// the back, a wall down each side, sofas and tables down both sides, plants and a rope at the
    /// front corners and a way in in the middle. The platform (12 by 8) has steps up the middle of
    /// the back, and a deck with a rail round it.
    /// </summary>
    private static WalkIn Part(StallKind kind, int u, int v)
    {
        if (kind == StallKind.VipLounge)
        {
            var side = Math.Min(u, 15 - u);
            return v < LoungeBarDepth || side == 0
                || (side <= 2 && v is >= 4 and <= 9)
                || (side == 3 && v == 7)
                || (side <= 1 && v >= 10)
                || (side is >= 2 and <= 5 && v == 11)
                ? WalkIn.Solid : WalkIn.Vip;
        }

        // The platform: steps up the back in the middle, the deck from the third row.
        var middle = u is 5 or 6;
        if (v < 2)
        {
            return middle ? WalkIn.Vip : WalkIn.Open;
        }

        return v == 7 || u == 0 || u == 11 || (v == 2 && !middle) ? WalkIn.Solid : WalkIn.Vip;
    }

    /// <summary>The tiles a stall stands in the way on: all of it, except for the buildings people walk into.</summary>
    private static IEnumerable<TilePoint> BlockedBy(PlacedStall stall) =>
        !WalkedInto(stall.Kind) ? TilesOf(stall.Rect) : LocalTiles(stall).Where(t => Part(stall.Kind, t.U, t.V) == WalkIn.Solid).Select(t => t.P);

    /// <summary>How high people stand on a tile, in metres: up on the platform deck or its steps, otherwise on the ground.</summary>
    public double HeightAt(TilePoint p) => _heights.TryGetValue(p, out var h) ? h : 0;

    /// <summary>The tiles inside the VIP lounge and on the platform deck where VIP guests can go.</summary>
    public IReadOnlyCollection<TilePoint> VipInside => _vipInside;

    /// <summary>Tiles a VIP guest can stand on inside a VIP building: the lounge floor, or the platform deck (not its steps).</summary>
    public IEnumerable<TilePoint> InsideOf(PlacedStall stall) => !WalkedInto(stall.Kind) ? Enumerable.Empty<TilePoint>()
        : LocalTiles(stall).Where(t => Part(stall.Kind, t.U, t.V) == WalkIn.Vip && (stall.Kind == StallKind.VipLounge ? t.V >= LoungeBarDepth + 2 : t.V >= 3)).Select(t => t.P);

    /// <summary>The tiles only VIP guests (and staff) may walk on.</summary>
    public IReadOnlyCollection<TilePoint> VipGround => _vipGround;

    public bool IsVipGround(TilePoint p) => _vipGround.Contains(p);

    /// <summary>Whether the box between two tiles can be marked for VIPs (or cleared again). It costs nothing.</summary>
    public BuildCheck CheckVipGround(TilePoint from, TilePoint to, bool vip)
    {
        var any = Box(from, to).Any(p => vip ? CanMarkVip(p) && !_vipGround.Contains(p) : _vipGround.Contains(p));
        return any ? new BuildCheck(true, 0, null)
            : BuildCheck.No(vip ? "Only clear grass can be marked for VIPs." : "No ground here is marked for VIPs.");
    }

    /// <summary>Marks the box between two tiles for VIP guests only, or clears the mark. It takes over any "no customers" mark there.</summary>
    public BuildCheck SetVipGround(TilePoint from, TilePoint to, bool vip)
    {
        var check = CheckVipGround(from, to, vip);
        if (check.Ok)
        {
            foreach (var p in Box(from, to))
            {
                if (!vip)
                {
                    _vipGround.Remove(p);
                }
                else if (CanMarkVip(p))
                {
                    _vipGround.Add(p);
                    _keepOut.Remove(p);
                }
            }

            Refresh();
        }

        return check;
    }

    /// <summary>
    /// Keeps a stall for VIP guests only, or opens it to everyone again. The VIP lounge and
    /// platform are always for VIPs. Returns false if there is no such stall or it cannot change.
    /// </summary>
    public bool SetVipOnly(int stall, bool vipOnly)
    {
        if (stall < 0 || stall >= _stalls.Count || Catalogue.AlwaysVip(_stalls[stall].Kind) || _stalls[stall].Kind == StallKind.Bin)
        {
            return false;
        }

        _stalls[stall] = _stalls[stall] with { VipOnly = vipOnly };
        Refresh();
        return true;
    }

    /// <summary>Tells a bar to stop serving people who are drunk, or to serve everyone again. False if it is not a bar.</summary>
    public bool SetNoDrunks(int stall, bool noDrunks)
    {
        if (stall < 0 || stall >= _stalls.Count || !Catalogue.SellsAlcohol(_stalls[stall].Kind))
        {
            return false;
        }

        // Nothing about the ground changes, so the site is not laid out again.
        _stalls[stall] = _stalls[stall] with { NoDrunks = noDrunks };
        return true;
    }

    /// <summary>VIP ground goes on clear grass, or under seats and tables, which then become VIP seats.</summary>
    private bool CanMarkVip(TilePoint p) => _map[p] == Ground.Grass && (IsEmpty(p) || ((ComfortAt(p) is not null || SignAt(p) is not null || DecorAt(p) is not null || IsBuntingPole(p)) && !_keepOut.Contains(p)));

    /// <summary>True for a seat, table or shade standing on VIP ground: only VIP guests use it.</summary>
    public bool ForVips(PlacedComfort comfort) => TilesOf(comfort.Rect).Any(_vipGround.Contains);

    private List<TileRect>? SaveVipGround() =>
        _vipGround.Count == 0 ? null : RunsOf(_vipGround).Select(r => r.Vertical ? new TileRect(r.X, r.Y, 1, r.Length) : new TileRect(r.X, r.Y, r.Length, 1)).ToList();

    private void RestoreVipGround(SiteSave save)
    {
        _vipGround.Clear();
        foreach (var r in save.VipGround ?? new())
        {
            foreach (var p in TilesOf(r))
            {
                if (_map.InBounds(p.X, p.Y) && _map[p] == Ground.Grass && !_keepOut.Contains(p))
                {
                    _vipGround.Add(p);
                }
            }
        }
    }

    /// <summary>Drops VIP marks from ground something has since been built on, and tells the map.</summary>
    private void RefreshVipGround()
    {
        _vipGround.RemoveWhere(p => _fence.ContainsKey(p) || StageAt(p) is not null || StallAt(p) is not null || GateAt(p) is not null || _laneOf.ContainsKey(p) || _barriers.Contains(p) || ScreenAt(p) is not null || FireworksAt(p) || _areaEdges.Contains(p));
        _keepOut.ExceptWith(_vipGround);
        _vipInside.Clear();
        _heights.Clear();
        foreach (var stall in _stalls.Where(s => WalkedInto(s.Kind)))
        {
            foreach (var (u, v, p) in LocalTiles(stall))
            {
                var part = Part(stall.Kind, u, v);
                if (stall.Kind == StallKind.VipPlatform && (v >= 2 || part == WalkIn.Vip))
                {
                    // The whole deck stands high, rails and all, so nobody standing near the edge drops through it.
                    _heights[p] = v >= 2 ? PlatformHeight : PlatformHeight * (v + 1) / 3;
                }

                if (part == WalkIn.Vip)
                {
                    _vipInside.Add(p);
                }
            }
        }

        _map.SetVipOnly(_vipGround.Concat(_vipInside).Concat(VipAreaInside()));
    }
}
