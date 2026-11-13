using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.People;

/// <summary>What a party of festival-goers is doing.</summary>
public enum PartyState
{
    /// <summary>Not here yet.</summary>
    Expected,

    /// <summary>Walking in along the lane and through the gap.</summary>
    WalkingIn,

    /// <summary>Queuing at a gate, or waiting outside because there is no way in.</summary>
    Queuing,

    /// <summary>Standing about in the field.</summary>
    Standing,

    /// <summary>Walking to somewhere else in the field.</summary>
    Strolling,

    /// <summary>Walking back out to the lane to go home.</summary>
    Leaving,

    /// <summary>Gone home.</summary>
    Gone,
}

/// <summary>One person in a party, and where they stand relative to its middle.</summary>
public sealed record PartyMember(int Id, PersonLook Look, double OffsetX, double OffsetY);

/// <summary>
/// People come to a festival with their friends or family, not alone. A party arrives together,
/// walks together at the pace of its slowest member, and goes home together.
/// </summary>
public sealed class Party
{
    internal Party(int id, IReadOnlyList<PartyMember> members, DateTime arrives, DateTime leaves, bool fromWest, double speed, GameRandom random)
    {
        Id = id;
        Members = members;
        Arrives = arrives;
        Leaves = leaves;
        FromWest = fromWest;
        Speed = speed;
        Random = random;
    }

    public int Id { get; }

    public IReadOnlyList<PartyMember> Members { get; }

    public DateTime Arrives { get; }

    public DateTime Leaves { get; }

    /// <summary>Which end of the lane they come from, and go home by.</summary>
    public bool FromWest { get; }

    /// <summary>Metres walked per tick.</summary>
    public double Speed { get; }

    public PartyState State { get; internal set; } = PartyState.Expected;

    /// <summary>Where the middle of the party is, in metres.</summary>
    public double X { get; internal set; }

    public double Y { get; internal set; }

    /// <summary>The way they face, in radians: 0 is south, a quarter turn is east.</summary>
    public double Heading { get; internal set; }

    public TilePoint Target { get; internal set; }

    public DateTime WaitUntil { get; internal set; }

    public bool IsOnSite => State is not (PartyState.Expected or PartyState.Gone);

    public bool IsWalking => State is PartyState.WalkingIn or PartyState.Strolling or PartyState.Leaving || (State == PartyState.Queuing && Moving);

    /// <summary>Which gate they are queuing at, or -1.</summary>
    public int Gate { get; internal set; } = -1;

    /// <summary>Which lane of that gate.</summary>
    public int Lane { get; internal set; }

    /// <summary>How many more seconds the ticket check will take, or -1 if it has not begun.</summary>
    public int ServiceLeft { get; internal set; } = -1;

    /// <summary>True if they came in through a gate, false if they walked in unchecked, null if they are not in yet.</summary>
    public bool? Ticketed { get; internal set; }

    internal bool Moving { get; set; }

    internal DateTime RetryAt { get; set; }

    public bool HasChild => Members.Any(m => m.Look.Age == AgeGroup.Child);

    internal GameRandom Random { get; set; }

    internal List<TilePoint>? Path { get; set; }

    internal int PathIndex { get; set; }
}

/// <summary>One person as the game draws them: who they are, where they are, which way they face.</summary>
public readonly record struct PersonOnSite(int Id, PersonLook Look, double X, double Y, double Heading, bool Walking);

/// <summary>
/// Everyone who comes to the festival. The whole day's comings and goings are worked out from the
/// festival's seed: between 100 and 200 people in parties of one to four, arriving through the
/// morning, wandering the field, and going home in the evening (families with children first).
/// </summary>
public sealed class Crowd
{
    /// <summary>
    /// How far someone walks in one tick (one game second). Game time runs 7.5 times faster than
    /// real time at 1x, so this is a real walking pace of about 1.3 m/s as you watch it.
    /// </summary>
    public const double WalkMetresPerTick = 1.3 / GameClock.GameSecondsPerRealSecond;

