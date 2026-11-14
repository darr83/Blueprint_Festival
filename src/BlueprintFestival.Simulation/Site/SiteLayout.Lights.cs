using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Site;

/// <summary>The kinds of light the player can put up for after dark.</summary>
public enum LightKind
{
    /// <summary>A short post with a lamp on top, for beside a path.</summary>
    PathLight,

    /// <summary>A tall tower with a bank of lamps: lights a whole corner of a field.</summary>
    Floodlight,

    /// <summary>A string of coloured bulbs between poles. Dragged out; the poles go in by themselves.</summary>
    FairyLights,
}

/// <summary>
/// What there is to know about a kind of light. The cost is for each one, or for each metre of
/// fairy lights. The reach is how far its light carries, in metres.
/// </summary>
public sealed record LightInfo(LightKind Kind, string Name, int Cost, double Reach, string About);

/// <summary>A path light or a floodlight that has been put up, on one tile.</summary>
public sealed record PlacedLight(LightKind Kind, int X, int Y)
{
    public TilePoint Tile => new(X, Y);
}

/// <summary>A string of fairy lights between two tiles. Its poles stand at both ends and every few metres between.</summary>
public sealed record FairyString(TilePoint From, TilePoint To)
{
    /// <summary>How long the string is, in metres.</summary>
    public double Metres => Math.Sqrt(((To.X - From.X) * (To.X - From.X)) + ((To.Y - From.Y) * (To.Y - From.Y))) * FieldMap.TileMetres;

    /// <summary>The tiles its poles stand on, in order from one end to the other.</summary>
    public List<TilePoint> Poles()
    {
        var gaps = Math.Max(1, (int)Math.Ceiling(Metres / Lighting.PoleGapMetres));
        var poles = new List<TilePoint>();
        for (var i = 0; i <= gaps; i++)
        {
            var p = new TilePoint(
                From.X + (int)Math.Round((To.X - From.X) * (double)i / gaps),
                From.Y + (int)Math.Round((To.Y - From.Y) * (double)i / gaps));
            if (poles.Count == 0 || poles[^1] != p)
            {
                poles.Add(p);
            }
        }

        return poles;
    }
}

/// <summary>The lights the player can put up, and how light is worked out.</summary>
public static class Lighting
{
    public static readonly IReadOnlyList<LightInfo> All = new[]
    {
        new LightInfo(LightKind.PathLight, "Path light", 8, 5, "A lamp on a short post. Lights the ground for 5 m around."),
        new LightInfo(LightKind.Floodlight, "Floodlight", 90, 18, "A tower with a bank of lamps. Lights the ground for 18 m around."),
        new LightInfo(LightKind.FairyLights, "Fairy lights", 3, 4, "Coloured bulbs strung between poles. Lights the ground for 4 m either side."),
    };

    public static LightInfo Of(LightKind kind) => All[(int)kind];

    /// <summary>The furthest apart two poles of a string of fairy lights stand, in metres.</summary>
    public const double PoleGapMetres = 5;

    /// <summary>The shortest string of fairy lights, in metres.</summary>
    public const double ShortestStringMetres = 2;

    /// <summary>How far the lamps of a stall light the ground round it, in metres.</summary>
    public const double StallGlowMetres = 3;

    /// <summary>How far a stage with its own lighting rig lights the ground round it, in metres.</summary>
    public const double StageGlowMetres = 12;

    /// <summary>What a string of fairy lights costs: by the metre, part pounds rounded up.</summary>
    public static int FairyCost(double metres) => (int)Math.Ceiling((metres * Of(LightKind.FairyLights).Cost) - 1e-9);

    /// <summary>How brightly a light shows at a distance: full out to half its reach, then fading to nothing at its edge.</summary>
    public static double Glow(double metres, double reach) =>
        reach <= 0 || metres >= reach ? 0 : metres <= reach / 2 ? 1 : (reach - metres) / (reach / 2);
}

public sealed partial class SiteLayout
{
    private readonly List<PlacedLight> _lights = new();
    private readonly List<FairyString> _fairy = new();
    private readonly Dictionary<TilePoint, int> _poles = new();
    private float[] _lit = Array.Empty<float>();
    private bool _litStale = true;

    /// <summary>The path lights and floodlights that are up.</summary>
    public IReadOnlyList<PlacedLight> Lights => _lights;

    /// <summary>The strings of fairy lights that are up.</summary>
    public IReadOnlyList<FairyString> FairyLights => _fairy;

    public PlacedLight? LightAt(TilePoint p) => _lights.FirstOrDefault(l => l.Tile == p);

    /// <summary>True if a pole of some fairy lights stands on a tile.</summary>
    public bool IsFairyPole(TilePoint p) => Poles.ContainsKey(p);

