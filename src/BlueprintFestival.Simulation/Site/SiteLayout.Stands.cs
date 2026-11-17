using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Site;

/// <summary>What a stand is: seats on tiers, standing steps, a raised deck, or a low deck with a ramp.</summary>
public enum StandKind : byte
{
    /// <summary>Rows of seats, each row higher than the one in front.</summary>
    Seated,

    /// <summary>Rows of standing steps, each higher than the one in front.</summary>
    Terrace,

    /// <summary>A flat deck high up on scaffold, with a rail round it, to stand on.</summary>
    Deck,

    /// <summary>A low flat deck with a ramp up to it, for people who cannot stand in a crowd.</summary>
    Accessible,
}

/// <summary>How much of a stand has a roof over it.</summary>
public enum StandRoof : byte
{
    None,
    BackHalf,
    Full,
}

/// <summary>Who may go up on a stand.</summary>
public enum StandUse : byte
{
    /// <summary>Anyone, first come first served.</summary>
    Anyone,

    /// <summary>Anyone who pays for a seat ticket at the steps.</summary>
    SeatTicket,

    /// <summary>VIP guests only.</summary>
    Vip,
}

/// <summary>
/// A stand the player has dragged out: its whole box, rail and all, and the way the people on it
/// face. Rows run along its length; the first row is at the front, and steps are gaps in the rail.
/// </summary>
/// <param name="Rect">The whole box, rail and all.</param>
/// <param name="Facing">The way the people on it look: towards a stage.</param>
/// <param name="Steps">The middle tile of each set of steps in the rail. Steps are <see cref="Stands.StepTiles"/> tiles wide.</param>
public sealed record PlacedStand(StandKind Kind, TileRect Rect, Facing Facing, StandRoof Roof, List<TilePoint> Steps)
{
    /// <summary>Who may go up on it.</summary>
    public StandUse Use { get; init; }

    /// <summary>What a seat ticket costs, in pounds, when it is kept for seat tickets.</summary>
    public int Price { get; init; } = Stands.StartPrice;

    /// <summary>How long it is inside the rail, in tiles.</summary>
    public int Length => Facing is Facing.North or Facing.South ? Rect.Width - 2 : Rect.Height - 2;

    /// <summary>How deep it is inside the rail, in tiles.</summary>
    public int Depth => Facing is Facing.North or Facing.South ? Rect.Height - 2 : Rect.Width - 2;

    /// <summary>How many rows (for a deck, metres deep) it has.</summary>
    public int Rows => Depth / Stands.RowTiles;

    /// <summary>True for a tile inside the rail.</summary>
    public bool IsInside(TilePoint p) => p.X > Rect.X && p.X < Rect.Right - 1 && p.Y > Rect.Y && p.Y < Rect.Bottom - 1;

    /// <summary>True for a tile on the edge of the box, where the rail stands.</summary>
    public bool IsEdge(TilePoint p) => Rect.Contains(p) && !IsInside(p);

    /// <summary>
    /// A tile of the stand from where it is along (u, from 0 at one end inside the rail) and back
    /// from the front (v, from 0 at the first row). -1 and Length or Depth are on the rail.
    /// </summary>
    public TilePoint At(int u, int v) => Facing switch
    {
        Facing.North => new TilePoint(Rect.X + 1 + u, Rect.Y + 1 + v),
        Facing.South => new TilePoint(Rect.X + 1 + u, Rect.Bottom - 2 - v),
        Facing.West => new TilePoint(Rect.X + 1 + v, Rect.Y + 1 + u),
        _ => new TilePoint(Rect.Right - 2 - v, Rect.Y + 1 + u),
    };

    /// <summary>Where a tile is along the stand (u) and back from its front (v): the other way round from <see cref="At"/>.</summary>
    public (int U, int V) LocalOf(TilePoint p) => Facing switch
    {
        Facing.North => (p.X - Rect.X - 1, p.Y - Rect.Y - 1),
        Facing.South => (p.X - Rect.X - 1, Rect.Bottom - 2 - p.Y),
        Facing.West => (p.Y - Rect.Y - 1, p.X - Rect.X - 1),
        _ => (p.Y - Rect.Y - 1, Rect.Right - 2 - p.X),
    };

