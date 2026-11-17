using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.People;

/// <summary>
/// People and the stands. Some of those going to watch an act would sooner sit, or see over
/// everyone's heads, or keep out of the rain: they walk up the steps of a stand that faces the
/// stage and take a place in a row. With a steward on its steps a stand never holds more than its
/// places; with nobody there people squeeze on, and on a crowded stand someone can fall.
/// </summary>
public sealed partial class Crowd
{
    private int[] _onStand = Array.Empty<int>();
    private bool[] _standKept = Array.Empty<bool>();
    private readonly HashSet<TilePoint> _standTaken = new();
    private readonly List<List<List<TilePoint>>> _standRows = new();

    /// <summary>Whether a steward is on a stand's steps, if the game knows. Left unset, every stand is kept.</summary>
    public Func<int, bool>? StandManned { get; set; }

    /// <summary>Raised when people pay for seat tickets at a stand's steps: which stand, and how much in all.</summary>
    public event Action<int, int>? SeatTicketsSold;

    /// <summary>Raised when someone falls on a stand that has more people on it than places.</summary>
    public event Action<Party>? FellOnStand;

    /// <summary>How many people are on a stand, or on their way up it, as last counted.</summary>
    public int OnStand(int stand) => stand >= 0 && stand < _onStand.Length ? _onStand[stand] : 0;

    /// <summary>True if a steward is on a stand's steps, as last checked.</summary>
    public bool StandKept(int stand) => stand >= 0 && stand < _standKept.Length && _standKept[stand];

    /// <summary>True if a stand holds as many as it has places.</summary>
    public bool StandFull(int stand) => stand >= 0 && stand < _site.Stands.Count && OnStand(stand) >= Places(stand);

    /// <summary>How many places a stand has.</summary>
    public int Places(int stand)
    {
        LayStandRows();
        return stand >= 0 && stand < _standRows.Count ? _standRows[stand].Sum(r => r.Count) : 0;
    }

    /// <summary>How many people have bought a seat ticket at a stand so far today.</summary>
    public int SeatTickets { get; private set; }

    /// <summary>True for a party watching an act from a stand.</summary>
    public static bool UpOnAStand(Party party) => party.Stand >= 0 && party.State == PartyState.Watching;

    /// <summary>True for a party sat in the seats of a seated stand, watching.</summary>
    private bool InTheSeats(Party party) => UpOnAStand(party) && party.Stand < _site.Stands.Count && _site.Stands[party.Stand].Kind == StandKind.Seated;

    /// <summary>Works out again the rows of places on every stand, if the stands have changed.</summary>
    private void LayStandRows()
    {
        if (_standRows.Count == _site.Stands.Count && SameStands())
        {
            return;
        }

        _standRows.Clear();
        _laidStands.Clear();
        foreach (var stand in _site.Stands)
        {
            _laidStands.Add(stand);
            var rows = new SortedDictionary<int, List<TilePoint>>();
            foreach (var spot in Stands.SpotsOf(stand))
            {
                var (_, v) = stand.LocalOf(spot);
                if (!rows.TryGetValue(v, out var row))
                {
                    rows[v] = row = new List<TilePoint>();
                }

                row.Add(spot);
            }

            _standRows.Add(rows.Values.ToList());
        }

        if (_onStand.Length != _site.Stands.Count)
        {
            _onStand = new int[_site.Stands.Count];
            _standKept = new bool[_site.Stands.Count];
        }

        _standTaken.Clear();
    }

    private readonly List<PlacedStand> _laidStands = new();

