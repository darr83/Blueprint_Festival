using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Site;

/// <summary>What a stretch of a camping field is marked out for. Unmarked ground is general camping.</summary>
public enum CampZone : byte
{
    /// <summary>Anyone pitches here. All unmarked camping ground is general.</summary>
    General,

    /// <summary>For people who want to sleep: no music after eleven. Grown-ups who like it quiet pitch here.</summary>
    Quiet,

    /// <summary>For parties with children, away from the rowdy end.</summary>
    Family,

    /// <summary>Bigger pitches with room to get round, for people who need them.</summary>
    Accessible,

    /// <summary>A strip mown short and kept clear of tents, so a fire engine can get through.</summary>
    FireLane,
}

/// <summary>A run of camping ground marked for one use, as it is saved.</summary>
public sealed record CampZoneRect(CampZone Zone, TileRect Rect);

/// <summary>The numbers that go with laying out a camping field.</summary>
public static class CampLayout
{
    /// <summary>No tent may be further than this from a fire lane or the edge of its field, in metres.</summary>
    public const double FireLaneReach = 25;

    /// <summary>Campers want toilets and water no further than this from their tent, in metres.</summary>
    public const double FacilitiesReach = 100;

    /// <summary>One shower for this many campers is plenty.</summary>
    public const int CampersAShower = 50;

    /// <summary>Tents in an accessible area stand at least this far apart, centre to centre, in tiles (5 m), so there is room to get round.</summary>
    public const double AccessibleSpacing = 10;

    /// <summary>What the fire officer fines a festival whose tents are out of reach of a fire lane.</summary>
    public const int FireFine = 500;

    /// <summary>The marked kinds of camping ground, in the order they are offered.</summary>
    public static readonly CampZone[] Zones = { CampZone.Quiet, CampZone.Family, CampZone.Accessible, CampZone.FireLane };

    public static string Name(CampZone zone) => zone switch
    {
        CampZone.Quiet => "Quiet camping",
        CampZone.Family => "Family camping",
        CampZone.Accessible => "Accessible camping",
        CampZone.FireLane => "Fire lane",
        _ => "General camping",
    };

    public static string About(CampZone zone) => zone switch
    {
        CampZone.Quiet => "For grown-ups who want a good night's sleep. About a third of grown-up campers would rather pitch here.",
        CampZone.Family => "For parties with children. Families pitched here are happier; families pitched among everyone else less so.",
        CampZone.Accessible => $"Bigger pitches, at least {AccessibleSpacing / 2:0} m apart, for the campers who need room to get round. Without one they have a hard time.",
        CampZone.FireLane => $"A strip mown short and kept clear. Nothing can be built on it and no tent goes up on it. The fire officer wants every tent within {FireLaneReach:0} m of one, or of the hedge.",
        _ => "Everywhere in a camping field that is not marked for something else.",
    };

    /// <summary>The stalls that only go in a camping field: the showers, the camp info point and the camp shop.</summary>
    public static bool CampOnly(StallKind kind) => kind is StallKind.ShowerBlock or StallKind.CampInfo or StallKind.CampShop or StallKind.FirePit;
}

/// <summary>
/// The camping field's layout: ground marked for quiet, family or accessible camping, and fire
/// lanes kept clear of tents. Marking is free, like VIP ground, and only goes on camping ground.
/// </summary>
public sealed partial class SiteLayout
{
    private readonly Dictionary<TilePoint, CampZone> _campZones = new();

    /// <summary>The marked camping ground, tile by tile. Unmarked camping ground is general.</summary>
    public IReadOnlyDictionary<TilePoint, CampZone> CampZones => _campZones;

    /// <summary>How a tile of camping ground is marked: general if it is not.</summary>
    public CampZone CampZoneAt(TilePoint p) => _campZones.TryGetValue(p, out var zone) ? zone : CampZone.General;

    /// <summary>True for a tile of fire lane.</summary>
    public bool IsFireLane(TilePoint p) => _campZones.TryGetValue(p, out var zone) && zone == CampZone.FireLane;

    /// <summary>True if any camping ground is marked for this use.</summary>
    public bool HasCampZone(CampZone zone) => _campZones.Values.Any(z => z == zone);

    /// <summary>Raised when the marks on the camping ground change. Nothing is blocked by them, so the site is not worked out again.</summary>
    public event Action? CampZonesChanged;

