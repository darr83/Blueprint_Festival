using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.People;

/// <summary>
/// People and the crowd areas. Security on a gap count people in, and once an area holds its
/// limit nobody else picks a place inside it: keen fans wait by the gaps for someone to come out,
/// everyone else finds a place outside. An area with nobody on its gaps fills like open ground.
/// </summary>
public sealed partial class Crowd
{
    private int[] _inArea = Array.Empty<int>();
    private int[] _waitingAt = Array.Empty<int>();
    private bool[] _manned = Array.Empty<bool>();
    private bool[] _fullNoted = Array.Empty<bool>();

    /// <summary>The most parties that wait by one gap of a full area. Anyone else finds somewhere outside.</summary>
    public const int WaitPerGap = 6;

    /// <summary>How keen on an act a party must be to wait by the gap of a full area rather than stand elsewhere.</summary>
    public const double KeenToWait = 0.55;

    /// <summary>Whether security are on an area's gaps, if the game knows. Left unset, every area is kept.</summary>
    public Func<int, bool>? AreaManned { get; set; }

    /// <summary>Raised when a kept area fills to its limit, with the area's number.</summary>
    public event Action<int>? AreaFilled;

    /// <summary>How many people are in an area, or on their way to a place in it, as last counted.</summary>
    public int InArea(int area) => area >= 0 && area < _inArea.Length ? _inArea[area] : 0;

    /// <summary>How many people are waiting by an area's gaps to be let in.</summary>
    public int WaitingAt(int area) => area >= 0 && area < _waitingAt.Length ? _waitingAt[area] : 0;

    /// <summary>True if security are on an area's gaps, as last checked.</summary>
    public bool Kept(int area) => area >= 0 && area < _manned.Length && _manned[area];

    /// <summary>True if an area is kept and holds as many as it lets in.</summary>
    public bool IsFull(int area) => Kept(area) && InArea(area) >= _site.Areas[area].Limit;

    private void SizeAreas()
    {
        var n = _site.Areas.Count;
        if (_inArea.Length != n)
        {
            _inArea = new int[n];
            _waitingAt = new int[n];
            _manned = new bool[n];
            _fullNoted = new bool[n];
        }
    }

    /// <summary>
    /// Counts everyone in each area (or walking to a place in it), and everyone waiting by its
    /// gaps, once a minute. Then lets waiting people in where there is room.
    /// </summary>
    private void CountAreas(DateTime now)
    {
        SizeAreas();
        if (_inArea.Length == 0)
        {
            return;
        }

        Array.Clear(_inArea);
        Array.Clear(_waitingAt);
        for (var i = 0; i < _manned.Length; i++)
        {
            _manned[i] = AreaManned?.Invoke(i) ?? true;
        }

        foreach (var party in _parties)
        {
            if (party.WaitFor >= 0 && (party.WaitFor >= _inArea.Length || party.Watch < 0 || party.State is not (PartyState.Watching or PartyState.Strolling) || !StillOn(party.Watch, now)))
            {
                party.WaitFor = -1;
            }

            if (party.WaitFor >= 0)
            {
                _waitingAt[party.WaitFor] += party.Members.Count;
                continue;
            }

            if (party.IsOnSite && party.State is PartyState.Watching or PartyState.Strolling or PartyState.Standing or PartyState.Sitting
                && _site.AreaInside(party.Target) is >= 0 and var area && area < _inArea.Length)
            {
                _inArea[area] += party.Members.Count;
            }
        }

        for (var i = 0; i < _inArea.Length; i++)
        {
            var limit = _site.Areas[i].Limit;
            if (_manned[i] && _inArea[i] >= limit)
            {
                if (!_fullNoted[i])
                {
                    _fullNoted[i] = true;
                    AreaFilled?.Invoke(i);
                }
            }
            else if (_inArea[i] < limit * 0.85)
            {
                _fullNoted[i] = false;
            }

            if (_manned[i] && _waitingAt[i] > 0 && _inArea[i] < limit)
            {
                LetIn(i, now);
            }
        }
    }