    /// <summary>True if a light or a pole of fairy lights stands on a tile.</summary>
    public bool HasLight(TilePoint p) => LightAt(p) is not null || IsFairyPole(p);

    /// <summary>Every tile with a fairy light pole on it, and how many strings are tied to it.</summary>
    private Dictionary<TilePoint, int> Poles
    {
        get
        {
            if (_poles.Count == 0 && _fairy.Count > 0)
            {
                foreach (var p in _fairy.SelectMany(f => f.Poles()))
                {
                    _poles[p] = _poles.GetValueOrDefault(p) + 1;
                }
            }

            return _poles;
        }
    }

    /// <summary>The lights people have to walk round: a floodlight tower. Posts and poles are thin enough to step past.</summary>
    private IEnumerable<TilePoint> LightsInTheWay => _lights.Where(l => l.Kind == LightKind.Floodlight).Select(l => l.Tile);

    /// <summary>Whether a path light or floodlight can go on a tile, and what it costs.</summary>
    public BuildCheck CheckLight(LightKind kind, TilePoint at)
    {
        if (kind == LightKind.FairyLights)
        {
            return BuildCheck.No("Drag fairy lights out from one place to another.");
        }

        if (!_map.InBounds(at.X, at.Y) || !_map.Rented(at) || !IsBuildable(at))
        {
            return BuildCheck.No("A light must stand on the grass inside the hedge.");
        }

        return IsEmpty(at) ? new BuildCheck(true, Lighting.Of(kind).Cost, null) : BuildCheck.No("Something is already built there.");
    }

    public BuildCheck AddLight(LightKind kind, TilePoint at)
    {
        var check = CheckLight(kind, at);
        if (check.Ok)
        {
            _lights.Add(new PlacedLight(kind, at.X, at.Y));
            Refresh();
        }

        return check;
    }

    /// <summary>
    /// Whether fairy lights can be strung between two tiles, and what they cost. The string can
    /// cross over anything; only its poles need clear grass. A pole that is already there is shared.
    /// </summary>
    public BuildCheck CheckFairyLights(TilePoint from, TilePoint to)
    {
        var fairy = new FairyString(from, to);
        if (fairy.Metres < Lighting.ShortestStringMetres)
        {
            return BuildCheck.No("Drag further: fairy lights need at least 2 m.");
        }

        if (_fairy.Any(f => (f.From == from && f.To == to) || (f.From == to && f.To == from)))
        {
            return BuildCheck.No("There are fairy lights here already.");
        }

        foreach (var p in fairy.Poles())
        {
            if (!_map.InBounds(p.X, p.Y) || !_map.Rented(p) || !IsBuildable(p))
            {
                return BuildCheck.No("The poles must stand on the grass inside the hedge.");
            }

            if (!IsEmpty(p) && !IsFairyPole(p))
            {
                return BuildCheck.No("A pole would stand where something is already built.");
            }
        }

        return new BuildCheck(true, Lighting.FairyCost(fairy.Metres), null);
    }

    /// <summary>Strings fairy lights between two tiles. The poles go in by themselves.</summary>
    public BuildCheck AddFairyLights(TilePoint from, TilePoint to)
    {
        var check = CheckFairyLights(from, to);
        if (check.Ok)
        {
            _fairy.Add(new FairyString(from, to));
            Refresh();
        }

        return check;
    }

    /// <summary>The pole a drag starting on a tile would tie on to: one on the tile or right beside it. Otherwise the tile itself.</summary>
    public TilePoint SnapToPole(TilePoint p)
    {
        if (IsFairyPole(p))
        {
            return p;
        }

        foreach (var q in Poles.Keys)
        {
            if (Near(p, q))
            {
                return q;
            }
        }

        return p;
    }

    /// <summary>The strings of fairy lights with a pole on a tile, or right beside it if nothing else is there.</summary>
    private List<FairyString> StringsAt(TilePoint p)
    {
        var exact = _fairy.Where(f => f.Poles().Contains(p)).ToList();
        if (exact.Count > 0 || !IsEmpty(p))
        {
            return exact;
        }

        var near = SnapToPole(p);
        return near == p ? exact : _fairy.Where(f => f.Poles().Contains(near)).ToList();
    }

    /// <summary>What taking down the light on a tile, or the fairy lights tied to a pole there, would give back.</summary>
    public int LightRefundAt(TilePoint p) =>
        LightAt(p) is { } light ? Lighting.Of(light.Kind).Cost : StringsAt(p).Sum(f => Lighting.FairyCost(f.Metres));

    private bool RemoveLightsAt(TilePoint p)
    {
        if (LightAt(p) is { } light)
        {
            _lights.Remove(light);
            return true;
        }

        var strings = StringsAt(p);
        foreach (var f in strings)
        {
            _fairy.Remove(f);
        }

        return strings.Count > 0;
    }

