using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Show;
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

    /// <summary>Standing in front of the stage watching an act.</summary>
    Watching,

    /// <summary>Queuing at a stall, or being served.</summary>
    AtStall,

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

    public DateTime Leaves { get; internal set; }

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

    public bool IsWalking => State is PartyState.WalkingIn or PartyState.Strolling or PartyState.Leaving || (State is PartyState.Queuing or PartyState.AtStall && Moving);

    /// <summary>Which gate they are queuing at, or -1.</summary>
    public int Gate { get; internal set; } = -1;

    /// <summary>Which lane of that gate.</summary>
    public int Lane { get; internal set; }

    /// <summary>How many more seconds the ticket check will take, or -1 if it has not begun.</summary>
    public int ServiceLeft { get; internal set; } = -1;

    /// <summary>True if they came in through a gate, false if they walked in unchecked, null if they are not in yet.</summary>
    public bool? Ticketed { get; internal set; }

    /// <summary>The set they are watching or walking to watch, or -1.</summary>
    public int Watch { get; internal set; } = -1;

    /// <summary>The last set they made their mind up about, so they only decide once for each.</summary>
    public int Decided { get; internal set; } = -1;

    /// <summary>How hungry they are, from 0 (just eaten) to 1 and a little over (starving).</summary>
    public double Hunger { get; internal set; }

    public double Thirst { get; internal set; }

    /// <summary>How much they need the toilet, from 0 to 1 and a little over.</summary>
    public double Toilet { get; internal set; }

    /// <summary>How the day is going for them, from 0 (miserable) to 1 (delighted).</summary>
    public double Mood { get; internal set; } = Crowd.StartingMood;

    /// <summary>Which stall they are queuing at, or -1.</summary>
    public int Stall { get; internal set; } = -1;

    public int StallLane { get; internal set; }

    /// <summary>When they joined the stall queue.</summary>
    public DateTime QueuedSince { get; internal set; }

    /// <summary>They will not look for a stall again before this time.</summary>
    public DateTime NeedRetryAt { get; internal set; }

    /// <summary>How many times they have walked away from a queue that was too long.</summary>
    public int GaveUp { get; internal set; }

    /// <summary>What the party has spent at stalls today, in pounds.</summary>
    public int Spent { get; internal set; }

    /// <summary>True if they went home early because the day was miserable.</summary>
    public bool LeftUnhappy { get; internal set; }

    /// <summary>When they will drop the rubbish they are holding, if they are holding any.</summary>
    public DateTime LitterAt { get; internal set; }

    /// <summary>How fast their needs grow compared with most people.</summary>
    internal double Appetite { get; set; } = 1;

    public double Level(Need need) => need switch { Need.Food => Hunger, Need.Drink => Thirst, _ => Toilet };

    /// <summary>What they most want right now, if anything is pressing.</summary>
    public Need? Wants
    {
        get
        {
            var need = Toilet >= Thirst && Toilet >= Hunger ? Need.Toilet : Thirst >= Hunger ? Need.Drink : Need.Food;
            return Level(need) >= Crowd.Pressing ? need : null;
        }
    }

    internal bool Moving { get; set; }

    internal DateTime RetryAt { get; set; }

    public bool HasChild => Members.Any(m => m.Look.Age == AgeGroup.Child);

    internal GameRandom Random { get; set; }

    internal List<TilePoint>? Path { get; set; }

    internal int PathIndex { get; set; }
}