    public static readonly TimeSpan GatesOpen = TimeSpan.FromHours(8);
    public static readonly TimeSpan LastArrival = TimeSpan.FromHours(13);
    public static readonly TimeSpan EveryoneOut = TimeSpan.FromHours(23.5);

    /// <summary>How far apart parties stand in a queue, in metres, and how many places a queue has before it bunches up.</summary>
    private const double QueueSpacing = 1.1;
    private const int QueuePlaces = 16;

    private readonly FieldMap _map;
    private readonly SiteLayout _site;
    private readonly PathFinder _paths;
    private readonly List<Party> _parties = new();
    private readonly List<List<Party>[]> _queues = new();
    private readonly List<TilePoint> _standable = new();

    public Crowd(FieldMap map, int seed, DateTime festivalDay)
        : this(map, new SiteLayout(map), seed, festivalDay)
    {
    }

    public Crowd(FieldMap map, SiteLayout site, int seed, DateTime festivalDay)
    {
        _map = map;
        _site = site;
        _paths = new PathFinder(map);
        Seed = seed;
        Plan(festivalDay.Date);
        SiteChanged();
        site.Changed += SiteChanged;
    }

    /// <summary>How many people are queuing at gates or waiting outside.</summary>
    public int Queuing => _parties.Where(p => p.State == PartyState.Queuing).Sum(p => p.Members.Count);

    /// <summary>How many people have had their tickets checked at a gate today.</summary>
    public int ThroughGates => _parties.Where(p => p.Ticketed == true).Sum(p => p.Members.Count);

    /// <summary>How many people have walked in today with nobody checking tickets.</summary>
    public int WalkedInFree => _parties.Where(p => p.Ticketed == false).Sum(p => p.Members.Count);

    /// <summary>How many parties are in the queue for one lane of a gate.</summary>
    public int QueueLength(int gate, int lane) => gate < _queues.Count && lane < _queues[gate].Length ? _queues[gate][lane].Count : 0;

    public int Seed { get; }

    public IReadOnlyList<Party> Parties => _parties;

    /// <summary>How many people are expected over the whole day.</summary>
    public int Expected { get; private set; }

    /// <summary>How many people are on the site right now.</summary>
    public int OnSite => _parties.Where(p => p.IsOnSite).Sum(p => p.Members.Count);

    /// <summary>How many people have come through so far today, including those who have gone home.</summary>
    public int Arrived => _parties.Where(p => p.State != PartyState.Expected).Sum(p => p.Members.Count);

    /// <summary>How many of the people on site are inside the hedge.</summary>
    public int InField => _parties.Where(p => p.IsOnSite && _map.Field.Contains(FieldMap.TileAt(p.X, p.Y))).Sum(p => p.Members.Count);

    /// <summary>Everyone on site, one entry per person.</summary>
    public IEnumerable<PersonOnSite> People
    {
        get
        {
            foreach (var party in _parties)
            {
                if (!party.IsOnSite)
                {
                    continue;
                }

                foreach (var m in party.Members)
                {
                    yield return new PersonOnSite(m.Id, m.Look, party.X + m.OffsetX, party.Y + m.OffsetY, party.Heading, party.IsWalking);
                }
            }
        }
    }