    private void RestoreLights(SiteSave save)
    {
        foreach (var l in save.Lights ?? new())
        {
            if (CheckLight(l.Kind, l.Tile).Ok)
            {
                _lights.Add(l);
            }
        }

        foreach (var f in save.FairyLights ?? new())
        {
            _poles.Clear();
            if (CheckFairyLights(f.From, f.To).Ok)
            {
                _fairy.Add(f);
            }
        }

        _poles.Clear();
    }

    /// <summary>
    /// How well lit a tile is after dark, from 0 (nothing) to 1 (fully lit): by the lights the
    /// player has put up, by the lamps of the stalls and by a stage with its own lighting rig.
    /// </summary>
    public double LitAt(TilePoint p)
    {
        if (!_map.InBounds(p.X, p.Y))
        {
            return 0;
        }

        if (_litStale)
        {
            WorkOutLight();
        }

        return _lit[(p.Y * _map.Width) + p.X];
    }

    /// <summary>How well lit a place is, in metres.</summary>
    public double LitAtPlace(double x, double y) => LitAt(FieldMap.TileAt(x, y));

    private void WorkOutLight()
    {
        _litStale = false;
        if (_lit.Length != _map.Width * _map.Height)
        {
            _lit = new float[_map.Width * _map.Height];
        }
        else
        {
            Array.Clear(_lit);
        }

        foreach (var l in _lights)
        {
            Shine(l.X + 0.5, l.Y + 0.5, l.X + 0.5, l.Y + 0.5, Lighting.Of(l.Kind).Reach);
        }

        foreach (var f in _fairy)
        {
            Shine(f.From.X + 0.5, f.From.Y + 0.5, f.To.X + 0.5, f.To.Y + 0.5, Lighting.Of(LightKind.FairyLights).Reach);
        }

        foreach (var s in _stalls)
        {
            if (s.Kind != StallKind.WaterPoint)
            {
                Shine(s.Rect, Lighting.StallGlowMetres);
            }
        }

        foreach (var s in _stages)
        {
            if (s.Kind is StageKind.HiredStage or StageKind.MainStage)
            {
                Shine(s.Rect, Lighting.StageGlowMetres);
            }
        }
    }

    /// <summary>Lights the ground round a line between two points (in tiles), out to a reach in metres.</summary>
    private void Shine(double x0, double y0, double x1, double y1, double reach)
    {
        var r = reach / FieldMap.TileMetres;
        var (dx, dy) = (x1 - x0, y1 - y0);
        var length = (dx * dx) + (dy * dy);
        var fromX = Math.Max(0, (int)Math.Floor(Math.Min(x0, x1) - r));
        var toX = Math.Min(_map.Width - 1, (int)Math.Ceiling(Math.Max(x0, x1) + r));
        var fromY = Math.Max(0, (int)Math.Floor(Math.Min(y0, y1) - r));
        var toY = Math.Min(_map.Height - 1, (int)Math.Ceiling(Math.Max(y0, y1) + r));
        for (var y = fromY; y <= toY; y++)
        {
            for (var x = fromX; x <= toX; x++)
            {
                var (px, py) = (x + 0.5 - x0, y + 0.5 - y0);
                var along = length <= 0 ? 0 : Math.Clamp(((px * dx) + (py * dy)) / length, 0, 1);
                var (ox, oy) = (px - (along * dx), py - (along * dy));
                var glow = (float)Lighting.Glow(Math.Sqrt((ox * ox) + (oy * oy)) * FieldMap.TileMetres, reach);
                var i = (y * _map.Width) + x;
                if (glow > _lit[i])
                {
                    _lit[i] = glow;
                }
            }
        }
    }

    /// <summary>Lights the ground round a rectangle of tiles, out to a reach in metres from its edge.</summary>
    private void Shine(TileRect rect, double reach)
    {
        var r = (int)Math.Ceiling(reach / FieldMap.TileMetres);
        for (var y = Math.Max(0, rect.Y - r); y < Math.Min(_map.Height, rect.Bottom + r); y++)
        {
            for (var x = Math.Max(0, rect.X - r); x < Math.Min(_map.Width, rect.Right + r); x++)
            {
                var ox = Math.Max(0, Math.Max(rect.X - x, x - (rect.Right - 1)));
                var oy = Math.Max(0, Math.Max(rect.Y - y, y - (rect.Bottom - 1)));
                var glow = (float)Lighting.Glow(Math.Sqrt((ox * ox) + (oy * oy)) * FieldMap.TileMetres, reach);
                var i = (y * _map.Width) + x;
                if (glow > _lit[i])
                {
                    _lit[i] = glow;
                }
            }
        }
    }
}