    /// <summary>The step, in tiles, that goes along the stand.</summary>
    public (int X, int Y) Along => Facing is Facing.North or Facing.South ? (1, 0) : (0, 1);

    /// <summary>The way the people on it look, as a step in tiles.</summary>
    public (int X, int Y) Looking => Facing switch
    {
        Facing.North => (0, -1),
        Facing.South => (0, 1),
        Facing.West => (-1, 0),
        _ => (1, 0),
    };

    /// <summary>How many people it holds.</summary>
    public int Places => Stands.SpotsOf(this).Count();

    /// <summary>The middle of it, in metres.</summary>
    public (double X, double Y) Middle => ((Rect.X + (Rect.Width / 2.0)) * FieldMap.TileMetres, (Rect.Y + (Rect.Height / 2.0)) * FieldMap.TileMetres);
}

/// <summary>The sizes, heights, prices and rules of stands.</summary>
public static class Stands
{
    /// <summary>How deep a row is, in tiles (a metre).</summary>
    public const int RowTiles = 2;

    /// <summary>How much higher each row is than the one in front, in metres.</summary>
    public const double Rise = 0.4;

    /// <summary>How high a seat is above the row it is on, in metres.</summary>
    public const double SeatHeight = 0.45;

    /// <summary>How high a raised deck is, in metres.</summary>
    public const double DeckHeight = 1.4;

    /// <summary>How high an accessible deck is, in metres: low, so its ramp is gentle.</summary>
    public const double AccessibleHeight = 0.5;

    /// <summary>The fewest and most rows (for a deck, metres deep).</summary>
    public const int FewestRows = 1;

    public const int MostRows = 10;

    /// <summary>The shortest stand inside its rail, in tiles (3 m).</summary>
    public const int Shortest = 6;

    /// <summary>The longest stand inside its rail, in tiles (40 m).</summary>
    public const int Longest = 80;

    /// <summary>How wide each set of steps is, in tiles (a metre and a half).</summary>
    public const int StepTiles = 3;

    /// <summary>The most sets of steps one stand can have.</summary>
    public const int MostSteps = 4;

    /// <summary>The most stands a site can have.</summary>
    public const int MostStands = 20;

    /// <summary>What a seat ticket costs to start with, and the most it can be, in pounds.</summary>
    public const int StartPrice = 5;

    public const int MostPrice = 30;

    /// <summary>A seat on a seated stand, in pounds.</summary>
    public const int SeatCost = 6;

    /// <summary>A standing place on a terrace, in pounds.</summary>
    public const int TerraceCost = 3;

    /// <summary>A square metre of raised deck, in pounds.</summary>
    public const int DeckCostPerMetre = 12;

    /// <summary>A square metre of accessible deck, ramp and all, in pounds.</summary>
    public const int AccessibleCostPerMetre = 14;

    /// <summary>A metre of rail round the edge, in pounds.</summary>
    public const int RailCostPerMetre = 3;

    /// <summary>A metre of roof along the stand, in pounds: the full roof, and half of it.</summary>
    public const int RoofCostPerMetre = 20;

    public const int HalfRoofCostPerMetre = 12;

    /// <summary>How far a stand can be from a stage's front and still be a place to watch it from, in metres.</summary>
    public const double Reach = 60;

    /// <summary>How much further back people on a stand see and hear well, being above the heads in front.</summary>
    public const double SeeFurther = 1.6;

    /// <summary>
    /// How many more than its places can squeeze onto a stand with nobody on its steps, as a share
    /// of its places. That is when people start to fall.
    /// </summary>
    public const double Squeeze = 0.3;