    private void Plan(DateTime day)
    {
        var random = GameRandom.FromSeed(Seed, 1);
        var target = 100 + random.Next(101);
        var personId = 0;
        while (Expected < target)
        {
            var roll = random.NextDouble();
            var size = Math.Min(target - Expected, roll < 0.25 ? 1 : roll < 0.65 ? 2 : roll < 0.85 ? 3 : 4);
            var members = new List<PartyMember>();
            for (var i = 0; i < size; i++)
            {
                var lookSeed = unchecked((Seed * 31) + personId);
                var look = LookGenerator.FestivalGoer(lookSeed);

                // A child never comes alone: the first of any party is at least a teenager.
                if (i == 0 && look.Age == AgeGroup.Child)
                {
                    look = LookGenerator.FestivalGoer(lookSeed, AgeGroup.Adult);
                }

                // Members stand in a small ring round the party's middle.
                var angle = (Math.Tau * i / size) + random.Between(-0.3, 0.3);
                var radius = size == 1 ? 0 : random.Between(0.34, 0.5);
                members.Add(new PartyMember(personId++, look, Math.Cos(angle) * radius, Math.Sin(angle) * radius));
            }

            // Arrivals build to a peak in the first half of the morning, then tail off.
            var middle = (random.NextDouble() + random.NextDouble()) / 2;
            var when = middle * middle;
            var arrives = day + GatesOpen + TimeSpan.FromSeconds(60 + (when * (LastArrival - GatesOpen).TotalSeconds));

            var family = members.Any(m => m.Look.Age == AgeGroup.Child);
            var leaves = day + TimeSpan.FromHours(family ? random.Between(17, 20.5) : random.Between(20.5, 23));
            var speed = WalkMetresPerTick * (family ? random.Between(0.72, 0.85) : random.Between(0.88, 1.12));

            _parties.Add(new Party(_parties.Count, members, arrives, leaves, random.NextDouble() < 0.6, speed, GameRandom.FromSeed(Seed, 100 + _parties.Count)));
            Expected += size;
        }
    }

    private TilePoint LaneEnd(Party party)
    {
        var y = _map.Lane.Y + (_map.Lane.Height / 2) + (party.Id % 3) - 1;
        return new TilePoint(party.FromWest ? 0 : _map.Width - 1, y);
    }

    /// <summary>A place to stand in the field, well clear of the hedge. Null if there is nowhere.</summary>
    private TilePoint? SomewhereInField(GameRandom random) =>
        _standable.Count == 0 ? null : _standable[random.Next(_standable.Count)];

    /// <summary>
    /// Called whenever something is built or taken down: routes are worked out again, queues re-form,
    /// and anyone who has had something built on top of them steps aside.
    /// </summary>
    private void SiteChanged()
    {
        _paths.Refresh();

        // People stand on the festival side of the gates, or anywhere in the field if it is open.
        const int inset = 4;
        var wanted = _site.HasWorkingGate ? Side.Inside : Side.Outside;
        var f = _map.Field;
        _standable.Clear();
        for (var y = f.Y + inset; y < f.Bottom - inset; y++)
        {
            for (var x = f.X + inset; x < f.Right - inset; x++)
            {
                var p = new TilePoint(x, y);
                if (_site.SideOf(p) == wanted && HasRoom(p))
                {
                    _standable.Add(p);
                }
            }
        }

        _queues.Clear();
        foreach (var gate in _site.Gates)
        {
            var lanes = new List<Party>[Catalogue.Of(gate.Kind).Lanes];
            for (var i = 0; i < lanes.Length; i++)
            {
                lanes[i] = new List<Party>();
            }

            _queues.Add(lanes);
        }

        foreach (var party in _parties)
        {
            if (!party.IsOnSite)
            {
                continue;
            }

            var at = FieldMap.TileAt(party.X, party.Y);
            if (!_map.IsWalkable(at))
            {
                at = NearestWalkable(at);
                (party.X, party.Y) = FieldMap.CentreOf(at);
            }

            var outside = _site.SideOf(at) == Side.Outside && !_site.OpenWayIn;
            switch (party.State)
            {
                case PartyState.Queuing:
                    Arrive(party);
                    break;
                case PartyState.WalkingIn when outside:
                    party.Ticketed = null;
                    Arrive(party);
                    break;
                case PartyState.WalkingIn:
                case PartyState.Strolling:
                    Send(party, _standable.Contains(party.Target) ? party.Target : SomewhereInField(party.Random), party.State);
                    break;
                case PartyState.Leaving:
                    Send(party, LaneEnd(party), PartyState.Leaving);
                    break;
            }
        }
    }