/// <summary>One person as the game draws them: who they are, where they are, which way they face.</summary>
public readonly record struct PersonOnSite(int Id, PersonLook Look, double X, double Y, double Heading, bool Walking, bool Watching = false, Need? Wants = null);

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

    /// <summary>How people feel when they walk in.</summary>
    public const double StartingMood = 0.65;

    /// <summary>The level at which a need sends someone to look for a stall, and the level at which it shows and starts to spoil their day.</summary>
    public const double Seeking = 0.6;
    public const double Pressing = 0.85;

    /// <summary>How long a need takes to go from nothing to desperate, in hours.</summary>
    private const double HoursToHungry = 7;
    private const double HoursToThirsty = 3.5;
    private const double HoursToToilet = 4.5;

    /// <summary>How long people will stand in a stall queue before walking away, in minutes.</summary>
    public const double PatienceMinutes = 10;
    private const int QueuePlaces = 16;

    private readonly FieldMap _map;
    private readonly SiteLayout _site;
    private readonly PathFinder _paths;
    private readonly List<Party> _parties = new();
    private readonly List<List<Party>[]> _queues = new();
    private readonly List<List<Party>[]> _stallQueues = new();
    private readonly List<TilePoint> _standable = new();
    private readonly HashSet<TilePoint> _standableSet = new();
    private readonly DateTime _day;

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
        _day = festivalDay.Date;
        Plan(_day, null);
        SiteChanged();
        site.Changed += SiteChanged;
    }

    /// <summary>The day's music, if there is any. People go and watch the acts they fancy.</summary>
    public RunningOrder? Show { get; set; }

    /// <summary>How many people are standing watching the act that is on.</summary>
    public int Watching => _parties.Where(p => p.State == PartyState.Watching).Sum(p => p.Members.Count);

    /// <summary>How many people are queuing at gates or waiting outside.</summary>
    public int Queuing => _parties.Where(p => p.State == PartyState.Queuing).Sum(p => p.Members.Count);

    /// <summary>How many people have had their tickets checked at a gate today.</summary>
    public int ThroughGates => _parties.Where(p => p.Ticketed == true).Sum(p => p.Members.Count);

    /// <summary>How many people have walked in today with nobody checking tickets.</summary>
    public int WalkedInFree => _parties.Where(p => p.Ticketed == false).Sum(p => p.Members.Count);

    /// <summary>Raised when people pay at a gate: how many of them.</summary>
    public event Action<int>? TicketsSold;

    /// <summary>Raised when a stall serves a party: which kind of stall, how many people, and what they paid.</summary>
    public event Action<StallKind, int, int>? Served;

    /// <summary>Raised when someone drops a piece of litter: where, in metres.</summary>
    public event Action<double, double>? Dropped;

    /// <summary>Whether a party is the sort to drop its rubbish. About half are, always the same ones.</summary>
    public bool Untidy(Party party) => (unchecked((uint)((Seed * 131) + (party.Id * 7919))) % 100) < 55;

    /// <summary>How many people are queuing at stalls.</summary>
    public int AtStalls => _parties.Where(p => p.State == PartyState.AtStall).Sum(p => p.Members.Count);

    /// <summary>How many parties are in one line of a stall's queue.</summary>
    public int StallQueueLength(int stall, int lane) => stall < _stallQueues.Count && lane < _stallQueues[stall].Length ? _stallQueues[stall][lane].Count : 0;

    /// <summary>How many people have gone home early because their day was miserable.</summary>
    public int WentHomeUnhappy => _parties.Where(p => p.LeftUnhappy).Sum(p => p.Members.Count);

    /// <summary>How many times someone has walked away from a stall queue today.</summary>
    public int GaveUpQueuing => _parties.Sum(p => p.GaveUp * p.Members.Count);

    /// <summary>How everyone who has come so far feels, from 0 to 100. 65 before anyone arrives.</summary>
    public int Happiness
    {
        get
        {
            var people = 0;
            var total = 0.0;
            foreach (var p in _parties)
            {
                if (p.State != PartyState.Expected)
                {
                    people += p.Members.Count;
                    total += p.Mood * p.Members.Count;
                }
            }

            return (int)Math.Round((people == 0 ? StartingMood : total / people) * 100);
        }
    }

    /// <summary>The share of people on site, from 0 to 100, for whom a need is not pressing.</summary>
    public int Content(Need need)
    {
        var people = 0;
        var fine = 0;
        foreach (var p in _parties)
        {
            if (p.IsOnSite && p.Ticketed is not null)
            {
                people += p.Members.Count;
                fine += p.Level(need) < Pressing ? p.Members.Count : 0;
            }
        }

        return people == 0 ? 100 : (int)Math.Round(100.0 * fine / people);
    }

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
                    yield return new PersonOnSite(m.Id, m.Look, party.X + m.OffsetX, party.Y + m.OffsetY, party.Heading, party.IsWalking, party.State == PartyState.Watching,
                        ReferenceEquals(m, party.Members[0]) ? party.Wants : null);
                }
            }
        }
    }

    /// <summary>
    /// Plans the day again for a set number of people. Only for before the gates open: everyone
    /// planned so far is forgotten.
    /// </summary>
    public void Replan(int people)
    {
        _parties.Clear();
        Expected = 0;
        Plan(_day, Math.Max(1, people));
        SiteChanged();
    }

    private void Plan(DateTime day, int? people)
    {
        var random = GameRandom.FromSeed(Seed, 1);
        var guess = 100 + random.Next(101);
        var target = people ?? guess;
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

            // Needs come from their own run of numbers, so adding them changes nothing else about the day.
            var needs = GameRandom.FromSeed(Seed, 5000 + _parties.Count);
            _parties.Add(new Party(_parties.Count, members, arrives, leaves, random.NextDouble() < 0.6, speed, GameRandom.FromSeed(Seed, 100 + _parties.Count))
            {
                Hunger = needs.Between(0.1, 0.5),
                Thirst = needs.Between(0.1, 0.4),
                Toilet = needs.Between(0, 0.3),
                Appetite = needs.Between(0.8, 1.25),
            });
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

        _standableSet.Clear();
        _standableSet.UnionWith(_standable);

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

        _stallQueues.Clear();
        foreach (var stall in _site.Stalls)
        {
            var lines = new List<Party>[Catalogue.Of(stall.Kind).Servers];
            for (var i = 0; i < lines.Length; i++)
            {
                lines[i] = new List<Party>();
            }

            _stallQueues.Add(lines);
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
                    if (party.Watch >= 0)
                    {
                        party.Watch = -1;
                        party.Decided = -1;
                    }

                    Send(party, _standableSet.Contains(party.Target) ? party.Target : SomewhereInField(party.Random), party.State);
                    break;
                case PartyState.Leaving:
                    Send(party, LaneEnd(party), PartyState.Leaving);
                    break;
                case PartyState.Watching:
                    // The stage may have moved or gone: think again.
                    party.Watch = -1;
                    party.Decided = -1;
                    party.State = PartyState.Standing;
                    break;
                case PartyState.AtStall:
                    // The stall may have moved or gone: step out of the queue and look again.
                    party.Stall = -1;
                    party.ServiceLeft = -1;
                    party.Moving = false;
                    party.Path = null;
                    party.State = PartyState.Standing;
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
            TicketsSold?.Invoke(party.Members.Count);
            Send(party, SomewhereInField(party.Random), PartyState.WalkingIn);
        }
    }

    /// <summary>Moves everyone on by one tick.</summary>
    public void Tick(DateTime now)
    {
        var closing = now.TimeOfDay >= EveryoneOut;
        foreach (var party in _parties)
        {
            if (party.State is PartyState.Standing or PartyState.Strolling or PartyState.Watching or PartyState.AtStall)
            {
                Feel(party, now);
            }

            if (party.LitterAt != default && now >= party.LitterAt && party.IsOnSite)
            {
                var pieces = Math.Max(1, party.Members.Count / 2);
                for (var i = 0; i < pieces; i++)
                {
                    var angle = (party.Id * 2.4) + (party.Spent * 0.7) + (i * 2.1);
                    Dropped?.Invoke(party.X + (Math.Cos(angle) * 0.6), party.Y + (Math.Sin(angle) * 0.6));
                }

                party.LitterAt = default;
            }

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

                case PartyState.AtStall:
                    if (now >= party.Leaves || closing)
                    {
                        LeaveStall(party);
                        Send(party, LaneEnd(party), PartyState.Leaving);
                    }
                    else
                    {
                        QueueAtStall(party, now);
                    }

                    break;

                case PartyState.Watching:
                    if (now >= party.Leaves || closing)
                    {
                        party.Watch = -1;
                        Send(party, LaneEnd(party), PartyState.Leaving);
                    }
                    else if (party.Wants is not null && SeekStall(party, now))
                    {
                        // Something cannot wait for the end of the set.
                        party.Watch = -1;
                    }
                    else if (Show?.Playing(now)?.Index != party.Watch)
                    {
                        // The set is over: hang about for a bit, then wander off.
                        party.Watch = -1;
                        party.State = PartyState.Standing;
                        party.WaitUntil = now + TimeSpan.FromMinutes(party.Random.Between(0.5, 4));
                    }

                    break;

                case PartyState.Standing:
                    if (now < party.RetryAt)
                    {
                        break;
                    }

                    if (now >= party.Leaves || closing)
                    {
                        party.Watch = -1;
                        Send(party, LaneEnd(party), PartyState.Leaving);
                    }
                    else if (SeekStall(party, now))
                    {
                        // Off to a stall.
                    }
                    else if (GoAndWatch(party, now))
                    {
                        // Off to the stage.
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
                        else if (party.Watch >= 0 && Show?.Playing(now) is { } on && on.Index == party.Watch && Show.Stage is { } stage)
                        {
                            // Got to the stage: stand and face it.
                            var (fx, fy, _, _) = RunningOrder.Front(stage);
                            party.State = PartyState.Watching;
                            party.Heading = Math.Atan2(fx - party.X, fy - party.Y) + party.Random.Between(-0.15, 0.15);
                        }
                        else
                        {
                            party.Watch = -1;
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

    /// <summary>
    /// One tick of getting hungrier, thirstier and needing the toilet, and of the day getting better
    /// or worse: a good act lifts the mood, a need left too long spoils it.
    /// </summary>
    private void Feel(Party party, DateTime now)
    {
        const double perHour = 1.0 / 3600;
        party.Hunger = Math.Min(1.2, party.Hunger + (party.Appetite * perHour / HoursToHungry));
        party.Thirst = Math.Min(1.2, party.Thirst + (party.Appetite * perHour / HoursToThirsty));
        party.Toilet = Math.Min(1.2, party.Toilet + (party.Appetite * perHour / HoursToToilet));

        var change = party.State == PartyState.Watching ? 0.05 * perHour : 0;
        foreach (var need in new[] { party.Hunger, party.Thirst, party.Toilet })
        {
            if (need >= Pressing)
            {
                change -= (need >= 1 ? 0.06 : 0.03) * perHour;
            }
        }

        party.Mood = Math.Clamp(party.Mood + change, 0, 1);
        if (party.Mood < 0.12 && !party.LeftUnhappy && now < party.Leaves)
        {
            // Enough is enough: they go home, and they will tell their friends.
            party.LeftUnhappy = true;
            party.Leaves = now;
        }
    }

    /// <summary>
    /// A party with a need that has grown enough looks for the stall that answers it with the
    /// shortest wait, and joins its queue. True if they set off.
    /// </summary>
    private bool SeekStall(Party party, DateTime now)
    {
        if (now < party.NeedRetryAt || _site.Stalls.Count == 0)
        {
            return false;
        }

        // The most pressing need that some stall here can answer.
        Need? wanted = null;
        foreach (var candidate in new[] { Need.Toilet, Need.Drink, Need.Food })
        {
            if (party.Level(candidate) >= Seeking && (wanted is null || party.Level(candidate) > party.Level(wanted.Value))
                && _site.Stalls.Any(s => Catalogue.Of(s.Kind).Serves == candidate))
            {
                wanted = candidate;
            }
        }

        if (wanted is not { } need)
        {
            return false;
        }

        var best = -1;
        var bestLane = 0;
        var bestScore = double.MaxValue;
        for (var i = 0; i < _site.Stalls.Count; i++)
        {
            var stall = _site.Stalls[i];
            var info = Catalogue.Of(stall.Kind);
            if (info.Serves != need)
            {
                continue;
            }

            for (var lane = 0; lane < info.Servers; lane++)
            {
                var (x, y) = SiteLayout.ServingPlace(stall, lane, 0.9);
                var walk = Math.Abs(x - party.X) + Math.Abs(y - party.Y);
                var score = (_stallQueues[i][lane].Count * info.SecondsPerPerson * 2.2) + (walk / party.Speed);
                if (score < bestScore)
                {
                    (best, bestLane, bestScore) = (i, lane, score);
                }
            }
        }

        if (best < 0)
        {
            return false;
        }

        var place = StallPlace(best, bestLane, _stallQueues[best][bestLane].Count);
        var path = _paths.FindPath(FieldMap.TileAt(party.X, party.Y), FieldMap.TileAt(place.X, place.Y));
        if (path is null)
        {
            // Cannot get there (fenced off, perhaps): try again in a while.
            party.NeedRetryAt = now + TimeSpan.FromMinutes(10);
            return false;
        }

        _stallQueues[best][bestLane].Add(party);
        party.Stall = best;
        party.StallLane = bestLane;
        party.ServiceLeft = -1;
        party.QueuedSince = now;
        party.Target = FieldMap.TileAt(place.X, place.Y);
        party.Path = path;
        party.PathIndex = 0;
        party.Moving = true;
        party.State = PartyState.AtStall;
        return true;
    }

    /// <summary>Where the party in a given place of a stall's queue stands, in metres.</summary>
    private (double X, double Y) StallPlace(int stall, int lane, int place) =>
        SiteLayout.ServingPlace(_site.Stalls[stall], lane, 0.9 + (Math.Min(place, QueuePlaces - 1) * QueueSpacing));

    private void LeaveStall(Party party)
    {
        if (party.Stall >= 0 && party.Stall < _stallQueues.Count)
        {
            _stallQueues[party.Stall][party.StallLane].Remove(party);
        }

        party.Stall = -1;
        party.ServiceLeft = -1;
        party.Moving = false;
        party.Path = null;
    }

    private void QueueAtStall(Party party, DateTime now)
    {
        if (party.Stall < 0 || party.Stall >= _stallQueues.Count)
        {
            LeaveStall(party);
            party.State = PartyState.Standing;
            return;
        }

        var stall = _site.Stalls[party.Stall];
        var info = Catalogue.Of(stall.Kind);
        var queue = _stallQueues[party.Stall][party.StallLane];
        var place = queue.IndexOf(party);

        // People only wait so long, unless they are already being served.
        if (party.ServiceLeft < 0 && now - party.QueuedSince > TimeSpan.FromMinutes(PatienceMinutes))
        {
            LeaveStall(party);
            party.GaveUp++;
            party.Mood = Math.Max(0, party.Mood - 0.08);
            party.NeedRetryAt = now + TimeSpan.FromMinutes(15);
            party.State = PartyState.Standing;
            party.WaitUntil = now;
            return;
        }

        if (party.Path is not null)
        {
            // Still walking to the back of the queue.
            party.Moving = true;
            if (!Walk(party))
            {
                return;
            }
        }

        var (x, y) = StallPlace(party.Stall, party.StallLane, place);
        var dx = x - party.X;
        var dy = y - party.Y;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        if (distance > 0.05)
        {
            var step = Math.Min(distance, party.Speed);
            party.X += dx / distance * step;
            party.Y += dy / distance * step;
            party.Heading = Math.Atan2(dx, dy);
            party.Moving = true;
            return;
        }

        party.Moving = false;
        var (_, _, fx, fy) = SiteLayout.Front(stall.Rect, stall.Facing);
        party.Heading = Math.Atan2(-fx, -fy);
        if (place != 0)
        {
            return;
        }

        if (party.ServiceLeft < 0)
        {
            party.ServiceLeft = info.SecondsPerPerson * party.Members.Count;
        }
        else if (--party.ServiceLeft <= 0)
        {
            LeaveStall(party);
            switch (info.Serves)
            {
                case Need.Food:
                    // A slice of cake is not a meal.
                    party.Hunger = stall.Kind == StallKind.TeaAndCake ? Math.Max(0, party.Hunger - 0.55) : 0;
                    break;
                case Need.Drink:
                    party.Thirst = 0;
                    party.Toilet = Math.Min(1.2, party.Toilet + 0.2);
                    break;
                default:
                    party.Toilet = 0;
                    break;
            }

            var paid = info.Price * party.Members.Count;
            party.Spent += paid;
            party.Mood = Math.Min(1, party.Mood + 0.012);
            party.State = PartyState.Standing;
            party.RetryAt = default;
            party.WaitUntil = now + TimeSpan.FromMinutes(party.Random.Between(1, 5));
            if (info.Price > 0 && info.Serves != Need.Toilet && Untidy(party))
            {
                // The wrapper or the cup lasts a few minutes, then some people just let go of it.
                party.LitterAt = now + TimeSpan.FromMinutes(4 + ((party.Id + party.Spent) % 14));
            }

            Served?.Invoke(stall.Kind, party.Members.Count, paid);
        }
    }

    /// <summary>
    /// When a set is on, a party that has not yet made up its mind about it decides whether to go
    /// and watch. True if they set off for the stage.
    /// </summary>
    private bool GoAndWatch(Party party, DateTime now)
    {
        if (Show?.Playing(now) is not { } slot || Show.Stage is not { } stage || party.Decided == slot.Index)
        {
            return false;
        }

        party.Decided = slot.Index;
        if (party.Random.NextDouble() >= slot.Act.Draw || PlaceToWatch(stage, party.Random) is not { } spot)
        {
            return false;
        }

        party.Watch = slot.Index;
        Send(party, spot, PartyState.Strolling);
        if (party.State != PartyState.Strolling)
        {
            party.Watch = -1;
            return false;
        }

        return true;
    }

    /// <summary>A place in front of a stage: close in more often than far back, in a fan that widens with distance.</summary>
    private TilePoint? PlaceToWatch(PlacedStage stage, GameRandom random)
    {
        var (fx, fy, dx, dy) = RunningOrder.Front(stage);
        var rect = stage.Rect;
        var half = (dx == 0 ? rect.Width : rect.Height) * FieldMap.TileMetres / 2;
        var reach = 5 + (Expected / 14.0);
        for (var attempt = 0; attempt < 24; attempt++)
        {
            var deep = 1.2 + (Math.Pow(random.NextDouble(), 1.3) * reach);
            var across = random.Between(-1, 1) * ((half * 0.6) + (deep * 0.55));
            var tile = FieldMap.TileAt(fx + (dx * deep) - (dy * across), fy + (dy * deep) + (dx * across));
            if (_standableSet.Contains(tile))
            {
                return tile;
            }
        }

        return null;
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
            p.Gate, p.Lane, p.Gate >= 0 && p.Gate < _queues.Count ? _queues[p.Gate][p.Lane].IndexOf(p) : 0, p.ServiceLeft, p.Ticketed, p.Watch, p.Decided)
            {
                Hunger = p.Hunger, Thirst = p.Thirst, Toilet = p.Toilet, Mood = p.Mood, Stall = p.Stall, StallLane = p.StallLane,
                StallPlace = p.Stall >= 0 && p.Stall < _stallQueues.Count ? _stallQueues[p.Stall][p.StallLane].IndexOf(p) : 0,
                QueuedSince = p.QueuedSince, NeedRetryAt = p.NeedRetryAt, GaveUp = p.GaveUp, Spent = p.Spent, LeftUnhappy = p.LeftUnhappy, Leaves = p.Leaves, LitterAt = p.LitterAt,
            })
        .ToList();

    /// <summary>Puts back parties from a save. Anyone not in the save has not arrived yet.</summary>
    public void Restore(IEnumerable<PartySave> saved)
    {
        foreach (var queue in _queues.SelectMany(q => q))
        {
            queue.Clear();
        }

        foreach (var queue in _stallQueues.SelectMany(q => q))
        {
            queue.Clear();
        }

        foreach (var s in saved.OrderBy(s => s.QueuePlace).ThenBy(s => s.StallPlace).ThenBy(s => s.Id))
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
            party.Watch = s.Watch;
            party.Decided = s.Decided;
            party.Gate = -1;
            party.ServiceLeft = s.ServiceLeft;
            party.Stall = -1;
            party.GaveUp = s.GaveUp;
            party.Spent = s.Spent;
            party.LeftUnhappy = s.LeftUnhappy;
            party.LitterAt = s.LitterAt;
            party.NeedRetryAt = s.NeedRetryAt;
            if (s.Mood is { } mood)
            {
                // Saves from before step 5 have no needs: those parties keep the ones they were planned with.
                party.Hunger = s.Hunger;
                party.Thirst = s.Thirst;
                party.Toilet = s.Toilet;
                party.Mood = mood;
            }

            if (s.Leaves is { } leaves)
            {
                party.Leaves = leaves;
            }

            if (s.State == PartyState.AtStall)
            {
                if (s.Stall >= 0 && s.Stall < _stallQueues.Count && s.StallLane >= 0 && s.StallLane < _stallQueues[s.Stall].Length)
                {
                    party.Stall = s.Stall;
                    party.StallLane = s.StallLane;
                    party.QueuedSince = s.QueuedSince;
                    _stallQueues[s.Stall][s.StallLane].Add(party);
                    party.Path = s.Path is { Count: > 0 } && s.Path.All(_map.IsWalkable) ? s.Path.ToList() : null;
                    party.PathIndex = 0;
                }
                else
                {
                    party.ServiceLeft = -1;
                    party.State = PartyState.Standing;
                }

                continue;
            }

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
    int Gate = -1, int Lane = 0, int QueuePlace = 0, int ServiceLeft = -1, bool? Ticketed = null, int Watch = -1, int Decided = -1)
{
    public double Hunger { get; init; }

    public double Thirst { get; init; }

    public double Toilet { get; init; }

    public double? Mood { get; init; }

    public int Stall { get; init; } = -1;

    public int StallLane { get; init; }

    public int StallPlace { get; init; }

    public DateTime QueuedSince { get; init; }

    public DateTime NeedRetryAt { get; init; }

    public int GaveUp { get; init; }

    public int Spent { get; init; }

    public bool LeftUnhappy { get; init; }

    public DateTime LitterAt { get; init; }

    public DateTime? Leaves { get; init; }
}