    public static string Name(StandKind kind) => kind switch
    {
        StandKind.Seated => "Seated stand",
        StandKind.Terrace => "Standing terrace",
        StandKind.Deck => "Raised deck",
        _ => "Accessible deck",
    };

    public static string Name(StandRoof roof) => roof switch
    {
        StandRoof.BackHalf => "Back half",
        StandRoof.Full => "Full",
        _ => "None",
    };

    public static string Name(StandUse use) => use switch
    {
        StandUse.SeatTicket => "Seat ticket",
        StandUse.Vip => "VIP only",
        _ => "Anyone, first come",
    };

    /// <summary>True for the stands with rows that step up, rather than a flat deck.</summary>
    public static bool Tiered(StandKind kind) => kind is StandKind.Seated or StandKind.Terrace;

    /// <summary>How high the floor of a row is, in metres, for a stand of this kind.</summary>
    public static double FloorOf(StandKind kind, int row) => kind switch
    {
        StandKind.Deck => DeckHeight,
        StandKind.Accessible => AccessibleHeight,
        _ => Rise * (row + 1),
    };

    /// <summary>How high the top of a stand is, in metres.</summary>
    public static double TopOf(PlacedStand stand) => FloorOf(stand.Kind, Math.Max(0, stand.Rows - 1));

    /// <summary>
    /// The places people take on a stand, one person each. On a seated stand a seat at the back of
    /// each row; on a terrace a place at the front of each step; on a deck every other tile (two
    /// people a square metre), and on an accessible deck one in four (room for a wheelchair).
    /// </summary>
    public static IEnumerable<TilePoint> SpotsOf(PlacedStand stand)
    {
        var rows = stand.Rows;
        for (var v = 0; v < stand.Depth; v++)
        {
            for (var u = 0; u < stand.Length; u++)
            {
                var take = stand.Kind switch
                {
                    StandKind.Seated => v % RowTiles == RowTiles - 1 && v / RowTiles < rows,
                    StandKind.Terrace => v % RowTiles == 0 && v / RowTiles < rows,
                    StandKind.Deck => (u + v) % 2 == 0,
                    _ => u % 2 == 0 && v % 2 == 0,
                };
                if (take)
                {
                    yield return stand.At(u, v);
                }
            }
        }
    }

    /// <summary>What a stand costs to put up, roof and rail and all.</summary>
    public static int CostOf(StandKind kind, int length, int rows, StandRoof roof)
    {
        var metres = length * FieldMap.TileMetres;
        var places = kind switch
        {
            StandKind.Seated => length * rows * SeatCost,
            StandKind.Terrace => length * rows * TerraceCost,
            StandKind.Deck => (int)Math.Round(metres * rows * DeckCostPerMetre),
            _ => (int)Math.Round(metres * rows * AccessibleCostPerMetre),
        };
        var rail = (int)Math.Round(((2 * metres) + (2 * rows)) * RailCostPerMetre);
        var top = roof switch
        {
            StandRoof.Full => (int)Math.Round(metres * RoofCostPerMetre),
            StandRoof.BackHalf => (int)Math.Round(metres * HalfRoofCostPerMetre),
            _ => 0,
        };
        return places + rail + top;
    }

    /// <summary>What a stand that is up cost.</summary>
    public static int CostOf(PlacedStand stand) => CostOf(stand.Kind, stand.Length, stand.Rows, stand.Roof);
}

/// <summary>
/// Stands: seats on tiers, standing terraces and raised decks, dragged out along a line. People
/// walk up the steps and watch the nearest stage from them over the heads of the crowd.
/// </summary>
public sealed partial class SiteLayout
{
    private readonly List<PlacedStand> _stands = new();
    private readonly Dictionary<TilePoint, int> _standOf = new();
    private readonly HashSet<TilePoint> _standRails = new();

    /// <summary>Raised when who may use a stand, or what its seats cost, changes. Nothing on the ground moves.</summary>
    public event Action? StandsChanged;

    public IReadOnlyList<PlacedStand> Stands => _stands;

