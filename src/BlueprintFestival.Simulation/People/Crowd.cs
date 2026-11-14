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

    /// <summary>Taken ill or hurt, and staying put until someone helps.</summary>
    Unwell,

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

    /// <summary>The last set on the second stage this party made its mind up about, or -1.</summary>
    public int DecidedSecond { get; internal set; } = -1;

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

    /// <summary>True once the party has made up its mind whether to stay on for the headliner.</summary>
    public bool StayDecided { get; internal set; }

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
public readonly record struct PersonOnSite(int Id, PersonLook Look, double X, double Y, double Heading, bool Walking, bool Watching = false, Need? Wants = null, bool Unwell = false);

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

    /// <summary>From this time of day, people who were about to go home may stay on for the headliner instead.</summary>
    public static readonly TimeSpan StayOnFrom = TimeSpan.FromHours(17);

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

    /// <summary>How long people wait at a gate with nobody checking tickets before they go home.</summary>
    public const double GatePatienceMinutes = 30;
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
    private DateTime _now;

    /// <summary>
    /// How many lanes of a gate have someone checking tickets, by the gate's number. Left unset,
    /// every lane of every gate is open.
    /// </summary>
    public Func<int, int>? OpenLanes { get; set; }

    /// <summary>How crowded every part of the map is, measured again each minute.</summary>
    public DensityMap Density { get; }

    /// <summary>
    /// True for a place (in metres) where security are keeping the crowd in hand. People looking
    /// for somewhere to watch are turned away from such a place once it is busy.
    /// </summary>
    public Func<double, double, bool>? Stewarded { get; set; }

    /// <summary>How far in front of a stage its front barrier stands, in metres, or 0 if it has none.</summary>
    public Func<PlacedStage, double>? BarrierDepth { get; set; }

    /// <summary>People per square metre at which security stop letting more people into a place.</summary>
    public const double StewardLimit = 3.0;

    /// <summary>People per square metre at which nobody else can physically squeeze in.</summary>
    public const double PackedLimit = 6.5;

    /// <summary>How far back from the front of the crowd people will still squeeze in tight for a big name, in metres.</summary>
    public const double FrontDepth = 16;

    /// <summary>How hard it is raining, from 0 (dry) to 1 (pouring).</summary>
    public double Rain { get; set; }

    /// <summary>How much faster than usual people get thirsty: 1 on an ordinary day, more in the heat.</summary>
    public double Heat { get; set; } = 1;

    /// <summary>How muddy the ground is at a place in metres, from 0 to 1. Left unset, the ground is firm.</summary>
    public Func<double, double, double>? Mud { get; set; }

    /// <summary>Whether people have dressed for a wet day: wellies, and a rain poncho for about half of them.</summary>
    public bool DressedForRain { get; set; }

    /// <summary>Whether the festival hands everyone a rain poncho at the gate on a wet day.</summary>
    public bool FreePonchos { get; set; }

    /// <summary>Mood lost in an hour of pouring rain by someone with no poncho, and by someone with one.</summary>
    public const double SoakedPerHour = 0.07;

    public const double PonchoPerHour = 0.015;

    /// <summary>Mood lost in an hour of standing in deep mud.</summary>
    public const double MudPerHour = 0.06;

    private int[] _claims = Array.Empty<int>();
    private int _claimSlot = -1;
    private int[] _claimsSecond = Array.Empty<int>();
    private int _claimSlotSecond = -1;
    private int _measuredMinute = -1;

    public Crowd(FieldMap map, int seed, DateTime festivalDay)
        : this(map, new SiteLayout(map), seed, festivalDay)
    {
    }

    public Crowd(FieldMap map, SiteLayout site, int seed, DateTime festivalDay)
    {
        _map = map;
        _site = site;
        _paths = new PathFinder(map);
        Density = new DensityMap(map);
        _claims = new int[Density.Across * Density.Down];
        _claimsSecond = new int[_claims.Length];
        Seed = seed;
        _day = festivalDay.Date;
        Plan(_day, null);
        SiteChanged();
        site.Changed += SiteChanged;
    }

    /// <summary>The day's music, if there is any. People go and watch the acts they fancy.</summary>
    public RunningOrder? Show { get; set; }

    /// <summary>The second stage's line-up, if there is one.</summary>
    public RunningOrder? Second { get; set; }

    /// <summary>The line-up a set belongs to, by the set's number.</summary>
    private RunningOrder? ShowOf(int set) => set >= RunningOrder.SecondBase ? Second : Show;

    /// <summary>True while the set a party went to watch is still on.</summary>
    private bool StillOn(int set, DateTime now) => set >= 0 && ShowOf(set)?.Playing(now)?.Index == set;

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
                        ReferenceEquals(m, party.Members[0]) && party.State != PartyState.Unwell ? party.Wants : null,
                        party.State == PartyState.Unwell && ReferenceEquals(m, party.Members[0]));
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
                var look = LookGenerator.FestivalGoer(lookSeed, DressedForRain);

                // A child never comes alone: the first of any party is at least a teenager.
                if (i == 0 && look.Age == AgeGroup.Child)
                {
                    look = LookGenerator.FestivalGoer(lookSeed, AgeGroup.Adult, DressedForRain);
                }

                if (DressedForRain && FreePonchos && look.Poncho is null)
                {
                    look = look with { Poncho = LookGenerator.PonchoColours[personId % LookGenerator.PonchoColours.Length] };
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
        // People spread across the whole width of the lane as they go.
        var y = _map.Lane.Y + (party.Id % _map.Lane.Height);
        return new TilePoint(party.FromWest ? 0 : _map.Width - 1, y);
    }

    /// <summary>The part of a gate a party inside the fence walks out through, or null if there is no gate to use.</summary>
    private TilePoint? WayOut(Party party, TilePoint from)
    {
        if (!_site.HasWorkingGate || _site.SideOf(from) != Side.Inside)
        {
            return null;
        }

        var wide = 0;
        for (var i = 0; i < _site.Gates.Count; i++)
        {
            wide += _site.GateUses[i].InUse ? Math.Max(_site.Gates[i].Rect.Width, _site.Gates[i].Rect.Height) : 0;
        }

        var pick = (int)((uint)(party.Id * 7919) % (uint)Math.Max(1, wide));
        for (var i = 0; i < _site.Gates.Count; i++)
        {
            if (!_site.GateUses[i].InUse)
            {
                continue;
            }

            var rect = _site.Gates[i].Rect;
            var across = Math.Max(rect.Width, rect.Height);
            if (pick < across)
            {
                var tile = rect.Width >= rect.Height ? new TilePoint(rect.X + pick, rect.Y) : new TilePoint(rect.X, rect.Y + pick);
                return _map.IsWalkable(tile) ? tile : null;
            }

            pick -= across;
        }

        return null;
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
                        party.Decided = party.DecidedSecond = -1;
                    }

                    Send(party, _standableSet.Contains(party.Target) ? party.Target : SomewhereInField(party.Random), party.State);
                    break;
                case PartyState.Leaving:
                    Send(party, LaneEnd(party), PartyState.Leaving);
                    break;
                case PartyState.Watching:
                    // The stage may have moved or gone: think again.
                    party.Watch = -1;
                    party.Decided = party.DecidedSecond = -1;
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

            // People join a lane with someone checking tickets; if nobody is, they wait at the first.
            for (var lane = 0; lane < Math.Max(1, Math.Min(_queues[g].Length, OpenLanes?.Invoke(g) ?? int.MaxValue)); lane++)
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
            party.QueuedSince = _now;
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

        if (OpenLanes is not null && party.Lane >= OpenLanes(party.Gate))
        {
            // Nobody is on this lane to check tickets. People will not stand here all day.
            if (_now - party.QueuedSince > TimeSpan.FromMinutes(GatePatienceMinutes))
            {
                LeaveQueue(party);
                GoHome(party, unhappy: true);
            }

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
        _now = now;
        if (now.Minute != _measuredMinute)
        {
            _measuredMinute = now.Minute;
            Density.Measure(Loose());
        }

        var closing = now.TimeOfDay >= EveryoneOut;
        var headliner = Show?.Stage is null || Show.CalledOff is not null ? null : Show.Slots.FirstOrDefault(s => s.Headliner);
        foreach (var party in _parties)
        {
            if (party.State is PartyState.Standing or PartyState.Strolling or PartyState.Watching or PartyState.AtStall)
            {
                Feel(party, now);
            }

            if (!party.StayDecided && now >= party.Leaves && party.IsOnSite && party.State != PartyState.Leaving)
            {
                // Time to go home, unless the headliner is worth staying on for.
                party.StayDecided = true;
                if (headliner is not null && now < headliner.End && now.TimeOfDay >= StayOnFrom && party.Mood >= 0.3 && party.Random.NextDouble() < headliner.Act.Draw)
                {
                    // A big crowd takes longer to drift away at the end of the night.
                    party.Leaves = headliner.End + TimeSpan.FromMinutes(party.Random.Between(1, 14 + Math.Min(20, _parties.Count / 80.0)));
                }
            }

            if (party.LitterAt != default && now >= party.LitterAt && party.IsOnSite)
            {
                // Anyone already on their way out takes it home with them.
                var pieces = _map.Field.Contains(FieldMap.TileAt(party.X, party.Y)) ? Math.Max(1, party.Members.Count / 2) : 0;
                for (var i = 0; i < pieces; i++)
                {
                    var angle = (party.Id * 2.4) + (party.Spent * 0.7) + (i * 2.1);
                    var (lx, ly) = (party.X + (Math.Cos(angle) * 0.6), party.Y + (Math.Sin(angle) * 0.6));
                    if (_map.Field.Contains(FieldMap.TileAt(lx, ly)))
                    {
                        Dropped?.Invoke(lx, ly);
                    }
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

                case PartyState.Unwell:
                    if (closing)
                    {
                        // Nobody is left lying in the field at the end of the night.
                        GoHome(party, unhappy: true);
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
                    else if (!StillOn(party.Watch, now))
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
                    if (Walk(party, Pace(party)))
                    {
                        if (party.State == PartyState.Leaving)
                        {
                            party.State = PartyState.Gone;
                        }
                        else if (StillOn(party.Watch, now) && ShowOf(party.Watch)?.Stage is { } stage)
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
        party.Thirst = Math.Min(1.2, party.Thirst + (party.Appetite * Heat * perHour / HoursToThirsty));
        party.Toilet = Math.Min(1.2, party.Toilet + (party.Appetite * perHour / HoursToToilet));

        var change = party.State == PartyState.Watching ? 0.05 * perHour : 0;
        foreach (var need in new[] { party.Hunger, party.Thirst, party.Toilet })
        {
            if (need >= Pressing)
            {
                change -= (need >= 1 ? 0.06 : 0.03) * perHour;
            }
        }

        if (Rain > 0.05)
        {
            // Rain is miserable without a poncho and only a nuisance with one.
            var covered = (double)party.Members.Count(m => m.Look.Poncho is not null) / party.Members.Count;
            change -= Rain * ((covered * PonchoPerHour) + ((1 - covered) * SoakedPerHour)) * perHour;
        }

        if (Mud is not null && party.State is not (PartyState.Queuing or PartyState.AtStall))
        {
            change -= Mud(party.X, party.Y) * MudPerHour * perHour;
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

        // People only wait so long once they have joined the queue, unless they are already being served.
        if (party.Path is null && party.ServiceLeft < 0 && now - party.QueuedSince > TimeSpan.FromMinutes(PatienceMinutes))
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

            // The queue forms in the order people get there, so nobody waits behind someone who
            // is still on their way across the field.
            queue.Remove(party);
            var behind = queue.FindIndex(p => p.Path is not null);
            queue.Insert(behind < 0 ? queue.Count : behind, party);
            place = queue.IndexOf(party);
            party.QueuedSince = now;
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
    private bool GoAndWatch(Party party, DateTime now) => GoAndWatch(party, now, Show, false) || GoAndWatch(party, now, Second, true);

    private bool GoAndWatch(Party party, DateTime now, RunningOrder? show, bool second)
    {
        if (show?.Playing(now) is not { } slot || show.Stage is not { } stage || (second ? party.DecidedSecond : party.Decided) == slot.Index)
        {
            return false;
        }

        if (second)
        {
            party.DecidedSecond = slot.Index;
            if (slot.Index != _claimSlotSecond)
            {
                _claimSlotSecond = slot.Index;
                Array.Clear(_claimsSecond);
            }
        }
        else
        {
            party.Decided = slot.Index;
            if (slot.Index != _claimSlot)
            {
                // A new act: everyone who wants to watch finds a place afresh.
                _claimSlot = slot.Index;
                Array.Clear(_claims);
            }
        }

        if (party.Random.NextDouble() >= slot.Act.Draw || PlaceToWatch(stage, party.Random, slot.Act.Draw, party.Members.Count) is not { } spot)
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

        Claim(spot, party.Members.Count, second);

        return true;
    }

    /// <summary>
    /// Where everyone stands who is free to push and be pushed. People in a queue at a gate or a
    /// stall are in an orderly line and are left out, as is anyone lying ill.
    /// </summary>
    private IEnumerable<(double X, double Y)> Loose()
    {
        foreach (var party in _parties)
        {
            if (!party.IsOnSite || party.State is PartyState.Queuing or PartyState.AtStall or PartyState.Unwell)
            {
                continue;
            }

            // Only the field and the way in are measured. Out on the lane people have room to spread onto the verges.
            if (!_map.HedgeLine.Contains(FieldMap.TileAt(party.X, party.Y)))
            {
                continue;
            }

            foreach (var m in party.Members)
            {
                yield return (party.X + m.OffsetX, party.Y + m.OffsetY);
            }
        }
    }

    /// <summary>How fast a party gets through where it is: a busy place is slow going and a packed one slower still.</summary>
    private double Pace(Party party) => (1 - (0.5 * (Mud?.Invoke(party.X, party.Y) ?? 0))) * (Density.StatusAt(party.X, party.Y) switch
    {
        Crowding.CrushRisk => 0.45,
        Crowding.Busy => 0.75,
        _ => 1.0,
    });

    /// <summary>
    /// How far back from a stage its audience spreads, in metres. The more people the act draws,
    /// the more of them press towards the front instead of standing back.
    /// </summary>
    public double AudienceReach(double draw) => 1.3 * Math.Sqrt(Math.Max(30, OnSite)) * Math.Clamp(1.45 - (draw * 1.25), 0.2, 1.2);

    /// <summary>
    /// How tightly people will pack themselves in to see an act, in people per square metre. For
    /// most acts they keep a comfortable distance; for a big name they squeeze in until it hurts.
    /// </summary>
    public static double Squeeze(double draw)
    {
        var keen = Math.Max(0, draw - 0.5) / 0.45;
        return Math.Min(PackedLimit, 2.0 + (4.5 * keen * keen));
    }

    /// <summary>People per square metre who have picked a place to watch from in the square at a place in metres.</summary>
    private double Claimed(double x, double y, int joining)
    {
        var (sx, sy) = Density.SquareOf(x, y);
        var square = (sy * Density.Across) + sx;
        return (_claims[square] + _claimsSecond[square] + joining) / (DensityMap.SquareMetres * DensityMap.SquareMetres);
    }

    /// <summary>The places people have picked to watch from, as written to a save file.</summary>
    public WatchingPlaces SavePlaces() => new(_claimSlot, Sparse(_claims), _claimSlotSecond, Sparse(_claimsSecond));

    public void RestorePlaces(WatchingPlaces? places)
    {
        Array.Clear(_claims);
        Array.Clear(_claimsSecond);
        (_claimSlot, _claimSlotSecond) = (places?.Slot ?? -1, places?.SlotSecond ?? -1);
        foreach (var (square, people) in places?.Claims ?? new())
        {
            if (square >= 0 && square < _claims.Length)
            {
                _claims[square] = people;
            }
        }

        foreach (var (square, people) in places?.ClaimsSecond ?? new())
        {
            if (square >= 0 && square < _claimsSecond.Length)
            {
                _claimsSecond[square] = people;
            }
        }
    }

    private static Dictionary<int, int> Sparse(int[] claims)
    {
        var kept = new Dictionary<int, int>();
        for (var i = 0; i < claims.Length; i++)
        {
            if (claims[i] != 0)
            {
                kept[i] = claims[i];
            }
        }

        return kept;
    }

    /// <summary>Notes that people have picked a place to watch from, for the main stage's act or the second stage's.</summary>
    private void Claim(TilePoint tile, int people, bool second)
    {
        var (x, y) = FieldMap.CentreOf(tile);
        var (sx, sy) = Density.SquareOf(x, y);
        (second ? _claimsSecond : _claims)[(sy * Density.Across) + sx] += people;
    }

    /// <summary>
    /// A place in front of a stage: close in more often than far back, in a fan that widens with
    /// distance. A big draw pulls the fan in tight. Nobody can squeeze into a square that is packed
    /// solid, and security turn people away from one that is already busy.
    /// </summary>
    private TilePoint? PlaceToWatch(PlacedStage stage, GameRandom random, double draw, int people, bool standBack = false)
    {
        var (fx, fy, dx, dy) = RunningOrder.Front(stage);
        var rect = stage.Rect;
        var half = (dx == 0 ? rect.Width : rect.Height) * FieldMap.TileMetres / 2;
        var reach = AudienceReach(draw);
        var barrier = BarrierDepth?.Invoke(stage) ?? 0;
        var nearest = barrier > 0 ? barrier + 0.6 : 1.2;
        for (var attempt = 0; attempt < 44; attempt++)
        {
            // Turned away from the front, people try further and further back.
            var back = standBack ? 0.8 + (attempt * 0.05) : attempt < 20 ? 0 : (attempt - 19) * 0.12;
            var deep = nearest + ((back + Math.Pow(random.NextDouble(), 1.3)) * reach);
            var across = random.Between(-1, 1) * ((half * 0.6) + (deep * 0.55));
            var tile = FieldMap.TileAt(fx + (dx * deep) - (dy * across), fy + (dy * deep) + (dx * across));
            if (!_standableSet.Contains(tile))
            {
                continue;
            }

            var (x, y) = FieldMap.CentreOf(tile);
            // People only squeeze in to be near the front. Further back they stop where it is merely busy.
            var keen = deep - nearest <= FrontDepth ? Squeeze(draw) : Math.Min(Squeeze(draw), StewardLimit);
            var limit = standBack ? DensityMap.Busy : Math.Min(keen, Stewarded?.Invoke(x, y) == true ? StewardLimit : PackedLimit);
            if (Claimed(x, y, people) <= limit)
            {
                return tile;
            }
        }

        return null;
    }

    /// <summary>
    /// Eases a packed crowd: everyone watching from a busy square moves further back. Returns how
    /// many people moved.
    /// </summary>
    public int Ease() => Ease(Show, false) + Ease(Second, true);

    private int Ease(RunningOrder? show, bool second)
    {
        if (show?.Playing(_now) is not { } slot || show.Stage is not { } stage)
        {
            return 0;
        }

        Array.Clear(second ? _claimsSecond : _claims);
        var moved = 0;
        foreach (var party in _parties)
        {
            if (party.State != PartyState.Watching || party.Watch != slot.Index || Density.At(party.X, party.Y) < DensityMap.Busy)
            {
                continue;
            }

            if (PlaceToWatch(stage, party.Random, slot.Act.Draw, party.Members.Count, standBack: true) is { } spot)
            {
                var watching = party.Watch;
                Send(party, spot, PartyState.Strolling);
                party.Watch = party.State == PartyState.Strolling ? watching : -1;
                Claim(spot, party.Members.Count, second);
                moved += party.Members.Count;
            }
        }

        return moved;
    }

    /// <summary>Moves one party out of the crush to somewhere further back, as security do when they lift someone over the barrier.</summary>
    public void MoveBack(Party party)
    {
        if ((ShowOf(party.Watch)?.Stage ?? Show?.Stage) is not { } stage || PlaceToWatch(stage, party.Random, 0.3, party.Members.Count, standBack: true) is not { } spot)
        {
            return;
        }

        party.Watch = -1;
        Send(party, spot, PartyState.Strolling);
    }

    /// <summary>
    /// The day is called off: everyone still here heads for the gate over the next few minutes,
    /// thinking less of the festival than they did.
    /// </summary>
    public void SendEveryoneHome(DateTime now, double moodLost)
    {
        foreach (var party in _parties)
        {
            if (party.State is PartyState.Gone or PartyState.Leaving)
            {
                continue;
            }

            party.Mood = Math.Max(0, party.Mood - moodLost);
            var goes = now + TimeSpan.FromMinutes(2 + ((party.Id * 7) % 18));
            if (party.Leaves > goes)
            {
                party.Leaves = goes;
            }
        }
    }

    /// <summary>Someone in the party is taken ill or hurt. They stop where they are until helped.</summary>
    public void TakeIll(Party party)
    {
        party.State = PartyState.Unwell;
        party.Watch = -1;
        party.Path = null;
        party.Moving = false;
    }

    /// <summary>Moves a party that is unwell, as a first-aider walks them somewhere.</summary>
    public void Carry(Party party, double x, double y)
    {
        if (party.State == PartyState.Unwell)
        {
            (party.X, party.Y) = (x, y);
        }
    }

    /// <summary>They are better, and go back to their day a little happier for being looked after.</summary>
    public void Recover(Party party)
    {
        if (party.State == PartyState.Unwell)
        {
            party.State = PartyState.Standing;
            party.RetryAt = default;
            party.WaitUntil = _now + TimeSpan.FromMinutes(2);
            party.Mood = Math.Min(1, party.Mood + 0.04);
            if (!_map.IsWalkable(FieldMap.TileAt(party.X, party.Y)))
            {
                (party.X, party.Y) = FieldMap.CentreOf(NearestWalkable(FieldMap.TileAt(party.X, party.Y)));
            }
        }
    }

    /// <summary>Sends a party home now. An unhappy one counts against the festival.</summary>
    public void GoHome(Party party, bool unhappy)
    {
        if (!party.IsOnSite)
        {
            return;
        }

        if (unhappy)
        {
            party.LeftUnhappy = true;
            party.Mood = Math.Min(party.Mood, 0.1);
        }

        party.Leaves = _now;
        party.Watch = -1;
        if (!_map.IsWalkable(FieldMap.TileAt(party.X, party.Y)))
        {
            (party.X, party.Y) = FieldMap.CentreOf(NearestWalkable(FieldMap.TileAt(party.X, party.Y)));
        }

        Send(party, LaneEnd(party), PartyState.Leaving);
        if (party.State != PartyState.Leaving)
        {
            // No way out just now: stand and try again as anyone else would.
            party.State = PartyState.Standing;
        }
    }

    /// <summary>Everyone within so many metres of a spot is put out by what happened there.</summary>
    public void Upset(double x, double y, double metres, double amount)
    {
        foreach (var party in _parties)
        {
            if (party.IsOnSite && ((party.X - x) * (party.X - x)) + ((party.Y - y) * (party.Y - y)) <= metres * metres)
            {
                party.Mood = Math.Max(0, party.Mood - amount);
            }
        }
    }

    /// <summary>Sets a party walking somewhere. If there is no way there, they stand where they are.</summary>
    private void Send(Party party, TilePoint? target, PartyState state)
    {
        var from = FieldMap.TileAt(party.X, party.Y);
        party.Target = target ?? from;
        if (state == PartyState.Leaving && target is not null && WayOut(party, from) is { } gate
            && _paths.FindPath(from, gate) is { } toGate && _paths.FindPath(gate, target.Value) is { } onward)
        {
            // Going home, people use the whole width of every gate instead of all cutting the same corner.
            toGate.AddRange(onward);
            party.Path = toGate;
        }
        else
        {
            party.Path = target is null ? null : _paths.FindPath(from, target.Value);
        }

        party.PathIndex = 0;
        party.State = party.Path is null ? PartyState.Standing : state;
    }

    /// <summary>Takes one tick's walk along the route. True once the party has got there.</summary>
    private static bool Walk(Party party, double pace = 1)
    {
        var path = party.Path;
        if (path is null)
        {
            return true;
        }

        var left = party.Speed * pace;
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
                QueuedSince = p.QueuedSince, NeedRetryAt = p.NeedRetryAt, GaveUp = p.GaveUp, Spent = p.Spent, LeftUnhappy = p.LeftUnhappy, Leaves = p.Leaves, LitterAt = p.LitterAt, DecidedSecond = p.DecidedSecond, StayDecided = p.StayDecided,
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
            party.DecidedSecond = s.DecidedSecond;
            party.Gate = -1;
            party.ServiceLeft = s.ServiceLeft;
            party.Stall = -1;
            party.GaveUp = s.GaveUp;
            party.Spent = s.Spent;
            party.LeftUnhappy = s.LeftUnhappy;
            party.LitterAt = s.LitterAt;
            party.StayDecided = s.StayDecided;
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

    public bool StayDecided { get; init; }

    /// <summary>The last set on the second stage the party made its mind up about, or -1.</summary>
    public int DecidedSecond { get; init; } = -1;

    public DateTime? Leaves { get; init; }
}

/// <summary>
/// How many people have picked each square of the crowd map to watch the act now on from, for
/// the main stage and the second stage, and which sets those are.
/// </summary>
public sealed record WatchingPlaces(int Slot, Dictionary<int, int>? Claims, int SlotSecond, Dictionary<int, int>? ClaimsSecond);