    private bool SameStands()
    {
        if (_laidStands.Count != _site.Stands.Count)
        {
            return false;
        }

        for (var i = 0; i < _laidStands.Count; i++)
        {
            var (a, b) = (_laidStands[i], _site.Stands[i]);
            if (a.Kind != b.Kind || a.Rect != b.Rect || a.Facing != b.Facing)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Counts everyone on each stand (or on their way up it) once a minute, and which places are
    /// taken. On a crowded stand with nobody on its steps, someone may fall.
    /// </summary>
    private void CountStands(DateTime now)
    {
        LayStandRows();
        if (_standRows.Count == 0)
        {
            return;
        }

        Array.Clear(_onStand);
        _standTaken.Clear();
        for (var i = 0; i < _standKept.Length; i++)
        {
            _standKept[i] = StandManned?.Invoke(i) ?? true;
        }

        foreach (var party in _parties)
        {
            if (party.Stand < 0)
            {
                continue;
            }

            if (party.Stand >= _standRows.Count || !party.IsOnSite || party.Watch < 0 || party.State is not (PartyState.Watching or PartyState.Strolling))
            {
                party.Stand = -1;
                continue;
            }

            _onStand[party.Stand] += party.Members.Count;
            if (Spots(party) is { } row)
            {
                for (var i = 0; i < party.Members.Count && party.StandFirst + i < row.Count; i++)
                {
                    _standTaken.Add(row[party.StandFirst + i]);
                }
            }
        }

        var minute = (int)now.TimeOfDay.TotalMinutes;
        for (var i = 0; i < _onStand.Length; i++)
        {
            var places = Places(i);
            if (_standKept[i] || places == 0 || _onStand[i] <= places)
            {
                continue;
            }

            // Squeezed on past its places with nobody on the steps: people jostle, and someone may go over.
            var over = (_onStand[i] - places) / (double)places;
            if (Roll(i, 83, minute) < Math.Min(0.25, over * 0.8))
            {
                var on = _parties.Where(p => p.Stand == i && UpOnAStand(p)).ToList();
                if (on.Count > 0)
                {
                    var fell = on[(int)(Roll(i, 85, minute) * on.Count) % on.Count];
                    FellOnStand?.Invoke(fell);
                    TakeIll(fell);
                }
            }
        }
    }

    /// <summary>The row of places a party has on a stand, or null.</summary>
    private List<TilePoint>? Spots(Party party)
    {
        if (party.Stand < 0 || party.Stand >= _standRows.Count)
        {
            return null;
        }

        var rows = _standRows[party.Stand];
        return party.StandRow >= 0 && party.StandRow < rows.Count ? rows[party.StandRow] : null;
    }

    /// <summary>Where one member of a party on a stand is, and the way they face: their own place in the row, looking out.</summary>
    private (double X, double Y, double Heading)? StandPlace(Party party, int member)
    {
        if (!UpOnAStand(party) || Spots(party) is not { } row || party.StandFirst + member >= row.Count)
        {
            return null;
        }

        var stand = _site.Stands[party.Stand];
        var (x, y) = FieldMap.CentreOf(row[party.StandFirst + member]);
        var (lx, ly) = stand.Looking;
        return (x, y, Math.Atan2(lx, ly));
    }

    /// <summary>True if a party may go up on a stand: VIP stands for VIPs, the accessible deck for those who need it.</summary>
    public static bool MayUse(Party party, PlacedStand stand) =>
        (stand.Use != StandUse.Vip || party.Vip) && (stand.Kind != StandKind.Accessible || party.NeedsAccess);

    /// <summary>
    /// How much a party fancies watching from a stand rather than down in the crowd: some like to
    /// sit, more when it rains and the stand has a roof, fewer for a big act they want to be close
    /// to, and fewer the dearer a seat ticket is. Those who cannot stand in a crowd want the
    /// accessible deck most of all.
    /// </summary>
    public double StandWish(Party party, PlacedStand stand, double draw)
    {
        if (!MayUse(party, stand))
        {
            return 0;
        }

        var wish = stand.Kind switch
        {
            StandKind.Accessible => 0.9,
            StandKind.Seated => party.NeedsAccess ? 0.6 : 0.3,
            StandKind.Terrace => 0.22,
            _ => 0.28,
        };
        if (stand.Use == StandUse.Vip)
        {
            wish = Math.Max(wish, 0.6);
        }

        if (stand.Kind != StandKind.Accessible && draw >= 0.75)
        {
            // The keenest fans want to be right at the front.
            wish *= 0.5;
        }

        if (Rain > 0.05 && stand.Roof != StandRoof.None)
        {
            wish += stand.Roof == StandRoof.Full ? 0.45 : 0.3;
        }

        if (stand.Use == StandUse.SeatTicket)
        {
            wish *= WillPay(stand.Price);
        }

        return Math.Clamp(wish, 0, 0.95);
    }

    /// <summary>The share of people who would pay a seat ticket price, in pounds: most at £5, few past £12.</summary>
    public static double WillPay(int price) => Math.Clamp(1.2 - (price / 12.0), 0, 1);

    /// <summary>
    /// A place on a stand facing a stage for a party that fancies one: the stand, the row, the
    /// first of the places side by side, and the tile to walk to. Null if they would rather stand
    /// in the crowd, or no stand near has room.
    /// </summary>
    private (int Stand, int Row, int First, TilePoint Tile)? StandSpot(Party party, PlacedStage stage, double draw)
    {
        if (_site.Stands.Count == 0)
        {
            return null;
        }

        LayStandRows();
        var (fx, fy, _, _) = RunningOrder.Front(stage);
        var roll = party.Random.NextDouble();
        var people = party.Members.Count;
        // Nearer stands first, but not always: people spread over the stands rather than all try the nearest.
        var order = Enumerable.Range(0, _site.Stands.Count).Select(i => (i, Away(_site.Stands[i], fx, fy) + (party.Random.NextDouble() * Stands.Reach / 2))).OrderBy(x => x.Item2).Select(x => x.i).ToList();
        foreach (var i in order)
        {
            var stand = _site.Stands[i];
            var away = Away(stand, fx, fy);
            if (away > Stands.Reach || !Faces(stand, fx, fy) || roll >= StandWish(party, stand, draw))
            {
                continue;
            }

            var kept = StandKept(i) || StandManned is null;
            var places = Places(i);
            if (OnStand(i) + people > places * (kept ? 1 : 1 + Stands.Squeeze))
            {
                continue;
            }

            var rows = _standRows[i];
            var start = party.Random.Next(Math.Max(1, rows.Count));
            for (var k = 0; k < rows.Count; k++)
            {
                var r = (start + k) % rows.Count;
                var row = rows[r];
                if (row.Count < people)
                {
                    continue;
                }

                var from = party.Random.Next(row.Count - people + 1);
                for (var j = 0; j <= row.Count - people; j++)
                {
                    var first = (from + j) % (row.Count - people + 1);
                    var free = true;
                    for (var m = 0; m < people && free; m++)
                    {
                        free = !_standTaken.Contains(row[first + m]);
                    }

                    if (free)
                    {
                        return (i, r, first, row[first]);
                    }
                }
            }

            if (!kept)
            {
                // Nobody on the steps to say it is full: they squeeze in where they can.
                var r = party.Random.Next(rows.Count);
                if (rows[r].Count >= people)
                {
                    var first = party.Random.Next(rows[r].Count - people + 1);
                    return (i, r, first, rows[r][first]);
                }
            }
        }

        return null;
    }

    /// <summary>Notes that a party has taken places on a stand: the places are theirs, and they pay for a seat ticket if it needs one.</summary>
    private void TakeStand(Party party, (int Stand, int Row, int First, TilePoint Tile) spot)
    {
        party.Stand = spot.Stand;
        party.StandRow = spot.Row;
        party.StandFirst = spot.First;
        var row = _standRows[spot.Stand][spot.Row];
        for (var m = 0; m < party.Members.Count && spot.First + m < row.Count; m++)
        {
            _standTaken.Add(row[spot.First + m]);
        }

        if (spot.Stand < _onStand.Length)
        {
            _onStand[spot.Stand] += party.Members.Count;
        }

        var stand = _site.Stands[spot.Stand];
        if (stand.Use == StandUse.SeatTicket)
        {
            var paid = stand.Price * party.Members.Count;
            party.Spent += paid;
            SeatTickets += party.Members.Count;
            SeatTicketsSold?.Invoke(spot.Stand, paid);
        }
    }

    /// <summary>True if a stand faces a place, in metres: the place is in front of it, not behind or off to one side.</summary>
    public static bool Faces(PlacedStand stand, double x, double y)
    {
        var (mx, my) = stand.Middle;
        var (lx, ly) = stand.Looking;
        var (dx, dy) = (x - mx, y - my);
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        return length < 1 || ((dx * lx) + (dy * ly)) / length > 0.25;
    }

    private static double Away(PlacedStand stand, double x, double y)
    {
        var (mx, my) = stand.Middle;
        return Math.Sqrt(((mx - x) * (mx - x)) + ((my - y) * (my - y)));
    }
}