    /// <summary>The stand whose box a tile is in, rail and steps included, or -1.</summary>
    public int StandAt(TilePoint p) => _standOf.TryGetValue(p, out var stand) ? stand : -1;

    /// <summary>True for a tile where a stand's rail stands (not its steps).</summary>
    public bool IsStandRail(TilePoint p) => _standRails.Contains(p);

    /// <summary>
    /// The box a stand would take if dragged along the line between two tiles, with so many rows.
    /// The line is its front; the rows go back from it, away from the nearest stage. Turned round,
    /// they go the other way.
    /// </summary>
    public (TileRect Rect, Facing Facing) StandBox(TilePoint from, TilePoint to, int rows, bool turned = false)
    {
        var across = Math.Abs(to.X - from.X) >= Math.Abs(to.Y - from.Y);
        var depth = (Math.Clamp(rows, Site.Stands.FewestRows, Site.Stands.MostRows) * Site.Stands.RowTiles) + 2;
        var line = across ? from.Y : from.X;
        var (lo, hi) = across ? (Math.Min(from.X, to.X), Math.Max(from.X, to.X)) : (Math.Min(from.Y, to.Y), Math.Max(from.Y, to.Y));

        // Face the nearest stage, if it is to one side of the line; otherwise north or west.
        var midX = (across ? (lo + hi + 1) / 2.0 : line + 0.5) * FieldMap.TileMetres;
        var midY = (across ? line + 0.5 : (lo + hi + 1) / 2.0) * FieldMap.TileMetres;
        var lookBack = true;
        var best = double.MaxValue;
        foreach (var stage in _stages)
        {
            var (fx, fy, _, _) = Front(stage.Rect, stage.Facing);
            var metres = Math.Sqrt(((fx - midX) * (fx - midX)) + ((fy - midY) * (fy - midY)));
            if (metres < best)
            {
                best = metres;
                lookBack = across ? fy < midY : fx < midX;
            }
        }

        lookBack ^= turned;
        if (across)
        {
            return lookBack ? (new TileRect(lo, line, hi - lo + 1, depth), Facing.North) : (new TileRect(lo, line - depth + 1, hi - lo + 1, depth), Facing.South);
        }

        return lookBack ? (new TileRect(line, lo, depth, hi - lo + 1), Facing.West) : (new TileRect(line - depth + 1, lo, depth, hi - lo + 1), Facing.East);
    }

    /// <summary>Whether a stand could be dragged along the line between two tiles, and what it costs.</summary>
    public BuildCheck CheckStand(StandKind kind, TilePoint from, TilePoint to, int rows, StandRoof roof, bool turned = false)
    {
        var (rect, facing) = StandBox(from, to, rows, turned);
        var probe = new PlacedStand(kind, rect, facing, roof, new());
        if (probe.Length < Site.Stands.Shortest)
        {
            return BuildCheck.No("A stand needs to be at least 4 m long.");
        }

        if (probe.Length > Site.Stands.Longest)
        {
            return BuildCheck.No("A stand can be at most 41 m long.");
        }

        if (_stands.Count >= Site.Stands.MostStands)
        {
            return BuildCheck.No($"There can be at most {Site.Stands.MostStands} stands.");
        }

        foreach (var p in TilesOf(rect))
        {
            if (!_map.InBounds(p.X, p.Y) || _map[p] != Ground.Grass || !_map.InFestival(p))
            {
                return BuildCheck.No("A stand must be on the grass of the festival field.");
            }

            if (StandAt(p) >= 0 || AreaAt(p) >= 0)
            {
                return BuildCheck.No("Something is in the way.");
            }

            if (!IsEmpty(p) || _keepOut.Contains(p) || _vipGround.Contains(p))
            {
                return BuildCheck.No("Something is in the way.");
            }
        }

        return new BuildCheck(true, Site.Stands.CostOf(kind, probe.Length, probe.Rows, roof), null);
    }