    /// <summary>True if a tile and all eight round it can be walked on, so a party is not standing in a fence.</summary>
    private bool HasRoom(TilePoint p)
    {
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if (!_map.IsWalkable(p.X + dx, p.Y + dy))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private TilePoint NearestWalkable(TilePoint from)
    {
        for (var r = 1; r < 60; r++)
        {
            for (var dy = -r; dy <= r; dy++)
            {
                for (var dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r && _map.IsWalkable(from.X + dx, from.Y + dy))
                    {
                        return new TilePoint(from.X + dx, from.Y + dy);
                    }
                }
            }
        }

        return _map.LaneStart;
    }

    /// <summary>
    /// A party that is outside decides how to get in: straight in if nothing stops them, into the
    /// shortest queue if there is a gate, or waiting on the track if the way is shut.
    /// </summary>
    private void Arrive(Party party)
    {
        party.Gate = -1;
        party.ServiceLeft = -1;
        party.Moving = false;
        if (_site.OpenWayIn)
        {
            party.Ticketed = false;
            Send(party, SomewhereInField(party.Random), PartyState.WalkingIn);
            if (party.State == PartyState.WalkingIn)
            {
                return;
            }

            party.Ticketed = null;
        }

        var best = -1;
        var bestLane = 0;
        var bestLength = int.MaxValue;
        for (var g = 0; g < _queues.Count; g++)
        {
            if (!_site.GateUses[g].InUse)
            {
                continue;
            }

            for (var lane = 0; lane < _queues[g].Length; lane++)
            {
                if (_queues[g][lane].Count < bestLength)
                {
                    (best, bestLane, bestLength) = (g, lane, _queues[g][lane].Count);
                }
            }
        }

        party.State = PartyState.Queuing;
        if (best >= 0)
        {
            party.Gate = best;
            party.Lane = bestLane;
            _queues[best][bestLane].Add(party);
            var (x, y) = QueuePlace(best, bestLane, bestLength);
            party.Target = FieldMap.TileAt(x, y);
            party.Path = _paths.FindPath(FieldMap.TileAt(party.X, party.Y), party.Target);
            party.PathIndex = 0;
            party.Moving = party.Path is not null;
            return;
        }

        // No way in at all: wait on the track by the hedge and look again in a couple of minutes.
        var track = _map.Track;
        party.Target = new TilePoint(track.X + 1 + (party.Id * 7 % (track.Width - 2)), track.Y + 2 + (party.Id * 5 % (track.Height - 4)));
        party.Path = _paths.FindPath(FieldMap.TileAt(party.X, party.Y), party.Target);
        party.PathIndex = 0;
        party.ServiceLeft = 120;
        party.Moving = party.Path is not null;
    }

    /// <summary>Where the party in a given place of a queue stands, in metres. Long queues bunch up at the back.</summary>
    private (double X, double Y) QueuePlace(int gate, int lane, int place) =>
        _site.LaneMouth(gate, lane, 0.9 + (Math.Min(place, QueuePlaces - 1) * QueueSpacing));

    private void LeaveQueue(Party party)
    {
        if (party.Gate >= 0 && party.Gate < _queues.Count)
        {
            _queues[party.Gate][party.Lane].Remove(party);
        }

        party.Gate = -1;
        party.ServiceLeft = -1;
        party.Moving = false;
    }