    /// <summary>One out, one in: lets people waiting by an area's gaps in while there is room.</summary>
    private void LetIn(int area, DateTime now)
    {
        var limit = _site.Areas[area].Limit;
        foreach (var party in _parties)
        {
            if (party.WaitFor != area)
            {
                continue;
            }

            if (_inArea[area] + party.Members.Count > limit)
            {
                break;
            }

            if (SpotInside(area, party) is not { } spot)
            {
                break;
            }

            var watching = party.Watch;
            Send(party, spot, PartyState.Strolling);
            party.Watch = party.State == PartyState.Strolling ? watching : -1;
            party.WaitFor = -1;
            _inArea[area] += party.Members.Count;
            _waitingAt[area] -= party.Members.Count;
            if (party.Watch >= 0)
            {
                Claim(spot, party.Members.Count, RunningOrder.StageOf(party.Watch));
            }
        }
    }

    /// <summary>
    /// Whether people looking for somewhere to watch a stage's act may pick a tile inside an area.
    /// Not inside another stage's area, nor inside one that security have closed because it is full.
    /// </summary>
    private bool MayPickInside(int area, PlacedStage stage, int people)
    {
        SizeAreas();
        var a = _site.Areas[area];
        var owner = _site.StageOfArea(a);
        if (owner >= 0 && _site.Stages[owner] != stage)
        {
            return false;
        }

        return !_manned[area] || _inArea[area] + people <= a.Limit;
    }

    /// <summary>How tightly people pack in one square of a kept area: no tighter than its limit spread over it, give or take.</summary>
    private double PackInside(int area)
    {
        var a = _site.Areas[area];
        return Math.Max(DensityMap.Busy, 1.2 * a.Limit / Math.Max(1, a.InsideMetres));
    }

    /// <summary>Notes that a party has picked a place inside an area, so the count is right before the next minute.</summary>
    private void NoteInside(TilePoint spot, int people)
    {
        if (_site.AreaInside(spot) is >= 0 and var area && area < _inArea.Length)
        {
            _inArea[area] += people;
        }
    }

    /// <summary>A free place inside an area for a party, or null if every place tried is taken.</summary>
    private TilePoint? SpotInside(int area, Party party)
    {
        var r = _site.Areas[area].Rect;
        var pack = _manned.Length > area && _manned[area] ? PackInside(area) : PackedLimit;
        for (var attempt = 0; attempt < 30; attempt++)
        {
            var tile = new TilePoint(r.X + 2 + party.Random.Next(Math.Max(1, r.Width - 4)), r.Y + 2 + party.Random.Next(Math.Max(1, r.Height - 4)));
            if (_site.AreaInside(tile) != area || !WalkableFor(party, tile))
            {
                continue;
            }

            var (x, y) = FieldMap.CentreOf(tile);
            if (Claimed(x, y, party.Members.Count) <= pack)
            {
                return tile;
            }
        }

        return null;
    }

    /// <summary>A place in one of the VIP areas by a stage for a VIP party, while there is room in it.</summary>
    private TilePoint? VipAreaSpot(Party party, PlacedStage stage)
    {
        SizeAreas();
        for (var i = 0; i < _site.Areas.Count; i++)
        {
            var a = _site.Areas[i];
            if (a.Kind != AreaKind.Vip || !MayPickInside(i, stage, party.Members.Count) || _site.StageOfArea(a) < 0)
            {
                continue;
            }

            if (SpotInside(i, party) is { } spot)
            {
                return spot;
            }
        }

        return null;
    }

    /// <summary>
    /// A place to wait just outside one of a full area's gaps, for a keen fan. Null if the gaps
    /// already have as many waiting as they take, or there is no room by them.
    /// </summary>
    private TilePoint? WaitByGap(int area, Party party)
    {
        var a = _site.Areas[area];
        if (a.Gaps.Count == 0 || _waitingAt[area] >= WaitPerGap * a.Gaps.Count * 2)
        {
            return null;
        }

        var gap = a.Gaps[party.Random.Next(a.Gaps.Count)];
        var (ox, oy) = SiteLayout.Outward(a, gap);
        for (var attempt = 0; attempt < 12; attempt++)
        {
            var out1 = 2 + party.Random.Next(5);
            var side = party.Random.Next(7) - 3;
            var tile = new TilePoint(gap.X + (ox * out1) - (oy * side), gap.Y + (oy * out1) + (ox * side));
            if (_standableSet.Contains(tile) && _site.AreaAt(tile) < 0)
            {
                return tile;
            }
        }

        return null;
    }
}