    /// <summary>Puts up a stand along the line between two tiles, with its steps where people will come to it from.</summary>
    public BuildCheck AddStand(StandKind kind, TilePoint from, TilePoint to, int rows, StandRoof roof, bool turned = false)
    {
        var check = CheckStand(kind, from, to, rows, roof, turned);
        if (!check.Ok)
        {
            return check;
        }

        var (rect, facing) = StandBox(from, to, rows, turned);
        var stand = new PlacedStand(kind, rect, facing, roof, new());
        _stands.Add(stand with { Steps = FirstSteps(stand) });
        Refresh();
        return check;
    }

    /// <summary>
    /// Where the steps go to start with: along the front of a tiered stand (people climb up from
    /// the front, as at any stand), and at the back of a deck. One set, or two on a long stand.
    /// </summary>
    private static List<TilePoint> FirstSteps(PlacedStand stand)
    {
        var length = stand.Length + 2;
        var at = length >= 20 ? new[] { 1 + (length / 6) + 1, length - 3 - (length / 6) } : new[] { length / 2 };
        var v = Site.Stands.Tiered(stand.Kind) ? -1 : stand.Depth;
        return at.Select(k => Math.Clamp(k, 2, length - 3))
            .Distinct()
            .Select(k => stand.At(k - 1, v))
            .ToList();
    }

    /// <summary>The tiles of one set of steps, along the edge it is in.</summary>
    public static IEnumerable<TilePoint> StepTilesOf(PlacedStand stand, TilePoint middle)
    {
        var r = stand.Rect;
        var alongX = middle.Y == r.Y || middle.Y == r.Bottom - 1;
        for (var k = -(Site.Stands.StepTiles / 2); k <= Site.Stands.StepTiles / 2; k++)
        {
            yield return alongX ? new TilePoint(middle.X + k, middle.Y) : new TilePoint(middle.X, middle.Y + k);
        }
    }

    /// <summary>
    /// Opens a set of steps in a stand's rail at a tile on its edge, or closes the steps there. A
    /// stand always keeps at least one way up. Returns a problem, or null if it worked.
    /// </summary>
    public string? ToggleSteps(TilePoint p)
    {
        var index = StandAt(p);
        if (index < 0 || !_stands[index].IsEdge(p))
        {
            return "Click on the rail round a stand.";
        }

        var stand = _stands[index];
        var steps = stand.Steps.ToList();
        var hit = steps.FindIndex(s => StepTilesOf(stand, s).Contains(p));
        if (hit >= 0)
        {
            if (steps.Count == 1)
            {
                return "A stand needs at least one way up.";
            }

            steps.RemoveAt(hit);
        }
        else
        {
            var r = stand.Rect;
            var onTop = p.Y == r.Y || p.Y == r.Bottom - 1;
            var onSide = p.X == r.X || p.X == r.Right - 1;
            if (onTop && onSide)
            {
                return "Steps cannot go on a corner.";
            }

            if (steps.Count >= Site.Stands.MostSteps)
            {
                return $"A stand can have at most {Site.Stands.MostSteps} sets of steps.";
            }

            var middle = onTop ? new TilePoint(Math.Clamp(p.X, r.X + 2, r.Right - 3), p.Y) : new TilePoint(p.X, Math.Clamp(p.Y, r.Y + 2, r.Bottom - 3));
            if (StepTilesOf(stand, middle).Any(t => steps.Any(s => StepTilesOf(stand, s).Contains(t))))
            {
                return "There are steps there already.";
            }

            steps.Add(middle);
        }

        _stands[index] = stand with { Steps = steps };
        Refresh();
        return null;
    }

    /// <summary>Changes who may go up on a stand. Keeping it for VIPs changes where people can walk, so the site is worked out again.</summary>
    public bool SetStandUse(int stand, StandUse use)
    {
        if (stand < 0 || stand >= _stands.Count)
        {
            return false;
        }

        var vipBefore = _stands[stand].Use == StandUse.Vip;
        _stands[stand] = _stands[stand] with { Use = use };
        if (vipBefore != (use == StandUse.Vip))
        {
            Refresh();
        }

        StandsChanged?.Invoke();
        return true;
    }