    private void Queue(Party party)
    {
        if (party.Path is not null)
        {
            // Still walking to the back of the queue (or to the track).
            party.Moving = true;
            if (!Walk(party))
            {
                return;
            }
        }

        if (party.Gate < 0)
        {
            party.Moving = false;
            if (--party.ServiceLeft <= 0)
            {
                Arrive(party);
            }

            return;
        }

        var queue = _queues[party.Gate][party.Lane];
        var place = queue.IndexOf(party);
        var (x, y) = QueuePlace(party.Gate, party.Lane, place);
        var dx = x - party.X;
        var dy = y - party.Y;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        var use = _site.GateUses[party.Gate];
        if (distance > 0.05)
        {
            // Shuffle up.
            var step = Math.Min(distance, party.Speed);
            party.X += dx / distance * step;
            party.Y += dy / distance * step;
            party.Heading = Math.Atan2(dx, dy);
            party.Moving = true;
            return;
        }

        party.Moving = false;
        party.Heading = Math.Atan2(-use.OutX, -use.OutY);
        if (place != 0)
        {
            return;
        }

        // At the front: tickets are checked, one person at a time.
        if (party.ServiceLeft < 0)
        {
            party.ServiceLeft = Catalogue.Of(_site.Gates[party.Gate].Kind).SecondsPerPerson * party.Members.Count;
        }
        else if (--party.ServiceLeft <= 0)
        {
            LeaveQueue(party);
            party.Ticketed = true;
            Send(party, SomewhereInField(party.Random), PartyState.WalkingIn);
        }
    }

    /// <summary>Moves everyone on by one tick.</summary>
    public void Tick(DateTime now)
    {
        var closing = now.TimeOfDay >= EveryoneOut;
        foreach (var party in _parties)
        {
            switch (party.State)
            {
                case PartyState.Expected:
                    if (now >= party.Arrives && now.TimeOfDay < EveryoneOut && now < party.Leaves)
                    {
                        var start = LaneEnd(party);
                        (party.X, party.Y) = FieldMap.CentreOf(start);
                        party.Heading = party.FromWest ? Math.PI / 2 : -Math.PI / 2;
                        Arrive(party);
                    }

                    break;

                case PartyState.Queuing:
                    if (now >= party.Leaves || closing)
                    {
                        LeaveQueue(party);
                        Send(party, LaneEnd(party), PartyState.Leaving);
                    }
                    else
                    {
                        Queue(party);
                    }

                    break;

                case PartyState.Standing:
                    if (now < party.RetryAt)
                    {
                        break;
                    }

                    if (now >= party.Leaves || closing)
                    {
                        Send(party, LaneEnd(party), PartyState.Leaving);
                    }
                    else if (now >= party.WaitUntil)
                    {
                        Send(party, SomewhereInField(party.Random), PartyState.Strolling);
                    }

                    if (party.State == PartyState.Standing && (now >= party.Leaves || closing))
                    {
                        // No way out just now: look again in a couple of minutes.
                        party.RetryAt = now + TimeSpan.FromMinutes(2);
                    }

                    break;

                case PartyState.WalkingIn:
                case PartyState.Strolling:
                case PartyState.Leaving:
                    if (Walk(party))
                    {
                        if (party.State == PartyState.Leaving)
                        {
                            party.State = PartyState.Gone;
                        }
                        else
                        {
                            party.State = PartyState.Standing;
                            party.RetryAt = default;
                            party.WaitUntil = now + TimeSpan.FromMinutes(party.Random.Between(2, 12));
                            party.Heading += party.Random.Between(-1.2, 1.2);
                        }
                    }

                    break;
            }
        }
    }

    /// <summary>Sets a party walking somewhere. If there is no way there, they stand where they are.</summary>
    private void Send(Party party, TilePoint? target, PartyState state)
    {
        var from = FieldMap.TileAt(party.X, party.Y);
        party.Target = target ?? from;
        party.Path = target is null ? null : _paths.FindPath(from, target.Value);
        party.PathIndex = 0;
        party.State = party.Path is null ? PartyState.Standing : state;
    }