    /// <summary>Camping ground can be marked: grass in a camping field. A fire lane cannot go under something built.</summary>
    private bool CanMarkCamp(TilePoint p, CampZone zone) =>
        _map.InBounds(p.X, p.Y) && _map[p] == Ground.Grass && _map.UseAt(p) == FieldUse.Camping && (zone != CampZone.FireLane || IsEmpty(p));

    /// <summary>Whether the box between two tiles can be marked for a use (or cleared, with <see cref="CampZone.General"/>). It costs nothing.</summary>
    public BuildCheck CheckCampZone(TilePoint from, TilePoint to, CampZone zone)
    {
        if (!Box(from, to).Any(p => _map.InBounds(p.X, p.Y) && _map.UseAt(p) == FieldUse.Camping))
        {
            return BuildCheck.No("Only a camping field can be marked out for camping.");
        }

        var any = Box(from, to).Any(p => zone == CampZone.General ? _campZones.ContainsKey(p) : CanMarkCamp(p, zone) && CampZoneAt(p) != zone);
        return any ? new BuildCheck(true, 0, null)
            : BuildCheck.No(zone == CampZone.General ? "None of this ground is marked."
                : zone == CampZone.FireLane ? "A fire lane only goes on clear camping ground." : "That is marked already.");
    }

    /// <summary>Marks the box between two tiles for a use, or clears it with <see cref="CampZone.General"/>.</summary>
    public BuildCheck SetCampZone(TilePoint from, TilePoint to, CampZone zone)
    {
        var check = CheckCampZone(from, to, zone);
        if (!check.Ok)
        {
            return check;
        }

        foreach (var p in Box(from, to))
        {
            if (zone == CampZone.General)
            {
                _campZones.Remove(p);
            }
            else if (CanMarkCamp(p, zone))
            {
                _campZones[p] = zone;
            }
        }

        CampZonesChanged?.Invoke();
        return check;
    }

    private List<CampZoneRect>? SaveCampZones()
    {
        if (_campZones.Count == 0)
        {
            return null;
        }

        var rects = new List<CampZoneRect>();
        foreach (var zone in CampLayout.Zones)
        {
            var tiles = _campZones.Where(z => z.Value == zone).Select(z => z.Key).ToHashSet();
            rects.AddRange(RunsOf(tiles).Select(r => new CampZoneRect(zone, r.Vertical ? new TileRect(r.X, r.Y, 1, r.Length) : new TileRect(r.X, r.Y, r.Length, 1))));
        }

        return rects;
    }

    private void RestoreCampZones(SiteSave save)
    {
        _campZones.Clear();
        foreach (var r in save.CampZones ?? new())
        {
            if (r.Zone == CampZone.General)
            {
                continue;
            }

            foreach (var p in TilesOf(r.Rect))
            {
                if (CanMarkCamp(p, r.Zone))
                {
                    _campZones[p] = r.Zone;
                }
            }
        }
    }

    /// <summary>
    /// How far each tile of camping ground is from the nearest fire lane, or from the edge of its
    /// field, in tiles. Worked out once over the whole map: a spread outwards from every lane and edge.
    /// </summary>
    public Dictionary<TilePoint, int> FireReach()
    {
        var reach = new Dictionary<TilePoint, int>();
        var queue = new Queue<TilePoint>();
        foreach (var rect in _map.CampFields)
        {
            for (var y = rect.Y; y < rect.Bottom; y++)
            {
                for (var x = rect.X; x < rect.Right; x++)
                {
                    var p = new TilePoint(x, y);
                    if (x == rect.X || y == rect.Y || x == rect.Right - 1 || y == rect.Bottom - 1 || IsFireLane(p))
                    {
                        reach[p] = 0;
                        queue.Enqueue(p);
                    }
                }
            }
        }

        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            var d = reach[p] + 1;
            foreach (var n in new[] { new TilePoint(p.X + 1, p.Y), new TilePoint(p.X - 1, p.Y), new TilePoint(p.X, p.Y + 1), new TilePoint(p.X, p.Y - 1) })
            {
                if (!reach.ContainsKey(n) && _map.InBounds(n.X, n.Y) && _map.UseAt(n) == FieldUse.Camping)
                {
                    reach[n] = d;
                    queue.Enqueue(n);
                }
            }
        }

        return reach;
    }
}