    /// <summary>Changes what a seat ticket for a stand costs. Returns the price kept, or null if there is no such stand.</summary>
    public int? SetStandPrice(int stand, int price)
    {
        if (stand < 0 || stand >= _stands.Count)
        {
            return null;
        }

        var kept = Math.Clamp(price, 1, Site.Stands.MostPrice);
        _stands[stand] = _stands[stand] with { Price = kept };
        StandsChanged?.Invoke();
        return kept;
    }

    /// <summary>True if a place, in metres, is under the roof of a stand.</summary>
    public bool StandRoofOver(double x, double y)
    {
        var p = FieldMap.TileAt(x, y);
        if (StandAt(p) is not (>= 0 and var index))
        {
            return false;
        }

        var stand = _stands[index];
        if (stand.Roof == StandRoof.None || !stand.IsInside(p))
        {
            return false;
        }

        return stand.Roof == StandRoof.Full || stand.LocalOf(p).V >= stand.Depth / 2;
    }

    /// <summary>What taking down a stand gives back: all it cost.</summary>
    private int StandRefundAt(TilePoint p) => StandAt(p) is >= 0 and var stand ? Site.Stands.CostOf(_stands[stand]) : 0;

    private bool RemoveStandAt(TilePoint p)
    {
        if (StandAt(p) is not (>= 0 and var stand))
        {
            return false;
        }

        _stands.RemoveAt(stand);
        return true;
    }

    /// <summary>Works out which tiles each stand covers, and where its rail stands.</summary>
    private void LayStands()
    {
        _standOf.Clear();
        _standRails.Clear();
        for (var i = 0; i < _stands.Count; i++)
        {
            var stand = _stands[i];
            var open = new HashSet<TilePoint>(stand.Steps.SelectMany(s => StepTilesOf(stand, s)));
            foreach (var p in TilesOf(stand.Rect))
            {
                _standOf[p] = i;
                if (stand.IsEdge(p) && !open.Contains(p))
                {
                    _standRails.Add(p);
                }
            }
        }
    }

    /// <summary>
    /// How high people stand on each tile of the stands: each row higher than the one in front, a
    /// deck all at one height, the rail as high as the row beside it and the steps half way.
    /// </summary>
    private void StandHeights()
    {
        foreach (var stand in _stands)
        {
            var open = new HashSet<TilePoint>(stand.Steps.SelectMany(s => StepTilesOf(stand, s)));
            foreach (var p in TilesOf(stand.Rect))
            {
                var (u, v) = stand.LocalOf(p);
                var row = Math.Clamp(v, 0, stand.Depth - 1) / Site.Stands.RowTiles;
                var floor = Site.Stands.FloorOf(stand.Kind, row);
                _heights[p] = open.Contains(p) ? floor / 2 : floor;
                _ = u;
            }
        }
    }

    /// <summary>The tiles on the stands kept for VIP guests.</summary>
    private IEnumerable<TilePoint> VipStandTiles() =>
        _stands.Where(s => s.Use == StandUse.Vip).SelectMany(s => TilesOf(s.Rect).Where(p => !_standRails.Contains(p)));

    private void RestoreStands(SiteSave save)
    {
        _stands.Clear();
        foreach (var stand in save.Stands ?? new())
        {
            if (stand.Length >= Site.Stands.Shortest && stand.Depth >= Site.Stands.RowTiles
                && TilesOf(stand.Rect).All(p => _map.InBounds(p.X, p.Y) && _stands.All(s => !s.Rect.Contains(p)) && StageAt(p) is null && StallAt(p) is null && GateAt(p) is null))
            {
                _stands.Add(stand with { Steps = stand.Steps is { Count: > 0 } steps ? steps : FirstSteps(stand) });
            }
        }
    }
}