    /// <summary>Takes one tick's walk along the route. True once the party has got there.</summary>
    private static bool Walk(Party party)
    {
        var path = party.Path;
        if (path is null)
        {
            return true;
        }

        var left = party.Speed;
        while (left > 0 && party.PathIndex < path.Count)
        {
            var (wx, wy) = FieldMap.CentreOf(path[party.PathIndex]);
            var dx = wx - party.X;
            var dy = wy - party.Y;
            var distance = Math.Sqrt((dx * dx) + (dy * dy));
            if (distance <= left)
            {
                party.X = wx;
                party.Y = wy;
                left -= distance;
                party.PathIndex++;
            }
            else
            {
                party.X += dx / distance * left;
                party.Y += dy / distance * left;
                party.Heading = Math.Atan2(dx, dy);
                left = 0;
            }
        }

        if (party.PathIndex >= path.Count)
        {
            party.Path = null;
            return true;
        }

        return false;
    }

    public List<PartySave> Save() => _parties
        .Where(p => p.State != PartyState.Expected)
        .Select(p => new PartySave(p.Id, p.State, p.X, p.Y, p.Heading, p.Target.X, p.Target.Y, p.WaitUntil, p.Random.State, p.Path?.Skip(p.PathIndex).ToList(),
            p.Gate, p.Lane, p.Gate >= 0 && p.Gate < _queues.Count ? _queues[p.Gate][p.Lane].IndexOf(p) : 0, p.ServiceLeft, p.Ticketed))
        .ToList();

    /// <summary>Puts back parties from a save. Anyone not in the save has not arrived yet.</summary>
    public void Restore(IEnumerable<PartySave> saved)
    {
        foreach (var queue in _queues.SelectMany(q => q))
        {
            queue.Clear();
        }

        foreach (var s in saved.OrderBy(s => s.QueuePlace).ThenBy(s => s.Id))
        {
            if (s.Id < 0 || s.Id >= _parties.Count)
            {
                continue;
            }

            var party = _parties[s.Id];
            party.X = s.X;
            party.Y = s.Y;
            party.Heading = s.Heading;
            party.WaitUntil = s.WaitUntil;
            party.Random = new GameRandom(s.Random);
            party.State = s.State;
            party.Target = new TilePoint(s.TargetX, s.TargetY);
            party.Ticketed = s.Ticketed;
            party.Gate = -1;
            party.ServiceLeft = s.ServiceLeft;
            if (s.State == PartyState.Queuing)
            {
                if (s.Gate >= 0 && s.Gate < _queues.Count && s.Lane >= 0 && s.Lane < _queues[s.Gate].Length && _site.GateUses[s.Gate].InUse)
                {
                    party.Gate = s.Gate;
                    party.Lane = s.Lane;
                    _queues[s.Gate][s.Lane].Add(party);
                }
                else if (s.Gate >= 0)
                {
                    Arrive(party);
                    continue;
                }

                party.Path = s.Path is { Count: > 0 } && s.Path.All(_map.IsWalkable) ? s.Path.ToList() : null;
                party.PathIndex = 0;
                continue;
            }

            if (party.State is not (PartyState.WalkingIn or PartyState.Strolling or PartyState.Leaving))
            {
                continue;
            }

            if (s.Path is { Count: > 0 } && s.Path.All(_map.IsWalkable) && _map.IsWalkable(FieldMap.TileAt(s.X, s.Y)))
            {
                // Carry on along the very route they were on.
                party.Path = s.Path.ToList();
                party.PathIndex = 0;
            }
            else
            {
                if (!_map.IsWalkable(FieldMap.TileAt(s.X, s.Y)))
                {
                    (party.X, party.Y) = FieldMap.CentreOf(LaneEnd(party));
                }

                Send(party, party.Target, s.State);
            }
        }
    }
}

/// <summary>One party as written to a save file.</summary>
public sealed record PartySave(int Id, PartyState State, double X, double Y, double Heading, int TargetX, int TargetY, DateTime WaitUntil, ulong Random, List<TilePoint>? Path = null,
    int Gate = -1, int Lane = 0, int QueuePlace = 0, int ServiceLeft = -1, bool? Ticketed = null);
