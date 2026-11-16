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

    /// <summary>Sitting down: on a bench, at a table, in a deckchair or on the grass.</summary>
    Sitting,
}

/// <summary>One person in a party, and where they stand relative to its middle.</summary>
public sealed record PartyMember(int Id, PersonLook Look, double OffsetX, double OffsetY)
{
    /// <summary>How they look. It changes when they buy a poncho or a hat and put it on.</summary>
    public PersonLook Look { get; internal set; } = Look;

    /// <summary>What they have in their hand, having bought it at a stall.</summary>
    public Carried Holding { get; internal set; }

    /// <summary>What they have bought and put on today: 1 for a poncho, 2 for a bucket hat, 3 for both.</summary>
    public int Bought { get; internal set; }
}

/// <summary>How a party gets to the festival.</summary>
public enum Travel
{
    /// <summary>On foot along the lane.</summary>
    OnFoot = 0,

    /// <summary>By car, parked in a car park field.</summary>
    Car = 1,

    /// <summary>On a shuttle bus to the bus stop.</summary>
    Bus = 2,
}

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

    public DateTime Arrives { get; internal set; }

    /// <summary>When they would turn up if they walked: a bus rider comes on the next bus after this.</summary>
    internal DateTime Planned { get; set; }

    /// <summary>How they get to the festival and home again.</summary>
    public Travel Travel { get; internal set; }

    /// <summary>The parking bay their car is in, for a party that drives.</summary>
    public TilePoint? Bay { get; internal set; }

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

    /// <summary>The crowd area this party is waiting by the gaps of to be let in, or -1.</summary>
    public int WaitFor { get; internal set; } = -1;

    public DateTime WaitUntil { get; internal set; }

    public bool IsOnSite => State is not (PartyState.Expected or PartyState.Gone);

    /// <summary>Where this party's tent is pitched in the camping field, or null if they are going home tonight.</summary>
    public TilePoint? Pitch { get; internal set; }

    /// <summary>True if they are staying the night in the camping field.</summary>
    public bool Camper => Pitch is not null;

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

    /// <summary>The entertainer (their staff number) they have stopped to listen to, or are walking over to, or -1.</summary>
    public int Busker { get; internal set; } = -1;

    /// <summary>When they will move on from listening to the entertainer.</summary>
    public DateTime BuskUntil { get; internal set; }

    /// <summary>Where the entertainer they are listening to stands, in metres, so they can face them.</summary>
    internal double BuskX { get; set; }

    internal double BuskY { get; set; }

    /// <summary>True while they are stood listening to an entertainer.</summary>
    public bool ListeningToBusker => Busker >= 0 && State == PartyState.Standing;

    /// <summary>The last set on the second stage this party made its mind up about, or -1.</summary>
    public int DecidedSecond { get; internal set; } = -1;

    /// <summary>The last set on the third stage this party made its mind up about, or -1.</summary>
    public int DecidedThird { get; internal set; } = -1;

    /// <summary>The last set on the fourth stage this party made its mind up about, or -1.</summary>
    public int DecidedFourth { get; internal set; } = -1;

    /// <summary>The last set on a stage (0 for the main stage) this party made its mind up about, or -1.</summary>
    public int DecidedOn(int stage) => stage switch { 0 => Decided, 1 => DecidedSecond, 2 => DecidedThird, _ => DecidedFourth };

    internal void Decide(int stage, int set)
    {
        switch (stage)
        {
            case 0: Decided = set; break;
            case 1: DecidedSecond = set; break;
            case 2: DecidedThird = set; break;
            default: DecidedFourth = set; break;
        }
    }

    /// <summary>Forgets every set they made their mind up about, so they think again.</summary>
    internal void ForgetDecisions() => Decided = DecidedSecond = DecidedThird = DecidedFourth = -1;

    /// <summary>True if they have made their mind up about any set on any stage.</summary>
    public bool DecidedAny => Decided >= 0 || DecidedSecond >= 0 || DecidedThird >= 0 || DecidedFourth >= 0;

    /// <summary>How hungry they are, from 0 (just eaten) to 1 and a little over (starving).</summary>
    public double Hunger { get; internal set; }

    public double Thirst { get; internal set; }

    /// <summary>How much they need the toilet, from 0 to 1 and a little over.</summary>
    public double Toilet { get; internal set; }

    /// <summary>How the day is going for them, from 0 (miserable) to 1 (delighted).</summary>
    public double Mood { get; internal set; } = Crowd.StartingMood;

    /// <summary>Which stall they are queuing at, or -1.</summary>
    public int Stall { get; internal set; } = -1;

    /// <summary>Which server of that stall is serving them, or -1 while they are still in the queue.</summary>
    public int Server { get; internal set; } = -1;

    /// <summary>True once they have reached the queue and are standing in it, one behind the next.</summary>
    public bool InLine { get; internal set; }

    /// <summary>How many places from the front of the queue the first of them stands. Part places are in between two.</summary>
    public double LinePos { get; internal set; }

    /// <summary>The place in the queue they are shuffling up to.</summary>
    internal int LineTarget { get; set; }

    /// <summary>How many of them have had their tickets checked and stepped through the gate.</summary>
    public int Checked { get; internal set; }

    /// <summary>How many times they have had to walk on because the back of the queue had moved.</summary>
    internal int Rejoins { get; set; }

    /// <summary>
    /// At the toilets, where each member has got to: -1 still waiting, the number of the cubicle
    /// they are using, or -2 once they have been. Null when the party is not at the toilets.
    /// </summary>
    internal int[]? Cubicles { get; set; }

    /// <summary>Which row beside the toilets this party gathers in once its members have been.</summary>
    internal int WaitRow { get; set; }

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

    /// <summary>When they will have finished the food and drink in their hands.</summary>
    public DateTime HoldUntil { get; internal set; }

    /// <summary>
    /// How far above the usual price they will go before they refuse to buy: 1 pays the usual
    /// price and no more, 2.4 pays well over double. Spread evenly, so each step up in price loses
    /// about the same number of customers.
    /// </summary>
    public double Stretch { get; internal set; } = 1.7;

    /// <summary>True while they are at a stall because they fancied something, not because they needed it.</summary>
    public bool Fancying { get; internal set; }

    /// <summary>They will not think about a treat or the shops again before this time.</summary>
    public DateTime FancyAt { get; internal set; }

    /// <summary>They will not grumble about prices again before this time.</summary>
    public DateTime GrumbleAt { get; internal set; }

    /// <summary>True once they have been to the hat stall, and to the merchandise tent.</summary>
    public bool BoughtHats { get; internal set; }

    public bool BoughtMerch { get; internal set; }

    /// <summary>How many times they have been on a ride or had a go at hook a duck today.</summary>
    public int FairGoes { get; internal set; }

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

    /// <summary>The seat they are sitting on or walking to, if any.</summary>
    public PlacedComfort? Seat { get; internal set; }

    /// <summary>Which of its seats each of them has.</summary>
    internal int[]? Seats { get; set; }

    /// <summary>True if they are sitting, or about to sit, on the ground: on artificial grass or sand.</summary>
    public bool OnGround { get; internal set; }

    /// <summary>When they will get up again.</summary>
    internal DateTime SitUntil { get; set; }

    /// <summary>No sitting down just for a rest before this: they have not long got up.</summary>
    internal DateTime RestAfter { get; set; }

    /// <summary>The sink or gel post they are on their way to.</summary>
    internal PlacedComfort? Wash { get; set; }

    /// <summary>True once they have paid to charge a phone today.</summary>
    public bool Charged { get; internal set; }

    /// <summary>True once a camper has had a shower, been to the camp info point, and been to the camp shop.</summary>
    public bool Showered { get; internal set; }

    public bool Lockered { get; internal set; }

    public bool CampShopped { get; internal set; }

    internal DateTime RetryAt { get; set; }

    public bool HasChild => Members.Any(m => m.Look.Age == AgeGroup.Child);

    /// <summary>True for a party on VIP tickets: they wear lanyards and may use VIP ground, stalls and buildings.</summary>
    public bool Vip { get; internal set; }

    /// <summary>How much the grown-ups have had to drink, in drinks each. It wears off at one an hour.</summary>
    public double Drink { get; internal set; }

    /// <summary>Until when one of them is bent double being sick.</summary>
    public DateTime SickUntil { get; internal set; }

    /// <summary>True while one of them is in a fight.</summary>
    public bool Fighting { get; internal set; }

    /// <summary>The party they are fighting, or -1.</summary>
    public int Opponent { get; internal set; } = -1;

    internal GameRandom Random { get; set; }

    internal List<TilePoint>? Path { get; set; }

    internal int PathIndex { get; set; }
}

/// <summary>One person as the game draws them: who they are, where they are, which way they face.</summary>
public readonly record struct PersonOnSite(int Id, PersonLook Look, double X, double Y, double Heading, bool Walking, bool Watching = false, Need? Wants = null, bool Unwell = false)
{
    /// <summary>What they have in their hand, having bought it at a stall.</summary>
    public Carried Holding { get; init; }

    /// <summary>True while this person is holding up their ticket for the gate staff.</summary>
    public bool Ticket { get; init; }

    /// <summary>How high the seat they are sitting on is, in metres (0 on the ground), or null if they are on their feet.</summary>
    public double? Seat { get; init; }

    /// <summary>The ride they are on (its number among the stalls), or -1 if they are not on one.</summary>
    public int Ride { get; init; } = -1;

    /// <summary>Which seat of the ride they are in.</summary>
    public int RideSeat { get; init; }

    /// <summary>How unsteady they are with drink, from 0 (steady) to 1 (staggering).</summary>
    public double Wobble { get; init; }

    /// <summary>True while they are in a fight.</summary>
    public bool Fighting { get; init; }

    /// <summary>True while they are bent double being sick.</summary>
    public bool BeingSick { get; init; }
}

/// <summary>
/// Everyone who comes to the festival. The whole day's comings and goings are worked out from the
/// festival's seed: between 100 and 200 people in parties of one to four, arriving through the
/// morning, wandering the field, and going home in the evening (families with children first).
/// </summary>
public sealed partial class Crowd
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

    /// <summary>How people feel when they walk in.</summary>
    public const double StartingMood = 0.65;

    /// <summary>Mood gained in an hour of watching an act, by its stars: a five star act is worth about three times a one star act.</summary>
    public static double Enjoyed(int stars) => 0.004 + (0.006 * Math.Clamp(stars, 1, 5));

    /// <summary>Sets the mood everyone arrives in, before the gates open.</summary>
    public void ArriveFeeling(double mood)
    {
        foreach (var party in _parties)
        {
            party.Mood = Math.Clamp(mood, 0, 1);
        }
    }

    /// <summary>The level at which a need sends someone to look for a stall, and the level at which it shows and starts to spoil their day.</summary>
    public const double Seeking = 0.6;
    public const double Pressing = 0.85;

    /// <summary>The level at which someone who has just watched a set goes to a stall in the break, sooner than they otherwise would.</summary>
    public const double InTheBreak = 0.35;

    /// <summary>How long a need takes to go from nothing to desperate, in hours.</summary>
    private const double HoursToHungry = 7;
    private const double HoursToThirsty = 3.5;
    private const double HoursToToilet = 4.5;

    /// <summary>How long people will stand in a stall queue before walking away, in minutes.</summary>
    public const double PatienceMinutes = 10;

    /// <summary>How long people wait at a gate with nobody checking tickets before they go home.</summary>
    public const double GatePatienceMinutes = 30;

    /// <summary>How far out from a stall's front the queue starts, leaving room for the people being served.</summary>
    public const double StallQueueStart = 2.8;

    /// <summary>How many places a stall's queue runs out before it folds back on itself, and the most places it has.</summary>
    private const int StallFold = 16;
    private const int StallPlaces = 90;
    private const int GatePlaces = 240;

    private readonly FieldMap _map;
    private readonly SiteLayout _site;
    private readonly PathFinder _paths;
    private readonly PathFinder _vipPaths;
    private readonly List<Party> _parties = new();
    private readonly List<QueueLine[]> _queues = new();
    private readonly List<QueueLine?> _stallQueues = new();
    private readonly List<Party?[]> _serving = new();
    private readonly List<CubicleUse?[]> _cubicles = new();
    private readonly List<TilePoint> _standable = new();
    private readonly HashSet<TilePoint> _standableSet = new();
    private DateTime _day;
    private DateTime _now;

    /// <summary>
    /// How many lanes of a gate have someone checking tickets, by the gate's number. Left unset,
    /// every lane of every gate is open.
    /// </summary>
    public Func<int, int>? OpenLanes { get; set; }

    /// <summary>
    /// How many people are checking tickets on one lane of a gate, by gate and lane. Each one is
    /// another ticket table, so two on a lane check people twice as fast. Left unset, one.
    /// </summary>
    public Func<int, int, int>? Checkers { get; set; }

    /// <summary>
    /// True while the gates are shut: people who arrive queue outside, and nobody's ticket is
    /// checked until the gates are opened.
    /// </summary>
    public bool GatesShut
    {
        get => _site.GatesShut;
        set => _site.GatesShut = value;
    }

    /// <summary>How long people will wait outside shut gates before they start to mind, in minutes.</summary>
    public const double ShutGraceMinutes = 30;

    /// <summary>Mood lost in an hour of waiting outside shut gates, once the first half hour is up.</summary>
    public const double ShutPerHour = 0.12;

    /// <summary>How long people will wait outside shut gates before they give up and go home, in minutes.</summary>
    public const double ShutPatienceMinutes = 120;

    /// <summary>
    /// Opens the gates: ticket checks start, and the wait for someone to check tickets counts from
    /// now rather than from when people joined the queue outside.
    /// </summary>
    public void OpenTheGates(DateTime now)
    {
        if (!GatesShut)
        {
            return;
        }

        GatesShut = false;
        foreach (var party in _parties)
        {
            if (party.State == PartyState.Queuing && party.Gate >= 0)
            {
                party.QueuedSince = now;
            }
        }
    }

    /// <summary>How many tickets are checked at once at the front of a lane.</summary>
    private int CheckersOn(int gate, int lane) => Math.Max(1, Checkers?.Invoke(gate, lane) ?? 1);

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

    /// <summary>The share of people who keep to the paths on a dry day. The rest cut across the grass.</summary>
    public const double DryPathShare = 0.55;

    /// <summary>The share of people who keep to the paths just now: more of them do once the grass is wet.</summary>
    public double PathShare { get; set; } = DryPathShare;

    private bool _sellers;

    /// <summary>True once any path has been laid.</summary>
    public bool HasPaths => _paths.HasPaths;

    /// <summary>Says which tiles have a path on them.</summary>
    public void SetPaths(IEnumerable<TilePoint> tiles)
    {
        _paths.SetPaths(tiles);
        _vipPaths.SetPaths(tiles);
    }

    /// <summary>
    /// Whether a party keeps to the paths. Each party has its own fixed liking for paths, so the same
    /// people always keep to them, and more join them as the share grows on a wet day.
    /// </summary>
    public bool KeepsToPaths(Party party) => (uint)(party.Id * 2246822519u) % 1000 < PathShare * 1000;

    private List<TilePoint>? Route(Party party, TilePoint from, TilePoint to) => PathsFor(party).FindPath(from, to, KeepsToPaths(party));

    private bool _vipPathsStale = true;

    /// <summary>
    /// The route finder for this party. VIPs only need their own when some ground is kept for
    /// them; until then they walk where everyone else does, and their finder is not kept up to date.
    /// </summary>
    private PathFinder PathsFor(Party party)
    {
        if (!party.Vip || !_map.HasVipOnly)
        {
            return _paths;
        }

        if (_vipPathsStale)
        {
            _vipPaths.Refresh();
            _vipPathsStale = false;
        }

        return _vipPaths;
    }

    /// <summary>Whether people have dressed for a wet day: wellies, and a rain poncho for about half of them.</summary>
    public bool DressedForRain { get; set; }

    /// <summary>Mood lost in an hour of pouring rain by someone with no poncho, and by someone with one.</summary>
    public const double SoakedPerHour = 0.07;

    public const double PonchoPerHour = 0.015;

    /// <summary>Mood lost in an hour of standing in deep mud.</summary>
    public const double MudPerHour = 0.06;

    /// <summary>How dark a place is right now, from 0 to 1, if the game knows: night with no light near is 1.</summary>
    public Func<double, double, double>? Darkness { get; set; }

    /// <summary>True once the daylight has gone: a fire pit cheers people more then.</summary>
    public bool Night { get; set; }

    /// <summary>True if a place, in metres, can see and hear a big screen well, if the game knows.</summary>
    public Func<double, double, bool>? OnScreen { get; set; }

    /// <summary>How far someone watching an act is from the front of its stage, in metres, or null if they are not watching one.</summary>
    public double? FromTheStage(Party party)
    {
        if (party.State != PartyState.Watching || ShowOf(party.Watch)?.Stage is not { } stage)
        {
            return null;
        }

        var (fx, fy, _, _) = RunningOrder.Front(stage);
        return Math.Sqrt(((party.X - fx) * (party.X - fx)) + ((party.Y - fy) * (party.Y - fy)));
    }

    /// <summary>
    /// How much of an act a party watching it gets: all of it near the stage, less and less further
    /// back, unless they are in front of a big screen.
    /// </summary>
    public double ShareOfTheAct(Party party) =>
        FromTheStage(party) is not { } metres || metres <= ClearFor(party) || (OnScreen?.Invoke(party.X, party.Y) ?? false)
            ? 1
            : BigScreens.Enjoyment(metres, ClearFor(party));

    /// <summary>
    /// How far back from the stage a party is watching people see and hear well: further for a
    /// bigger stage, and further still for one with screens of its own.
    /// </summary>
    public double ClearFor(Party party)
    {
        if (ShowOf(party.Watch)?.Stage is not { } stage)
        {
            return BigScreens.ClearMetres;
        }

        var info = Catalogue.Of(stage.Kind);
        return info.OwnScreens ? Math.Max(info.ClearMetres, BigScreens.OwnScreenMetres) : info.ClearMetres;
    }

    /// <summary>Mood lost in an hour of being somewhere pitch dark. Nobody minds the dark while watching an act.</summary>
    public const double DarkPerHour = 0.05;

    /// <summary>True if a party is somewhere the dark would bother them: about on the site, in a queue or at a stall.</summary>
    public static bool MindsTheDark(Party party) => party.State is PartyState.Standing or PartyState.Strolling or PartyState.Queuing or PartyState.AtStall;

    /// <summary>For each stage (main stage first), how many people have picked each square to watch its act from.</summary>
    private readonly int[][] _claims = new int[RunningOrder.MaxStages][];

    /// <summary>For each stage, the set those places were picked for.</summary>
    private readonly int[] _claimSlot = { -1, -1, -1, -1 };
    private int _measuredMinute = -1;

    /// <summary>The full crowd area the last search for a place to watch was turned away from, or -1.</summary>
    private int _turnedAway = -1;

    public Crowd(FieldMap map, int seed, DateTime festivalDay)
        : this(map, new SiteLayout(map), seed, festivalDay)
    {
    }

    public Crowd(FieldMap map, SiteLayout site, int seed, DateTime festivalDay)
    {
        _map = map;
        _site = site;
        _paths = new PathFinder(map);
        _vipPaths = new PathFinder(map, vip: true);
        Density = new DensityMap(map);
        for (var i = 0; i < _claims.Length; i++)
        {
            _claims[i] = new int[Density.Across * Density.Down];
        }

        Seed = seed;
        _day = festivalDay.Date;
        Plan(_day, null);
        SiteChanged();
        site.Changed += SiteChanged;
    }

    private readonly RunningOrder?[] _shows = new RunningOrder?[RunningOrder.MaxStages];

    /// <summary>The day's music, if there is any. People go and watch the acts they fancy.</summary>
    public RunningOrder? Show { get => _shows[0]; set => _shows[0] = value; }

    /// <summary>The second stage's line-up, if there is one.</summary>
    public RunningOrder? Second { get => _shows[1]; set => _shows[1] = value; }

    /// <summary>Every stage's line-up, main stage first (up to four).</summary>
    public IReadOnlyList<RunningOrder?> Shows
    {
        get => _shows;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            for (var i = 0; i < _shows.Length; i++)
            {
                _shows[i] = i < value.Count ? value[i] : null;
            }
        }
    }

    /// <summary>The line-up a set belongs to, by the set's number.</summary>
    private RunningOrder? ShowOf(int set) => set < 0 ? null : _shows[RunningOrder.StageOf(set)];

    /// <summary>True while the set a party went to watch is still on, or is a big act's they are waiting at the front for.</summary>
    private bool StillOn(int set, DateTime now) => set >= 0 && ShowOf(set)?.Drawing(now)?.Index == set;

    /// <summary>How long a party stays clapping and cheering after a set they watched to the end.</summary>
    private static TimeSpan ApplauseFor(Party party) => TimeSpan.FromMinutes(Performing.ApplauseMinutes(party.Id));

    /// <summary>True while a set a party watched has just finished and they are still clapping the act off.</summary>
    private bool Applauding(Party party, DateTime now) =>
        party.Watch >= 0 && ShowOf(party.Watch) is { } show && show.Bowing(now) is { } bowing && bowing.Index == party.Watch
        && now < bowing.End + ApplauseFor(party);

    /// <summary>
    /// True in the last part of a set, when those watching hold on for the end unless a need is
    /// pressing, rather than go to a stall.
    /// </summary>
    private bool NearTheEnd(int set, DateTime now) =>
        ShowOf(set)?.Playing(now) is { } slot && slot.Index == set && slot.End - now <= TimeSpan.FromMinutes(EndOfSetMinutes);

    /// <summary>How bad a need has to be to make someone miss the end of a set they are watching.</summary>
    public const double HoldOnFor = 0.95;

    /// <summary>How near the end of a set, in game minutes, those watching wait for it to finish before going for a drink or the toilet.</summary>
    public const int EndOfSetMinutes = 25;

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

    /// <summary>Raised each time one person buys one thing: the stall, which thing on its menu, and what they paid.</summary>
    public event Action<PlacedStall, int, int>? Sold;

    /// <summary>How many people have turned away from a stall today because everything on it cost too much for them.</summary>
    public int PricedOut { get; internal set; }

    /// <summary>A number from 0 up to 1 that is always the same for the same party, purpose and moment.</summary>
    private double Roll(int party, int purpose, int moment)
    {
        var h = unchecked((uint)((Seed * 73856093) ^ (party * 19349663) ^ (purpose * 83492791) ^ (moment * 40503)));
        h ^= h >> 15;
        h = unchecked(h * 2246822519u);
        h ^= h >> 13;
        h = unchecked(h * 3266489917u);
        h ^= h >> 16;
        return h / 4294967296.0;
    }

    /// <summary>Raised when someone drops a piece of litter: where, in metres.</summary>
    public event Action<double, double>? Dropped;

    /// <summary>Asked when a party is about to let go of some rubbish: true if it went in a bin instead.</summary>
    public Func<Party, int, bool>? Binned { get; set; }

    /// <summary>Whether a party is the sort to drop its rubbish. About half are, always the same ones.</summary>
    public bool Untidy(Party party) => (unchecked((uint)((Seed * 131) + (party.Id * 7919))) % 100) < 55;

    /// <summary>How many people are queuing at stalls.</summary>
    public int AtStalls => _parties.Where(p => p.State == PartyState.AtStall).Sum(p => p.Members.Count);

    /// <summary>How many people are in a stall's queue or walking to it, not counting anyone being served.</summary>
    public int StallQueueLength(int stall) => stall < _stallQueues.Count ? _stallQueues[stall]?.People ?? 0 : 0;

    /// <summary>How many of a stall's servers are busy with someone.</summary>
    public int BeingServed(int stall) => stall < _serving.Count ? _serving[stall].Count(p => p is not null) + _cubicles[stall].Count(c => c is not null) : 0;

    /// <summary>How one cubicle of a toilet looks just now: vacant, with its door open as someone goes in or out, or engaged.</summary>
    public CubicleLook Cubicle(int stall, int cubicle) =>
        stall < 0 || stall >= _cubicles.Count || cubicle < 0 || cubicle >= _cubicles[stall].Length ? CubicleLook.Vacant : _cubicles[stall][cubicle]?.Phase switch
        {
            CubiclePhase.Entering or CubiclePhase.Leaving => CubicleLook.DoorOpen,
            CubiclePhase.Inside => CubicleLook.Engaged,
            _ => CubicleLook.Vacant,
        };

    /// <summary>The queue at a stall, or null if it is not the kind of stall people queue at.</summary>
    public QueueLine? StallLine(int stall) => stall >= 0 && stall < _stallQueues.Count ? _stallQueues[stall] : null;

    /// <summary>The queue at one lane of a gate.</summary>
    public QueueLine? GateLine(int gate, int lane) => gate >= 0 && gate < _queues.Count && lane >= 0 && lane < _queues[gate].Length ? _queues[gate][lane] : null;

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

    /// <summary>How many people are in the queue for one lane of a gate, or walking to it.</summary>
    public int QueueLength(int gate, int lane) => GateLine(gate, lane)?.People ?? 0;

    public int Seed { get; }

    public IReadOnlyList<Party> Parties => _parties;

    /// <summary>How many people are expected over the whole day.</summary>
    public int Expected { get; private set; }

    /// <summary>How many people are on the site right now.</summary>
    public int OnSite => _parties.Where(p => p.IsOnSite).Sum(p => p.Members.Count);

    /// <summary>How many people have come through so far today, including those who have gone home.</summary>
    public int Arrived => _parties.Where(p => p.State != PartyState.Expected).Sum(p => p.Members.Count);

    /// <summary>How many of the people on site are inside the hedge.</summary>
    public int InField => _parties.Where(p => p.IsOnSite && _map.InFestival(FieldMap.TileAt(p.X, p.Y))).Sum(p => p.Members.Count);

    /// <summary>True while a party is sat on a ride: they have walked up to it and it has not let them off yet.</summary>
    public bool OnRide(Party party) =>
        party.State == PartyState.AtStall && party.Server >= 0 && !party.Moving && party.Stall >= 0 && party.Stall < _site.Stalls.Count && Rides.IsRide(_site.Stalls[party.Stall].Kind);

    /// <summary>How many people are sat on a ride just now.</summary>
    public int Riding(int stall) => _parties.Where(p => p.Stall == stall && OnRide(p)).Sum(p => p.Members.Count);

    /// <summary>How many big teddies have been won at hook a duck today.</summary>
    public int TeddiesWon => _parties.Sum(p => p.Members.Count(m => m.Holding == Carried.Teddy));

    /// <summary>Raised when somebody wins a teddy: the hook a duck stall it was won at.</summary>
    public event Action<PlacedStall>? TeddyWon;

    /// <summary>True for something that is kept in the hand all day, not eaten or drunk.</summary>
    public static bool Keeps(Carried item) => item is Carried.Bag or Carried.Teddy;

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

                for (var i = 0; i < party.Members.Count; i++)
                {
                    var m = party.Members[i];
                    var (x, y, heading) = Place(party, i);
                    var walking = party.IsWalking;
                    if (party.Cubicles is { } cubicles && party.State == PartyState.AtStall && party.Stall >= 0 && party.Stall < _cubicles.Count)
                    {
                        if (cubicles[i] >= 0 && _cubicles[party.Stall][cubicles[i]] is { } use)
                        {
                            if (use.Phase == CubiclePhase.Inside)
                            {
                                // Behind a shut door.
                                continue;
                            }

                            (x, y, heading, walking) = (use.X, use.Y, use.Heading, true);
                        }
                        else if (cubicles[i] == -2)
                        {
                            walking = false;
                        }
                    }

                    yield return new PersonOnSite(m.Id, m.Look, x, y, heading, walking, party.State == PartyState.Watching,
                        i == 0 && party.State != PartyState.Unwell ? party.Wants : null,
                        party.State == PartyState.Unwell && i == 0)
                    {
                        Holding = m.Holding,
                        Seat = party.State == PartyState.Sitting ? SeatHeight(party, i) : null,
                        Ride = OnRide(party) ? party.Stall : -1,
                        RideSeat = (Math.Max(0, party.Server) * Rides.SeatsEach) + Math.Min(i, Rides.SeatsEach - 1),
                        Ticket = party.State == PartyState.Queuing && party.InLine && party.ServiceLeft >= 0 && i >= party.Checked && i < party.Checked + CheckersOn(party.Gate, party.Lane),
                        Wobble = party.Drink > 0 && GrownUp(m) ? WobbleOf(party.Drink) : 0,
                        Fighting = party.Fighting && i == 0,
                        BeingSick = i == 0 && _now < party.SickUntil && party.State == PartyState.Standing,
                    };
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
        Replanned?.Invoke();
    }

    /// <summary>
    /// The next day of a festival of more than one day: the same people come again, a day later.
    /// Everyone planned so far is forgotten, as with <see cref="Replan"/>. With tents staying up,
    /// the campers' tents stay pitched overnight and they do not pay or settle in again at the gate.
    /// </summary>
    public void NewDay(DateTime day, int people, bool tentsStayUp)
    {
        _day = day.Date;
        TentsStayUp = tentsStayUp;
        _measuredMinute = -1;
        _turnedAway = -1;
        for (var i = 0; i < _claimSlot.Length; i++)
        {
            _claimSlot[i] = -1;
            Array.Clear(_claims[i]);
        }

        Replan(people);
    }

    /// <summary>True on the second or third day of a festival, when the campers' tents are up from the night before.</summary>
    public bool TentsStayUp { get; set; }

    /// <summary>Raised when the crowd has been planned afresh, so how they travel can be worked out again.</summary>
    public event Action? Replanned;

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
                Planned = arrives,
                Hunger = needs.Between(0.1, 0.5),
                Thirst = needs.Between(0.1, 0.4),
                Toilet = needs.Between(0, 0.3),
                Appetite = needs.Between(0.8, 1.25),
                Stretch = 1 + (1.4 * Roll(_parties.Count, 77, 0)),
            });
            Expected += size;
        }

        MarkVips();
    }

    /// <summary>How often a shuttle bus pulls in at the stop.</summary>
    public static readonly TimeSpan BusEvery = TimeSpan.FromMinutes(15);

    /// <summary>How many people come in each car, on average.</summary>
    public const double PeopleACar = 2.5;

    /// <summary>Raised each time a car is parked.</summary>
    public event Action? CarParked;

    /// <summary>How many came each way, counted in people, among those who have arrived.</summary>
    public int CameBy(Travel travel) => _parties.Where(p => p.State != PartyState.Expected && p.Travel == travel).Sum(p => p.Members.Count);

    /// <summary>How many are planned to come each way, counted in people.</summary>
    public int ComingBy(Travel travel) => _parties.Where(p => p.Travel == travel).Sum(p => p.Members.Count);

    /// <summary>The bays with a car in them now: from when its party arrives until they have driven home. Campers' cars stay the night.</summary>
    public IEnumerable<TilePoint> CarsParked => _parties
        .Where(p => p.Travel == Travel.Car && p.Bay is not null && p.State != PartyState.Expected && (p.State != PartyState.Gone || (p.Camper && p.Ticketed == true)))
        .Select(p => p.Bay!.Value).Distinct();

    /// <summary>
    /// Says how the crowd travels: so many people on foot, so many by car into the bays given
    /// (two and a half people a car, so small parties share), so many by bus, and anyone left over on foot. The same parties travel
    /// the same way every time. Bus riders turn up when the next bus pulls in.
    /// </summary>
    public void SetTravel(int onFoot, int byCar, int byBus, IReadOnlyList<TilePoint> bays)
    {
        var inCars = 0;
        foreach (var party in _parties.OrderBy(p => (uint)(p.Id * 2246822519u) % 9973).ThenBy(p => p.Id))
        {
            var size = party.Members.Count;
            party.Bay = null;
            party.Arrives = party.Planned;
            if (onFoot >= size)
            {
                party.Travel = Travel.OnFoot;
                onFoot -= size;
            }
            else if (byCar >= size && (int)(inCars / PeopleACar) < bays.Count)
            {
                // Small parties share a lift, so a car park fills at two and a half people a space.
                party.Travel = Travel.Car;
                party.Bay = bays[(int)(inCars / PeopleACar)];
                inCars += size;
                byCar -= size;
            }
            else if (byBus >= size && _map.BusStop is not null)
            {
                party.Travel = Travel.Bus;
                byBus -= size;
                var late = (party.Planned - _day).Ticks % BusEvery.Ticks;
                party.Arrives = late == 0 ? party.Planned : party.Planned + TimeSpan.FromTicks(BusEvery.Ticks - late);
            }
            else
            {
                party.Travel = Travel.OnFoot;
            }
        }
    }

    private TilePoint LaneEnd(Party party)
    {
        if (party.Travel == Travel.Car && party.Bay is { } bay)
        {
            return _map.IsWalkable(bay) ? bay : NearestWalkable(bay);
        }

        if (party.Travel == Travel.Bus && _map.BusStop is { } stop)
        {
            // They get off, and wait to get on, along the kerb by the stop.
            return new TilePoint(stop.X - 6 + (party.Id % 13), stop.Y + (party.Id % 3));
        }

        // People spread across the whole width of the lane as they go.
        var y = _map.Lane.Y + (party.Id % _map.Lane.Height);
        return new TilePoint(party.FromWest ? 0 : _map.Width - 1, y);
    }

    /// <summary>Where a party goes at the end of their day: back down the lane, or to their tent.</summary>
    private TilePoint Home(Party party) => party.Pitch is { } pitch && party.Ticketed == true ? pitch : LaneEnd(party);

    /// <summary>The most of the crowd that will ever camp, however cheap the pitches.</summary>
    public const double MostCamping = 0.5;

    /// <summary>
    /// The share of the crowd that brings a tent at a pitch price: half when pitches are free, nearly
    /// two in five at four pounds, a quarter at seven, a sixth at ten, and only a few past twenty.
    /// </summary>
    public static double CamperShareAt(int pounds) => MostCamping / (1 + Math.Pow(Math.Max(0, pounds) / 7.0, 2));

    /// <summary>The share of the crowd that camps when camping is on offer, set from the pitch price.</summary>
    public double CamperShare { get; set; } = CamperShareAt(4);

    /// <summary>How many people have a tent in the camping field.</summary>
    public int Campers => _parties.Where(p => p.Camper).Sum(p => p.Members.Count);

    /// <summary>How many of them are through the gate, with the tent up.</summary>
    public int CampersIn => _parties.Where(p => p.Camper && p.Ticketed == true).Sum(p => p.Members.Count);

    /// <summary>How many campers have turned in for the night.</summary>
    public int InTents => _parties.Where(p => p.Camper && p.Ticketed == true && p.State == PartyState.Gone).Sum(p => p.Members.Count);

    /// <summary>The tents that are up: where, and whose (for a colour that stays the same). A tent packed away in the morning is gone.</summary>
    public IEnumerable<(TilePoint Pitch, int Party)> Tents =>
        _parties.Where(p => p.Camper && p.Pitch is not null && (p.Ticketed == true || TentsStayUp) && !_struck.Contains(p.Id)).Select(p => (p.Pitch!.Value, p.Id));

    /// <summary>
    /// The tents to draw: those up because their campers are here, and the tents ready pitched and
    /// bell tents put up before anyone arrives. Packed-away tents are gone.
    /// </summary>
    public IEnumerable<(TilePoint Pitch, int Party, CampKind Kind)> TentsUp =>
        _parties.Where(p => p.Pitch is not null && !_struck.Contains(p.Id) && (p.Ticketed == true || TentsStayUp || KindOf(p) is CampKind.ReadyPitched or CampKind.Glamping))
            .Select(p => (p.Pitch!.Value, p.Id, KindOf(p)));

    /// <summary>Every tent that was put up today, packed away since or not.</summary>
    public IEnumerable<(TilePoint Pitch, int Party)> Pitched =>
        _parties.Where(p => p.Camper && p.Ticketed == true).Select(p => (p.Pitch!.Value, p.Id));

    private readonly HashSet<int> _struck = new();

    /// <summary>Takes a party's tent down: packed away in the morning, or cleared once it was left behind.</summary>
    internal void StrikeTent(int party) => _struck.Add(party);

    /// <summary>The parties whose tents have been taken down.</summary>
    internal IReadOnlyCollection<int> Struck => _struck;

    /// <summary>A camper has come through the gate and paid for their pitch.</summary>
    public event Action<Party>? CamperArrived;

    /// <summary>
    /// Gives a share of the crowd a pitch in the camping field. They come like anyone else, stay for
    /// the headliner, and walk to their tents at the end of the night and not out of the gate.
    /// </summary>
    public void PitchCamp()
    {
        _struck.Clear();
        foreach (var party in _parties)
        {
            party.Pitch = null;
        }

        foreach (var (party, pitch) in PlanPitches())
        {
            party.Pitch = pitch;
        }
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
    private TilePoint? SomewhereInField(GameRandom random)
    {
        if (_standable.Count == 0)
        {
            return null;
        }

        // People wandering about keep out of the crowd areas: those are for watching the acts from.
        var tile = _standable[random.Next(_standable.Count)];
        for (var tries = 0; tries < 6 && _site.AreaInside(tile) >= 0; tries++)
        {
            tile = _standable[random.Next(_standable.Count)];
        }

        return tile;
    }

    /// <summary>
    /// Called whenever something is built or taken down: routes are worked out again, queues re-form,
    /// and anyone who has had something built on top of them steps aside.
    /// </summary>
    private void SiteChanged()
    {
        _paths.Refresh();
        _vipPathsStale = true;
        RefreshVipPlaces();
        SizeAreas();

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
                if (_site.SideOf(p) == wanted && HasRoom(p) && _map.InFestival(p))
                {
                    _standable.Add(p);
                }
            }
        }

        _standableSet.Clear();
        _standableSet.UnionWith(_standable);

        LayOutQueues();

        foreach (var party in _parties)
        {
            if (!party.IsOnSite)
            {
                continue;
            }

            if ((party.Seat is { } seat && !_site.Comforts.Contains(seat)) || (party.Wash is { } wash && !_site.Comforts.Contains(wash)))
            {
                // What they were sitting on, or walking to, has been taken away.
                GiveUpSeat(party);
                if (party.State == PartyState.Sitting)
                {
                    party.State = PartyState.Standing;
                }
            }

            var at = FieldMap.TileAt(party.X, party.Y);
            if (!WalkableFor(party, at))
            {
                at = NearestWalkable(at);
                (party.X, party.Y) = FieldMap.CentreOf(at);
            }

            var outside = _site.SideOf(at) == Side.Outside && !_site.OpenWayIn;
            party.InLine = false;
            party.Checked = 0;
            party.Server = -1;
            party.Cubicles = null;
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
                        party.ForgetDecisions();
                    }

                    Send(party, _standableSet.Contains(party.Target) ? party.Target : SomewhereInField(party.Random), party.State);
                    break;
                case PartyState.Leaving:
                    Send(party, Home(party), PartyState.Leaving);
                    break;
                case PartyState.Watching:
                    // The stage may have moved or gone: think again.
                    party.Watch = -1;
                    party.ForgetDecisions();
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
                var length = _queues[g][lane].People;
                if (length < bestLength)
                {
                    (best, bestLane, bestLength) = (g, lane, length);
                }
            }
        }

        party.State = PartyState.Queuing;
        if (best >= 0)
        {
            party.Gate = best;
            party.Lane = bestLane;
            party.QueuedSince = _now;
            var line = _queues[best][bestLane];
            party.Target = Approach(line, Entry(line, line.People));
            line.Parties.Add(party);
            party.Path = Route(party, FieldMap.TileAt(party.X, party.Y), party.Target);
            party.PathIndex = 0;
            party.Moving = party.Path is not null;
            return;
        }

        // No way in at all: wait on the track by the hedge and look again in a couple of minutes.
        var track = _map.Track;
        party.Target = new TilePoint(track.X + 1 + (party.Id * 7 % (track.Width - 2)), track.Y + 2 + (party.Id * 5 % (track.Height - 4)));
        party.Path = Route(party, FieldMap.TileAt(party.X, party.Y), party.Target);
        party.PathIndex = 0;
        party.ServiceLeft = 120;
        party.Moving = party.Path is not null;
    }

    /// <summary>
    /// Sets out every queue afresh: one for each lane of a gate, running out from the gate on the
    /// outside, and one for each stall, running out from its front and folding back on itself.
    /// No queue runs through another, or through where people stand to be served.
    /// </summary>
    private void LayOutQueues()
    {
        _queues.Clear();
        _stallQueues.Clear();
        _serving.Clear();
        _cubicles.Clear();
        _taken.Clear();
        _sellers = _site.Stalls.Any(s => Catalogue.Sells(s.Kind));
        var fenced = MatchLanes();
        foreach (var stall in _site.Stalls)
        {
            var info = Catalogue.Of(stall.Kind);
            for (var k = 0; Catalogue.HasQueue(stall.Kind) && k < info.Servers; k++)
            {
                if (info.Serves == Need.Toilet)
                {
                    Take(SiteLayout.CubicleDoor(stall, k, DoorMetres));
                    Take(SiteLayout.CubicleDoor(stall, k, WaitMetres));
                }
                else
                {
                    Take(SiteLayout.ServingPlace(stall, k, CounterMetres));
                    Take(SiteLayout.ServingPlace(stall, k, CounterMetres + 0.8));
                }
            }
        }

        for (var g = 0; g < _site.Gates.Count; g++)
        {
            var use = _site.GateUses[g];
            var lanes = new QueueLine[Catalogue.Of(_site.Gates[g].Kind).Lanes];
            for (var lane = 0; lane < lanes.Length; lane++)
            {
                var (fx, fy) = _site.LaneMouth(g, lane, 0);
                lanes[lane] = new QueueLine(fx, fy);
                if (use.InUse && fenced.TryGetValue((true, g, lane), out var built))
                {
                    AlongLane(lanes[lane], built.Lane, built.FromEnd, GatePlaces, p => _site.SideOf(p) == Side.Outside);
                }
                else if (use.InUse)
                {
                    var (x, y) = _site.LaneMouth(g, lane, 0.9);
                    Grow(lanes[lane], x, y, use.OutX, use.OutY, GatePlaces, 0, p => _site.SideOf(p) == Side.Outside);
                }
            }

            _queues.Add(lanes);
        }

        foreach (var stall in _site.Stalls)
        {
            var info = Catalogue.Of(stall.Kind);
            _serving.Add(new Party?[info.Serves == Need.Toilet || !Catalogue.HasQueue(stall.Kind) ? 0 : info.Servers]);
            _cubicles.Add(new CubicleUse?[info.Serves == Need.Toilet ? info.Servers : 0]);
            if (!Catalogue.HasQueue(stall.Kind))
            {
                _stallQueues.Add(null);
                continue;
            }

            var (fx, fy, dx, dy) = SiteLayout.Front(stall.ServeRect, stall.Facing);
            var line = new QueueLine(fx, fy);
            _layingVip = stall.ForVips;
            if (fenced.TryGetValue((false, _stallQueues.Count, 0), out var built))
            {
                AlongLane(line, built.Lane, built.FromEnd, StallPlaces, _ => true);
            }
            else
            {
                Grow(line, fx + (dx * StallQueueStart), fy + (dy * StallQueueStart), dx, dy, StallPlaces, StallFold, _ => true);
            }

            _stallQueues.Add(line);
            _layingVip = false;
        }
    }

    /// <summary>True while the queue for a stall kept for VIPs is being laid out: it may run over VIP ground.</summary>
    private bool _layingVip;

    /// <summary>How near the end of a queue lane must be to where a queue would start for the queue to use it, in metres.</summary>
    public const double LaneReach = 3.0;

    /// <summary>
    /// Pairs the queue lanes the player has laid with the queues they serve. A lane belongs to the
    /// gate or stall whose queue would start nearest one of its ends, if that is within a few
    /// metres; that end is the front. Each queue has one lane and each lane one queue.
    /// </summary>
    private Dictionary<(bool Gate, int Index, int Lane), (int Lane, bool FromEnd)> MatchLanes()
    {
        var pairs = new Dictionary<(bool, int, int), (int, bool)>();
        var lanes = _site.Lanes;
        if (lanes.Count == 0)
        {
            return pairs;
        }

        var offers = new List<(double Away, (bool, int, int) Queue, int Lane, bool FromEnd)>();
        void Offer((bool, int, int) queue, double x, double y)
        {
            for (var l = 0; l < lanes.Count; l++)
            {
                foreach (var fromEnd in new[] { false, true })
                {
                    var (ex, ey) = FieldMap.CentreOf(fromEnd ? lanes[l][^1] : lanes[l][0]);
                    var away = Math.Sqrt(((ex - x) * (ex - x)) + ((ey - y) * (ey - y)));
                    if (away <= LaneReach)
                    {
                        offers.Add((away, queue, l, fromEnd));
                    }
                }
            }
        }

        for (var g = 0; g < _site.Gates.Count; g++)
        {
            for (var lane = 0; _site.GateUses[g].InUse && lane < Catalogue.Of(_site.Gates[g].Kind).Lanes; lane++)
            {
                var (x, y) = _site.LaneMouth(g, lane, 0.9);
                Offer((true, g, lane), x, y);
            }
        }

        for (var i = 0; i < _site.Stalls.Count; i++)
        {
            if (Catalogue.HasQueue(_site.Stalls[i].Kind))
            {
                var (fx, fy, dx, dy) = SiteLayout.Front(_site.Stalls[i].ServeRect, _site.Stalls[i].Facing);
                Offer((false, i, 0), fx + (dx * LaneFront), fy + (dy * LaneFront));
            }
        }

        var used = new HashSet<int>();
        foreach (var (_, queue, lane, fromEnd) in offers.OrderBy(o => o.Away))
        {
            if (!pairs.ContainsKey(queue) && used.Add(lane))
            {
                pairs[queue] = (lane, fromEnd);
            }
        }

        return pairs;
    }

    /// <summary>How far out from a stall's front a queue lane is best started, in metres.</summary>
    private const double LaneFront = 2.0;

    private bool FreeFor(double x, double y, Func<TilePoint, bool> allowed)
    {
        var tile = FieldMap.TileAt(x, y);
        return (_layingVip ? _map.IsWalkableForVip(tile) : _map.IsWalkable(tile)) && allowed(tile) && !Taken(x, y);
    }

    /// <summary>
    /// Sets a queue out along a lane the player has laid: a place every step down the middle of it
    /// from the front, round its corners, and then on out of the open end as far as there is room.
    /// </summary>
    private void AlongLane(QueueLine line, int laneIndex, bool fromEnd, int most, Func<TilePoint, bool> allowed)
    {
        const double s = QueueLine.Spacing;
        var tiles = fromEnd ? _site.Lanes[laneIndex].Reverse().ToList() : _site.Lanes[laneIndex].ToList();
        var (x, y) = FieldMap.CentreOf(tiles[0]);
        line.Add(x, y);
        var owed = s;
        var (dx, dy) = (0.0, 0.0);
        for (var i = 1; i < tiles.Count; i++)
        {
            var (nx, ny) = FieldMap.CentreOf(tiles[i]);
            (dx, dy) = ((nx - x) / FieldMap.TileMetres, (ny - y) / FieldMap.TileMetres);
            var left = FieldMap.TileMetres;
            while (left >= owed - 1e-9)
            {
                (x, y) = (x + (dx * owed), y + (dy * owed));
                left -= owed;
                owed = s;
                line.Add(x, y);
            }

            owed -= left;
            (x, y) = (nx, ny);
        }

        foreach (var (px, py) in line.Places)
        {
            Take((px, py));
        }

        line.Fenced = line.Places.Count;
        line.Mouth = (x + (dx * FieldMap.TileMetres), y + (dy * FieldMap.TileMetres));

        // Past the open end the queue carries on across the grass like any other.
        var (lx, ly) = line.Places[^1];
        var (tx, ty) = (lx + (dx * s), ly + (dy * s));
        if (_site.IsLane(FieldMap.TileAt(tx, ty)))
        {
            (tx, ty) = (tx + (dx * FieldMap.TileMetres), ty + (dy * FieldMap.TileMetres));
        }

        if (line.Places.Count < most && FreeFor(tx, ty, allowed))
        {
            Grow(line, tx, ty, dx, dy, most, 0, allowed);
        }
    }

    /// <summary>How far out from a stall's front the people being served stand, in metres.</summary>
    private const double CounterMetres = 0.7;

    private readonly Dictionary<(int, int), List<(double X, double Y)>> _taken = new();

    private void Take((double X, double Y) at)
    {
        var key = ((int)Math.Floor(at.X), (int)Math.Floor(at.Y));
        if (!_taken.TryGetValue(key, out var list))
        {
            _taken[key] = list = new List<(double, double)>();
        }

        list.Add(at);
    }

    /// <summary>True if a place in some queue, or at a counter, is already within an arm's length of here.</summary>
    private bool Taken(double x, double y)
    {
        const double near = 0.72;
        var (cx, cy) = ((int)Math.Floor(x), (int)Math.Floor(y));
        for (var gy = cy - 1; gy <= cy + 1; gy++)
        {
            for (var gx = cx - 1; gx <= cx + 1; gx++)
            {
                if (!_taken.TryGetValue((gx, gy), out var list))
                {
                    continue;
                }

                foreach (var (tx, ty) in list)
                {
                    if (((tx - x) * (tx - x)) + ((ty - y) * (ty - y)) < near * near)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Adds places to a queue, one behind the next. A gate's queue (no fold) runs straight out and
    /// turns a corner when something is in the way. A stall's queue runs out a set number of
    /// places, steps to one side and comes back, like the rope lanes at a ticket office.
    /// </summary>
    private void Grow(QueueLine line, double x, double y, double dx, double dy, int most, int fold, Func<TilePoint, bool> allowed)
    {
        const double s = QueueLine.Spacing;
        bool Free(double px, double py) => FreeFor(px, py, allowed);

        var (sx, sy) = (-dy, dx);
        var depth = 0;
        var going = 1;
        var rows = 0;
        var across = 0;
        line.Add(x, y);
        Take((x, y));
        while (line.Places.Count < most)
        {
            var (nx, ny) = (x + (dx * going * s), y + (dy * going * s));
            var room = fold == 0 || (going > 0 ? depth < fold - 1 : depth > 0);
            if (across == 0 && room && Free(nx, ny))
            {
                depth += going;
            }
            else if (fold > 0)
            {
                // Step two places to one side and come back the other way, leaving a clear gap between the rows.
                (nx, ny) = (x + (sx * s), y + (sy * s));
                if (!Free(nx, ny) && rows == 0 && across == 0)
                {
                    (sx, sy) = (-sx, -sy);
                    (nx, ny) = (x + (sx * s), y + (sy * s));
                }

                if (!Free(nx, ny))
                {
                    break;
                }

                if (++across == RowsApart)
                {
                    across = 0;
                    going = -going;
                    rows++;
                }
            }
            else if (Free(x + (sx * s), y + (sy * s)))
            {
                // Turn the corner and carry on along whatever is in the way.
                (dx, dy) = (sx, sy);
                (sx, sy) = (-dy, dx);
                (nx, ny) = (x + (dx * s), y + (dy * s));
            }
            else if (Free(x - (sx * s), y - (sy * s)))
            {
                (dx, dy) = (-sx, -sy);
                (sx, sy) = (-dy, dx);
                (nx, ny) = (x + (dx * s), y + (dy * s));
            }
            else
            {
                break;
            }

            (x, y) = (nx, ny);
            line.Add(x, y);
            Take((x, y));
        }
    }

    /// <summary>How many places apart the rows of a folded queue are.</summary>
    private const int RowsApart = 3;

    /// <summary>The place a party makes for to join a queue with so many people already in it.</summary>
    private static int Entry(QueueLine line, int people) =>
        Math.Clamp(Math.Max(people, line.Fenced), 0, Math.Max(0, line.Places.Count - 1));

    /// <summary>
    /// A party that has walked up to a queue takes its place at the back. If the back has moved on
    /// since they set off they walk on to it. True once they are standing in the line.
    /// </summary>
    private bool Join(Party party, QueueLine line)
    {
        var back = Math.Min(line.Standing, Math.Max(0, line.Places.Count - 1));
        var enter = Entry(line, back);
        var (x, y, heading) = line.At(enter);
        var away = Math.Sqrt(((x - party.X) * (x - party.X)) + ((y - party.Y) * (y - party.Y)));
        var door = Approach(line, enter);
        if (door != FieldMap.TileAt(x, y))
        {
            var (mx, my) = FieldMap.CentreOf(door);
            away = Math.Sqrt(((mx - party.X) * (mx - party.X)) + ((my - party.Y) * (my - party.Y)));
        }

        if (away > 1.5 && party.Rejoins < 3 && Route(party, FieldMap.TileAt(party.X, party.Y), door) is { Count: > 0 } path)
        {
            party.Rejoins++;
            party.Target = door;
            party.Path = path;
            party.PathIndex = 0;
            party.Moving = true;
            return false;
        }

        // The queue forms in the order people get there, so nobody waits behind someone who is
        // still on their way across the field.
        line.Parties.Remove(party);
        var behind = line.Parties.FindIndex(p => !p.InLine);
        line.Parties.Insert(behind < 0 ? line.Parties.Count : behind, party);
        party.InLine = true;
        party.LinePos = enter;
        party.LineTarget = back;
        (party.X, party.Y, party.Heading) = (x, y, heading);
        return true;
    }

    /// <summary>Shuffles a party up the queue towards its place. True once it is standing there.</summary>
    private static bool Shuffle(Party party, QueueLine line)
    {
        var gap = party.LinePos - party.LineTarget;
        var moving = Math.Abs(gap) > 0.02;
        if (moving)
        {
            party.LinePos -= Math.Sign(gap) * Math.Min(Math.Abs(gap), party.Speed / QueueLine.Spacing);
        }
        else
        {
            party.LinePos = party.LineTarget;
        }

        (party.X, party.Y, party.Heading) = line.At(party.LinePos);
        party.Moving = moving;
        return !moving;
    }

    /// <summary>Works out the place every party standing in a queue should be in: behind everyone ahead of them.</summary>
    private void OrderQueues()
    {
        foreach (var lanes in _queues)
        {
            foreach (var line in lanes)
            {
                Order(line);
            }
        }

        foreach (var line in _stallQueues)
        {
            if (line is not null)
            {
                Order(line);
            }
        }
    }

    private static void Order(QueueLine line)
    {
        var ahead = 0;
        foreach (var party in line.Parties)
        {
            if (party.InLine)
            {
                party.LineTarget = ahead;
                ahead += party.Members.Count - party.Checked;
            }
        }
    }

    /// <summary>Where one member of a party stands, and the way they face.</summary>
    private (double X, double Y, double Heading) Place(Party party, int i)
    {
        if (party.State == PartyState.Queuing && party.InLine && GateLine(party.Gate, party.Lane) is { } gateLine)
        {
            if (i >= party.Checked)
            {
                return gateLine.At(party.LinePos + i - party.Checked);
            }

            // Those already checked wait just inside for the rest of their party.
            var use = _site.GateUses[party.Gate];
            var (ix, iy) = _site.LaneMouth(party.Gate, party.Lane, -(1.3 + (QueueLine.Spacing * (party.Checked - 1 - i))));
            return (ix, iy, Math.Atan2(-use.OutX, -use.OutY));
        }

        if (party.State == PartyState.AtStall && party.Stall >= 0 && party.Stall < _site.Stalls.Count)
        {
            if (party.Cubicles is not null && (i < party.Checked || !party.InLine))
            {
                // Those who have been wait in a row, clear of the doors, for the rest of their party.
                var toilets = _site.Stalls[party.Stall];
                var (fx, fy, dx, dy) = SiteLayout.Front(toilets.Rect, toilets.Facing);
                var (wx, wy) = Waiting(party);
                return (wx + (dy * i * 0.75), wy - (dx * i * 0.75), Math.Atan2(dy, -dx));
            }

            if (party.InLine && _stallQueues[party.Stall] is { } line)
            {
                return line.At(party.LinePos + i - party.Checked);
            }

            if (party.Server >= 0)
            {
                // At the counter people stand close together, two abreast, facing whoever is serving.
                var stall = _site.Stalls[party.Stall];
                var info = Catalogue.Of(stall.Kind);
                var (_, _, fx, fy) = SiteLayout.Front(stall.Rect, stall.Facing);
                var narrow = party.Members.Count == 1 || info.Width * FieldMap.TileMetres * 0.75 / info.Servers < 1.0;
                var across = narrow ? 0 : ((i % 2) - 0.5) * 0.5;
                var back = (narrow ? i : i / 2) * 0.55;
                return (party.X - (fy * across) + (fx * back), party.Y + (fx * across) + (fy * back), Math.Atan2(-fx, -fy));
            }
        }

        if (party.State == PartyState.Sitting && party.Seat is { } comfort && party.Seats is { } seats && i < seats.Length)
        {
            return comfort.Place(Comforts.Of(comfort.Kind).Seats[seats[i]]);
        }

        var m = party.Members[i];
        return (party.X + m.OffsetX, party.Y + m.OffsetY, party.Heading);
    }

    private void LeaveQueue(Party party)
    {
        if (party.Gate >= 0 && party.Gate < _queues.Count && party.Lane < _queues[party.Gate].Length)
        {
            _queues[party.Gate][party.Lane].Parties.Remove(party);
        }

        party.Gate = -1;
        party.ServiceLeft = -1;
        party.Moving = false;
        if (party.InLine)
        {
            StepOut(party);
        }

        party.InLine = false;
        party.Checked = 0;
        party.Rejoins = 0;
    }

    /// <summary>Someone leaving a fenced queue lane part way along ducks out under the barrier.</summary>
    private void StepOut(Party party)
    {
        var at = FieldMap.TileAt(party.X, party.Y);
        if (_site.IsLane(at))
        {
            (party.X, party.Y) = FieldMap.CentreOf(NearestWalkable(at));
        }
    }

    /// <summary>The tile to walk to for a place in a queue: the place itself, or the open end of the lane if the place is inside one.</summary>
    private TilePoint Approach(QueueLine line, int place)
    {
        var (x, y, _) = line.At(place);
        var tile = FieldMap.TileAt(x, y);
        return _map.IsWalkable(tile) || line.Mouth is not { } mouth ? tile : FieldMap.TileAt(mouth.X, mouth.Y);
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

        var line = _queues[party.Gate][party.Lane];
        if (!party.InLine && !Join(party, line))
        {
            return;
        }

        if (GatesShut)
        {
            // Waiting outside shut gates is fine for a while; after that people mind, and in the end give up.
            var waited = _now - party.QueuedSince;
            if (waited > TimeSpan.FromMinutes(ShutGraceMinutes))
            {
                party.Mood = Math.Max(0, party.Mood - (ShutPerHour / 3600));
            }

            if (waited > TimeSpan.FromMinutes(ShutPatienceMinutes))
            {
                LeaveQueue(party);
                GoHome(party, unhappy: true);
                return;
            }
        }

        if (!Shuffle(party, line) || party.LineTarget != 0)
        {
            return;
        }

        if (GatesShut)
        {
            // The gates are not open yet. Nobody's ticket is looked at, and people wait at the front.
            party.ServiceLeft = -1;
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

        // At the front: each person in turn holds up their ticket and steps through. With more
        // than one person checking on the lane, that many tickets are looked at side by side.
        var checkers = CheckersOn(party.Gate, party.Lane);
        var seconds = Catalogue.Of(_site.Gates[party.Gate].Kind).SecondsPerPerson;
        var batch = Math.Min(checkers, party.Members.Count - party.Checked);
        var wait = Math.Max(1, (int)Math.Round((double)seconds * batch / checkers));
        if (party.ServiceLeft < 0)
        {
            party.ServiceLeft = wait;
        }
        else if (--party.ServiceLeft <= 0)
        {
            party.Checked += batch;
            if (party.Checked < party.Members.Count)
            {
                var next = Math.Min(checkers, party.Members.Count - party.Checked);
                party.ServiceLeft = Math.Max(1, (int)Math.Round((double)seconds * next / checkers));
                return;
            }

            var (gx, gy) = _site.LaneMouth(party.Gate, party.Lane, 0);
            LeaveQueue(party);
            if (_map.IsWalkable(FieldMap.TileAt(gx, gy)))
            {
                (party.X, party.Y) = (gx, gy);
            }

            party.Ticketed = true;
            TicketsSold?.Invoke(party.Members.Count);
            if (party.Camper && !TentsStayUp)
            {
                // The tent goes up: a good pitch cheers them, a poor one lets them down.
                party.Mood = Math.Clamp(party.Mood + CampFeel(party), 0, 1);
                CamperArrived?.Invoke(party);
            }

            Send(party, SomewhereInField(party.Random), PartyState.WalkingIn);
        }
    }

    /// <summary>Moves everyone on by one tick.</summary>
    public void Tick(DateTime now)
    {
        _now = now;
        OrderQueues();
        if (now.Minute != _measuredMinute)
        {
            _measuredMinute = now.Minute;
            Density.Measure(Loose());
            CountAreas(now);
        }

        var closing = now.TimeOfDay >= EveryoneOut;
        var headliner = Show?.Stage is null || Show.CalledOff is not null ? null : Show.Slots.FirstOrDefault(s => s.Headliner);
        foreach (var party in _parties)
        {
            if (party.State is PartyState.Standing or PartyState.Strolling or PartyState.Watching or PartyState.AtStall or PartyState.Sitting)
            {
                Feel(party, now);
            }

            if (!party.StayDecided && now >= party.Leaves && party.IsOnSite && party.State != PartyState.Leaving)
            {
                // Time to go home, unless the headliner is worth staying on for.
                party.StayDecided = true;
                if (party.Camper && party.Ticketed == true && headliner is not null && now < headliner.End && party.Mood >= 0.15)
                {
                    // With a tent to go back to there is no reason to miss the end.
                    party.Leaves = headliner.End + TimeSpan.FromMinutes(party.Random.Between(1, 45));
                }
                else if (headliner is not null && now < headliner.End && now.TimeOfDay >= StayOnFrom && party.Mood >= 0.3 && party.Random.NextDouble() < FanPull.ChanceToWatch(party.Id, headliner.Act.Genre, headliner.Act.Draw))
                {
                    // A big crowd takes longer to drift away at the end of the night.
                    party.Leaves = headliner.End + TimeSpan.FromMinutes(party.Random.Between(1, 14 + Math.Min(20, _parties.Count / 80.0)));
                }

                if (party.State == PartyState.Watching && ShowOf(party.Watch)?.Playing(now) is { } watched && watched.Index == party.Watch
                    && party.Leaves < watched.End + ApplauseFor(party) && party.Mood >= 0.15 && Performing.StaysToTheEnd(party.Id))
                {
                    // Most of those watching see the set out and clap the act off before they go.
                    party.Leaves = watched.End + ApplauseFor(party);
                }
            }

            if (party.HoldUntil != default && now >= party.HoldUntil)
            {
                // The food is eaten and the drinks are drunk. A bag of merchandise is kept all day.
                party.HoldUntil = default;
                foreach (var member in party.Members)
                {
                    if (!Keeps(member.Holding))
                    {
                        member.Holding = Carried.None;
                    }
                }
            }

            if (party.LitterAt != default && now >= party.LitterAt && party.IsOnSite)
            {
                // Anyone already on their way out takes it home with them.
                var pieces = _map.InFestival(FieldMap.TileAt(party.X, party.Y)) ? Math.Max(1, party.Members.Count / 2) : 0;
                if (pieces > 0 && Binned?.Invoke(party, pieces) == true)
                {
                    // There was a bin within a few steps, and it went in there.
                    pieces = 0;
                }

                for (var i = 0; i < pieces; i++)
                {
                    var angle = (party.Id * 2.4) + (party.Spent * 0.7) + (i * 2.1);
                    var (lx, ly) = (party.X + (Math.Cos(angle) * 0.6), party.Y + (Math.Sin(angle) * 0.6));
                    if (_map.InFestival(FieldMap.TileAt(lx, ly)))
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
                        if (party.Travel == Travel.Car && !_parties.Any(p => p != party && p.Bay == party.Bay && p.State != PartyState.Expected))
                        {
                            CarParked?.Invoke();
                        }

                        party.Heading = party.FromWest ? Math.PI / 2 : -Math.PI / 2;
                        Arrive(party);
                    }

                    break;

                case PartyState.Queuing:
                    if (now >= party.Leaves || closing)
                    {
                        LeaveQueue(party);
                        Send(party, Home(party), PartyState.Leaving);
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
                        Send(party, Home(party), PartyState.Leaving);
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
                        Send(party, Home(party), PartyState.Leaving);
                    }
                    else if (party.Wants is not null && SeekStall(party, now, NearTheEnd(party.Watch, now) || Applauding(party, now) ? HoldOnFor : Seeking))
                    {
                        // Something cannot wait for the end of the set.
                        party.Watch = -1;
                    }
                    else if (!StillOn(party.Watch, now))
                    {
                        if (Applauding(party, now))
                        {
                            // The set is over, but the act is still taking its applause.
                            break;
                        }

                        // The set is over. With the stage empty this is the moment for a drink, some food
                        // or the toilet, even for those who could have waited; the rest hang about, then wander off.
                        party.Watch = -1;
                        if (!SeekStall(party, now, InTheBreak))
                        {
                            party.State = PartyState.Standing;
                            party.WaitUntil = now + TimeSpan.FromMinutes(party.Random.Between(0.5, 4));
                        }
                    }

                    break;

                case PartyState.Standing:
                    if (now < party.RetryAt)
                    {
                        break;
                    }

                    if (party.Busker >= 0)
                    {
                        // The entertainer has had their time: move on.
                        party.Busker = -1;
                        party.WaitUntil = now;
                    }

                    if (now >= party.Leaves || closing)
                    {
                        party.Watch = -1;
                        Send(party, Home(party), PartyState.Leaving);
                    }
                    else if (SeekStall(party, now))
                    {
                        // Off to a stall.
                    }
                    else if (GoAndWatch(party, now))
                    {
                        // Off to the stage.
                    }
                    else if (Fancy(party, now))
                    {
                        // Off for a treat, or to the shops.
                    }
                    else if (now >= party.WaitUntil && Rest(party, now))
                    {
                        // Off for a sit down, or to get under cover.
                    }
                    else if (now >= party.WaitUntil)
                    {
                        Send(party, Wander(party), PartyState.Strolling);
                    }

                    if (party.State == PartyState.Standing && (now >= party.Leaves || closing))
                    {
                        // No way out just now: look again in a couple of minutes.
                        party.RetryAt = now + TimeSpan.FromMinutes(2);
                    }

                    break;

                case PartyState.Sitting:
                    Sit(party, now, closing);
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
                        else if (party.State == PartyState.Strolling && party.Busker >= 0)
                        {
                            // Got to the entertainer: stand and listen until it is time to move on.
                            party.State = PartyState.Standing;
                            party.Heading = Math.Atan2(party.BuskX - party.X, party.BuskY - party.Y) + (((party.Id % 7) - 3) * 0.05);
                            party.RetryAt = party.BuskUntil > now ? party.BuskUntil : now;
                            party.WaitUntil = party.RetryAt;
                        }
                        else if (party.State == PartyState.Strolling && Settle(party, now))
                        {
                            // Sat down, or stopped to wash their hands.
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
        var sheltered = Sheltered(party);
        party.Thirst = Math.Min(1.2, party.Thirst + (party.Appetite * (sheltered ? Math.Min(Heat, 1) : Heat) * perHour / HoursToThirsty));
        party.Toilet = Math.Min(1.2, party.Toilet + (party.Appetite * perHour / HoursToToilet));

        // Waiting at the front for a big act to come on is not watching one yet.
        var waiting = party.State == PartyState.Watching && ShowOf(party.Watch)?.FillingUp(now)?.Index == party.Watch;
        var change = party.State == PartyState.Watching && !waiting && ShowOf(party.Watch)?.Playing(now)?.Act is { } act
            ? Enjoyed(act.Rating) * FanPull.Enjoyment(party.Id, act.Genre) * ShareOfTheAct(party) * perHour
            : 0;
        foreach (var need in new[] { party.Hunger, party.Thirst, party.Toilet })
        {
            if (need >= Pressing)
            {
                change -= (need >= 1 ? 0.06 : 0.03) * perHour;
            }
        }

        if (party.State == PartyState.Sitting)
        {
            // A sit down does everyone good.
            change += SitPerHour * perHour;
        }

        if (_site.Decorations.Count > 0 || _site.Bunting.Count > 0)
        {
            // A field that has been dressed up is a nicer place to be.
            change += _site.CharmAt(party.X, party.Y, Night) * Decor.CharmPerHour * perHour;
        }

        if (party.ListeningToBusker)
        {
            // A song from one of the entertainers brightens the day.
            change += Busking.MoodPerHour * perHour;
        }

        if (Rain > 0.05 && !sheltered)
        {
            // Rain is miserable without a poncho and only a nuisance with one.
            var covered = (double)party.Members.Count(m => m.Look.Poncho is not null) / party.Members.Count;
            change -= Rain * ((covered * PonchoPerHour) + ((1 - covered) * SoakedPerHour)) * perHour;
        }

        if (Mud is not null && party.State is not (PartyState.Queuing or PartyState.AtStall))
        {
            change -= Mud(party.X, party.Y) * MudPerHour * perHour;
        }

        change += VipFeel(party) * perHour;
        if (Darkness is not null && MindsTheDark(party))
        {
            // Stumbling about an unlit field at night is no fun, and does not feel safe.
            change -= Darkness(party.X, party.Y) * DarkPerHour * perHour;
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
    private bool SeekStall(Party party, DateTime now, double from = Seeking)
    {
        if (now < party.NeedRetryAt || _site.Stalls.Count == 0)
        {
            return false;
        }

        // The most pressing need that some stall here can answer at a price they will pay.
        Need? wanted = null;
        var tooDear = false;
        foreach (var candidate in new[] { Need.Toilet, Need.Drink, Need.Food })
        {
            if (party.Level(candidate) < from)
            {
                continue;
            }

            var any = false;
            var affordable = false;
            foreach (var s in _site.Stalls)
            {
                if (Catalogue.Of(s.Kind).Serves == candidate && MayUse(party, s))
                {
                    any = true;
                    affordable = affordable || WorthIt(party, s);
                }
            }

            tooDear = tooDear || (any && !affordable);
            if (affordable && (wanted is null || party.Level(candidate) > party.Level(wanted.Value)))
            {
                wanted = candidate;
            }
        }

        if (tooDear && now >= party.GrumbleAt && party.Ticketed is not null)
        {
            // There is food or drink here, but not at a price they will pay. They go without, and they mind.
            party.GrumbleAt = now + TimeSpan.FromMinutes(30);
            party.Mood = Math.Max(0, party.Mood - 0.03);
            PricedOut += party.Members.Count;
        }

        if (wanted is not { } need)
        {
            return false;
        }

        var best = -1;
        var bestScore = double.MaxValue;
        for (var i = 0; i < _site.Stalls.Count; i++)
        {
            var stall = _site.Stalls[i];
            var info = Catalogue.Of(stall.Kind);
            if (info.Serves != need || _stallQueues[i] is not { } queue || !WorthIt(party, stall) || !MayUse(party, stall))
            {
                continue;
            }

            var score = WaitAndWalk(party, i, queue, info) + Liking(party, stall, now) - (party.Vip && stall.ForVips ? VipPreference : 0);
            if (score < bestScore)
            {
                (best, bestScore) = (i, score);
            }
        }

        return best >= 0 && JoinStall(party, best, now, fancy: false);
    }

    /// <summary>How long a stall would take a party, in seconds: the queue, the people being served and the walk there.</summary>
    private double WaitAndWalk(Party party, int stall, QueueLine queue, StallInfo info)
    {
        var (x, y, _) = queue.At(Entry(queue, queue.People));
        var walk = Math.Abs(x - party.X) + Math.Abs(y - party.Y);
        // Somebody who has just read a finger post or the site map minds a longer walk less.
        if (_site.Signs.Count > 0 && _site.WayShown(party.X, party.Y))
        {
            walk *= Signs.ShownWalk;
        }

        return ((queue.People + (_serving[stall].Count(p => p is not null) * 2.2)) * info.SecondsPerPerson / Math.Max(1, info.Servers)) + (walk / party.Speed);
    }

    /// <summary>
    /// How much less a party likes one stall than another, counted as seconds of extra wait they
    /// would put up with to go elsewhere: dear prices, their own tastes, and whether it suits the
    /// weather and the time of day.
    /// </summary>
    private double Liking(Party party, PlacedStall stall, DateTime now)
    {
        if (!Catalogue.Sells(stall.Kind))
        {
            // The tap is free, but some people would always rather have a proper drink. The toilets are just the toilets.
            return stall.Kind == StallKind.WaterPoint ? 60 + (150 * Roll(party.Id, 31, (int)stall.Kind)) : 0;
        }

        // Someone properly hungry or thirsty wants a meal or a long drink: a snack is a poor second.
        var menu = Catalogue.MenuOf(stall.Kind);
        var snack = 1 - Math.Min(1, menu.Max(m => m.Fills));
        var score = (Cheapest(party, stall) * 60) + (300 * Roll(party.Id, 31, (int)stall.Kind)) + (snack * 300);
        var hot = Heat >= 1.5;
        var wet = Rain > 0.05;
        var hour = now.TimeOfDay.TotalHours;
        score += stall.Kind switch
        {
            StallKind.IceCream or StallKind.Slush => (hot ? -80 : 0) + (wet ? 200 : 0) + (stall.Kind == StallKind.Slush ? (party.HasChild ? -40 : 60) : 0),
            StallKind.Coffee => (hour < 12 ? -120 : 0) + (wet ? -120 : 0) + (hot ? 150 : 0),
            StallKind.Cocktails => (party.HasChild ? 200 : 0) + (hour >= 17 ? -100 : 0),
            StallKind.Bar => party.HasChild ? 60 : 0,
            _ => 0,
        };
        return score;
    }

    private static bool GrownUp(PartyMember member) => member.Look.Age is AgeGroup.Adult or AgeGroup.Older;

    /// <summary>Whether one person may buy one thing: no beer for children, and children's sizes only for children.</summary>
    private static bool MayBuy(PartyMember member, StallItem item) =>
        (!item.AdultsOnly || GrownUp(member)) && (!item.ForChildren || member.Look.Age == AgeGroup.Child);

    /// <summary>Whether one person has any use for something that is worn: nobody buys a second poncho or hat.</summary>
    private static bool HasUseFor(PartyMember member, StallItem item) => item.Wears switch
    {
        Worn.Poncho => member.Look.Poncho is null,
        Worn.BucketHat => member.Look.Hat == Hat.None && member.Look.Poncho is null,
        _ => true,
    };

    /// <summary>
    /// How dear the best-priced thing on a stall is for a party, against its usual price: 1 is the
    /// usual price. Only things somebody in the party could buy count.
    /// </summary>
    public double Cheapest(Party party, PlacedStall stall)
    {
        var menu = Catalogue.MenuOf(stall.Kind);
        var best = double.MaxValue;
        for (var i = 0; i < menu.Count; i++)
        {
            var dear = stall.Dearness(i);
            if (dear < best && Wanted(party, menu[i]))
            {
                best = dear;
            }
        }

        return best;
    }

    private static bool Wanted(Party party, StallItem item)
    {
        foreach (var member in party.Members)
        {
            if (MayBuy(member, item) && HasUseFor(member, item))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether a stall has anything a party would pay for. The toilets and the taps always do.</summary>
    public bool WorthIt(Party party, PlacedStall stall) => !Catalogue.Sells(stall.Kind) || Cheapest(party, stall) <= party.Stretch;

    /// <summary>How many more (or fewer) people fancy a treat as its price goes down (or up): 1 at the usual price.</summary>
    public static double Tempted(double dearness) => Math.Clamp(1 + ((1 - dearness) * 1.2), 0.2, 1.8);

    /// <summary>How often a party with nothing pressing thinks about a treat or the shops, in minutes.</summary>
    public const double FancyEveryMinutes = 15;

    /// <summary>
    /// The chance that a party, thinking about it, fancies what a stall sells just now. Ponchos go
    /// when it rains, hats in hot sun, merchandise to people having a good day, and treats (ice
    /// cream, slush, coffee, cocktails) when the weather and the hour suit them.
    /// </summary>
    public double Appeal(Party party, PlacedStall stall, DateTime now)
    {
        var hot = Heat >= 1.5;
        var wet = Rain > 0.05;
        var hour = now.TimeOfDay.TotalHours;
        switch (stall.Kind)
        {
            case StallKind.Ponchos:
                return wet ? 0.7 : DressedForRain ? 0.04 : 0;
            case StallKind.BucketHats:
                return party.BoughtHats || wet ? 0 : hot ? 0.035 : 0.01;
            case StallKind.Merchandise:
                return party.BoughtMerch || party.Mood < 0.6 ? 0 : 0.012 + (party.DecidedAny ? 0.018 : 0);
            case StallKind.IceCream:
                return party.Hunger < 0.2 ? 0 : 0.03 * (hot ? 2.5 : 1) * (wet ? 0.2 : 1) * (party.HasChild ? 1.5 : 1);
            case StallKind.Slush:
                return party.Thirst < 0.25 ? 0 : 0.02 * (hot ? 2.5 : 1) * (wet ? 0.2 : 1) * (party.HasChild ? 2 : 1);
            case StallKind.Coffee:
                return party.Thirst < 0.25 ? 0 : 0.02 * (hour < 12 ? 2 : 1) * (wet ? 2 : 1) * (hot ? 0.3 : 1);
            case StallKind.Cocktails:
                return (party.Thirst < 0.25 || !party.Members.Any(GrownUp) ? 0 : 0.015 * (hour >= 17 ? 2.5 : 1) * (party.HasChild ? 0.3 : 1)) + (AnotherRound(party, stall, now) * 0.5);
            case StallKind.Bar:
                return AnotherRound(party, stall, now);
            case StallKind.PhoneCharging:
                // Batteries run down as the day goes on.
                return party.Charged ? 0 : hour < 12 ? 0.004 : hour < 16 ? 0.012 : 0.02;
            case StallKind.ShowerBlock or StallKind.CampInfo or StallKind.CampShop:
                return CampAppeal(party, stall.Kind, hour, hot, wet);
            case StallKind.VipLounge:
                return !party.Vip || party.Thirst < 0.15 ? 0 : 0.05 * (hour >= 17 ? 1.5 : 1);
            case StallKind.BigWheel or StallKind.Waltzer or StallKind.Twister or StallKind.Carousel or StallKind.Skylark or StallKind.HookADuck:
                // The fairground is for people having a good enough day, less so in the rain, and the novelty wears off.
                return party.Mood < 0.3 ? 0 : Rides.Pull(stall.Kind, party.HasChild) * (wet ? 0.3 : 1) / (1 + (0.7 * party.FairGoes));
            default:
                return 0;
        }
    }

    /// <summary>
    /// The extra pull of a bargain: food or drink well under its usual price tempts people who are
    /// only a little hungry or thirsty and would otherwise have waited.
    /// </summary>
    public double Bargain(Party party, PlacedStall stall)
    {
        if (Catalogue.Of(stall.Kind).Serves is not { } need || !Catalogue.Sells(stall.Kind))
        {
            return 0;
        }

        var wanting = need == Need.Food ? party.Hunger : party.Thirst;
        return wanting < 0.3 ? 0 : Math.Max(0, 0.9 - Cheapest(party, stall)) * 0.15;
    }

    /// <summary>
    /// Now and then a party with nothing pressing fancies a treat or a look round the shops. The
    /// cheaper a stall, the more people it tempts. True if they set off for one.
    /// </summary>
    private bool Fancy(Party party, DateTime now)
    {
        if (now < party.FancyAt || now < party.NeedRetryAt || party.Ticketed is null || !_sellers)
        {
            return false;
        }

        var moment = (int)(now.TimeOfDay.TotalMinutes / FancyEveryMinutes);
        party.FancyAt = now + TimeSpan.FromMinutes(FancyEveryMinutes * (0.7 + (0.6 * Roll(party.Id, 41, moment))));
        var best = -1;
        var bestScore = double.MaxValue;
        for (var i = 0; i < _site.Stalls.Count; i++)
        {
            var stall = _site.Stalls[i];
            if (!Catalogue.Sells(stall.Kind) || !MayUse(party, stall) || _stallQueues[i] is not { } queue || queue.People > 8)
            {
                continue;
            }

            var appeal = Appeal(party, stall, now) + Bargain(party, stall);
            if (_site.Signs.Count > 0 && _site.Advertised(stall))
            {
                // A sandwich board out the front catches the eye.
                appeal *= Signs.BoardPull;
            }

            if (appeal <= 0 || !WorthIt(party, stall))
            {
                continue;
            }

            // One throw of the dice for each kind of stall, so two ice cream vans do not double the urge.
            if (Roll(party.Id, 43 + (int)stall.Kind, moment) >= appeal * Tempted(Cheapest(party, stall)))
            {
                continue;
            }

            var score = WaitAndWalk(party, i, queue, Catalogue.Of(stall.Kind));
            if (score < bestScore)
            {
                (best, bestScore) = (i, score);
            }
        }

        return best >= 0 && JoinStall(party, best, now, fancy: true);
    }

    /// <summary>Sends a party to the back of a stall's queue. False if they cannot get there.</summary>
    private bool JoinStall(Party party, int best, DateTime now, bool fancy)
    {
        var line = _stallQueues[best]!;
        var door = Approach(line, Entry(line, line.People));
        var path = Route(party, FieldMap.TileAt(party.X, party.Y), door);
        if (path is null)
        {
            // Cannot get there (fenced off, perhaps): try again in a while.
            if (!fancy)
            {
                party.NeedRetryAt = now + TimeSpan.FromMinutes(10);
            }

            return false;
        }

        line.Parties.Add(party);
        party.Stall = best;
        party.Server = -1;
        party.InLine = false;
        party.Rejoins = 0;
        party.ServiceLeft = -1;
        party.QueuedSince = now;
        party.Target = door;
        party.Path = path;
        party.PathIndex = 0;
        party.Moving = true;
        party.Fancying = fancy;
        party.State = PartyState.AtStall;
        return true;
    }

    private void LeaveStall(Party party)
    {
        if (party.InLine && party.Server < 0)
        {
            StepOut(party);
        }

        if (party.Stall >= 0 && party.Stall < _stallQueues.Count)
        {
            _stallQueues[party.Stall]?.Parties.Remove(party);
            var serving = _serving[party.Stall];
            for (var k = 0; k < serving.Length; k++)
            {
                if (ReferenceEquals(serving[k], party))
                {
                    serving[k] = null;
                }
            }
        }

        if (party.Cubicles is not null && party.Stall >= 0 && party.Stall < _cubicles.Count)
        {
            var cubicles = _cubicles[party.Stall];
            for (var k = 0; k < cubicles.Length; k++)
            {
                if (ReferenceEquals(cubicles[k]?.Party, party))
                {
                    cubicles[k] = null;
                }
            }
        }

        party.Stall = -1;
        party.Server = -1;
        party.InLine = false;
        party.Checked = 0;
        party.Cubicles = null;
        party.Rejoins = 0;
        party.ServiceLeft = -1;
        party.Moving = false;
        party.Fancying = false;
        party.Path = null;
    }

    /// <summary>How far out from its door someone stands as it opens, and how far out those who have been wait.</summary>
    private const double DoorMetres = 0.9;
    private const double WaitMetres = 1.8;

    /// <summary>How long a cubicle door stands open as someone goes in or comes out, in seconds.</summary>
    public const int DoorSeconds = 14;

    /// <summary>
    /// Where the first of a party at the toilets waits once they have been: off to one side and
    /// well clear of the doors and the queue, each party in a row of its own. The rest of the
    /// party line up beside them.
    /// </summary>
    private (double X, double Y) Waiting(Party party)
    {
        var stall = _site.Stalls[party.Stall];
        var info = Catalogue.Of(stall.Kind);
        var (fx, fy, dx, dy) = SiteLayout.Front(stall.Rect, stall.Facing);
        var across = -((info.Width * FieldMap.TileMetres / 2) + 0.6);
        var metresOut = 2.4 + (party.WaitRow * 1.3);
        var (x, y) = (fx + (dx * metresOut) - (dy * across), fy + (dy * metresOut) + (dx * across));
        if (_map.IsWalkable(FieldMap.TileAt(x, y)))
        {
            return (x, y);
        }

        // Something is in the way there: wait in front of the cabins instead.
        return (fx + (dx * WaitMetres) + (dy * 0.6 * ((party.Members.Count - 1) / 2.0)), fy + (dy * WaitMetres) - (dx * 0.6 * ((party.Members.Count - 1) / 2.0)));
    }

    /// <summary>The first row beside a toilet that no other party is gathering in.</summary>
    private int FreeWaitRow(int stall)
    {
        for (var row = 0; ; row++)
        {
            if (!_parties.Any(p => p.Stall == stall && p.State == PartyState.AtStall && p.Cubicles is not null && p.WaitRow == row))
            {
                return row;
            }
        }
    }

    /// <summary>
    /// At the toilets people go in one at a time. Whoever is first in the queue walks to a free
    /// cubicle, the door opens, they go in and it shuts behind them; when they come out the door
    /// opens again and they wait nearby for the rest of their party.
    /// </summary>
    private void UseToilets(Party party, PlacedStall stall, StallInfo info, QueueLine line, DateTime now)
    {
        var cubicles = _cubicles[party.Stall];
        if (party.Server < 0)
        {
            // People only wait so long once they have joined the queue, unless some of them have already gone in.
            if (party.InLine && party.Checked == 0 && now - party.QueuedSince > TimeSpan.FromMinutes(PatienceMinutes))
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
                party.Moving = true;
                if (!Walk(party))
                {
                    return;
                }
            }

            if (!party.InLine)
            {
                if (!Join(party, line))
                {
                    return;
                }

                party.QueuedSince = now;
            }

            Shuffle(party, line);
            var free = party.LineTarget == 0 && party.LinePos < 0.6 ? Array.IndexOf(cubicles, null) : -1;
            if (free >= 0)
            {
                if (party.Cubicles is null)
                {
                    party.WaitRow = FreeWaitRow(party.Stall);
                    party.Cubicles = Enumerable.Repeat(-1, party.Members.Count).ToArray();
                }

                var (x, y, heading) = line.At(party.LinePos);
                cubicles[free] = new CubicleUse(party, party.Checked) { X = x, Y = y, Heading = heading };
                party.Cubicles[party.Checked] = free;
                party.Checked++;
                if (party.Checked == party.Members.Count)
                {
                    // The last of them has left the queue: the next party is now first.
                    line.Parties.Remove(party);
                    party.InLine = false;
                    party.Server = 0;
                    party.Moving = false;
                    (party.X, party.Y) = Waiting(party);
                }
            }
        }

        var busy = false;
        for (var k = 0; k < cubicles.Length; k++)
        {
            if (cubicles[k] is not { } use || !ReferenceEquals(use.Party, party))
            {
                continue;
            }

            if (Step(use, stall, k, info))
            {
                cubicles[k] = null;
                party.Cubicles![use.Member] = -2;
            }
            else
            {
                busy = true;
            }
        }

        if (party.Server >= 0 && !busy)
        {
            Finish(party, stall, info, now);
        }
    }

    /// <summary>Moves one person on through their visit to a cubicle. True once they are out and the door has shut.</summary>
    private static bool Step(CubicleUse use, PlacedStall stall, int cubicle, StallInfo info)
    {
        var (tx, ty) = SiteLayout.CubicleDoor(stall, cubicle, use.Phase == CubiclePhase.Entering ? -0.6 : DoorMetres);
        if (use.Phase != CubiclePhase.Inside)
        {
            var dx = tx - use.X;
            var dy = ty - use.Y;
            var distance = Math.Sqrt((dx * dx) + (dy * dy));
            if (distance > 0.02)
            {
                var step = Math.Min(distance, use.Party.Speed);
                use.X += dx / distance * step;
                use.Y += dy / distance * step;
                use.Heading = Math.Atan2(dx, dy);
            }

            if (use.Phase == CubiclePhase.Approaching)
            {
                if (distance <= 0.02)
                {
                    use.Phase = CubiclePhase.Entering;
                    use.Left = DoorSeconds;
                }

                return false;
            }
        }

        if (--use.Left > 0)
        {
            return false;
        }

        switch (use.Phase)
        {
            case CubiclePhase.Entering:
                // The walk there and the door take time too, so the visit itself is that much shorter.
                use.Phase = CubiclePhase.Inside;
                use.Left = Math.Max(10, info.SecondsPerPerson - (2 * DoorSeconds) - 8);
                return false;
            case CubiclePhase.Inside:
                use.Phase = CubiclePhase.Leaving;
                use.Left = DoorSeconds;
                return false;
            default:
                return true;
        }
    }

    private void QueueAtStall(Party party, DateTime now)
    {
        if (party.Stall < 0 || party.Stall >= _stallQueues.Count || _stallQueues[party.Stall] is not { } line)
        {
            LeaveStall(party);
            party.State = PartyState.Standing;
            return;
        }

        var stall = _site.Stalls[party.Stall];
        var info = Catalogue.Of(stall.Kind);
        if (info.Serves == Need.Toilet)
        {
            UseToilets(party, stall, info, line, now);
            return;
        }

        if (party.Server < 0)
        {
            // People only wait so long once they have joined the queue.
            if (party.InLine && now - party.QueuedSince > TimeSpan.FromMinutes(PatienceMinutes))
            {
                // Someone who only fancied a treat, and is not by now properly hungry or thirsty, just shrugs.
                var fancied = party.Fancying && !(info.Serves is { } served && party.Level(served) >= Seeking);
                LeaveStall(party);
                if (!fancied)
                {
                    // Giving up on a treat is a shrug. Giving up on a meal spoils the day.
                    party.GaveUp++;
                    party.Mood = Math.Max(0, party.Mood - 0.08);
                    party.NeedRetryAt = now + TimeSpan.FromMinutes(15);
                }

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

            if (!party.InLine)
            {
                if (!Join(party, line))
                {
                    return;
                }

                party.QueuedSince = now;
            }

            // First in the queue: as soon as whoever is serving is free, step up to them, without
            // first walking to the very front.
            var free = party.LineTarget == 0 ? Array.IndexOf(_serving[party.Stall], null) : -1;
            Shuffle(party, line);
            var ride = Rides.IsRide(stall.Kind);
            var turn = ride ? Rides.TurnAt(stall, now) : default;
            if (free < 0 || (ride && !turn.Boarding))
            {
                // A ride only lets people on while it is standing still.
                return;
            }

            line.Parties.Remove(party);
            _serving[party.Stall][free] = party;
            party.InLine = false;
            party.Server = free;
            // Everyone on a ride gets off together when it stops.
            party.ServiceLeft = ride ? turn.Left : info.SecondsPerPerson * party.Members.Count;
        }

        // Walk up to the counter. The serving starts as they set off, so nobody is kept waiting by the walk.
        var (cx, cy) = SiteLayout.ServingPlace(stall, party.Server, CounterMetres);
        var dx = cx - party.X;
        var dy = cy - party.Y;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        party.Moving = distance > 0.05;
        if (party.Moving)
        {
            var step = Math.Min(distance, party.Speed);
            party.X += dx / distance * step;
            party.Y += dy / distance * step;
            party.Heading = Math.Atan2(dx, dy);
        }

        if (--party.ServiceLeft <= 0 && !party.Moving)
        {
            Finish(party, stall, info, now);
        }
    }

    /// <summary>
    /// A party has been served. At a stall with a menu each of them picks something they will pay
    /// for, pays for it and walks off with it in their hand (or on their head). The need is
    /// answered, and how fair the prices were shows in their mood.
    /// </summary>
    private void Finish(Party party, PlacedStall stall, StallInfo info, DateTime now)
    {
        var fancied = party.Fancying;
        LeaveStall(party);
        var menu = Catalogue.MenuOf(stall.Kind);
        var paid = 0;
        var buyers = 0;
        var filled = 0.0;
        var dearness = 0.0;
        var handful = false;
        var rounds = 0;
        for (var m = 0; m < party.Members.Count && menu.Count > 0; m++)
        {
            var member = party.Members[m];
            var pick = Pick(party, member, stall, menu, now);
            if (pick < 0)
            {
                continue;
            }

            var item = menu[pick];
            var price = stall.PriceOf(pick);
            paid += price;
            buyers++;
            filled += item.Fills;
            dearness += stall.Dearness(pick);
            if (item.Gives != Carried.None && (!Keeps(member.Holding) || item.Gives == Carried.Bag) && (member.Holding != Carried.Teddy))
            {
                member.Holding = item.Gives;
                handful = handful || item.Gives != Carried.Bag;
            }

            PutOn(member, item, pick);
            Sold?.Invoke(stall, pick, price);
            rounds += Catalogue.IsAlcohol(stall.Kind, item) ? 1 : 0;
        }

        if (rounds > 0)
        {
            Drank(party, rounds);
        }
        else if (stall.NoDrunks && party.Drink >= HadEnoughAt && Catalogue.SellsAlcohol(stall.Kind))
        {
            // Told they have had enough. They are not pleased about it, and do not try again for a while.
            Refused++;
            party.Mood = Math.Max(0, party.Mood - 0.02);
            var later = now + TimeSpan.FromMinutes(40);
            party.FancyAt = party.FancyAt > later ? party.FancyAt : later;
        }

        var share = buyers == 0 ? 1 : filled / buyers;
        switch (info.Serves)
        {
            case Need.Food when menu.Count == 0 || buyers > 0:
                // Fries or an ice cream are not a meal.
                party.Hunger = Math.Max(0, party.Hunger - (menu.Count == 0 ? 1.2 : share * 1.2));
                Lined(party, 0.5 * share);
                break;
            case Need.Drink when menu.Count == 0 || buyers > 0:
                party.Thirst = Math.Max(0, party.Thirst - (menu.Count == 0 ? 1.2 : share * 1.2));
                party.Toilet = Math.Min(1.2, party.Toilet + (0.2 * (menu.Count == 0 ? 1 : share)));
                if (rounds == 0)
                {
                    // Water, or a soft drink.
                    Lined(party, 0.3);
                }

                break;
            case Need.Toilet:
                party.Toilet = 0;
                break;
        }

        if (Rides.IsFair(stall.Kind) && buyers > 0)
        {
            party.FairGoes++;
            party.Mood = Math.Clamp(party.Mood + Rides.Thrill, 0, 1);
            if (stall.Kind == StallKind.HookADuck && Roll(party.Id, 61, (int)now.TimeOfDay.TotalMinutes) < Rides.TeddyChance)
            {
                // The teddy goes to a child if there is one, and to whoever has a hand free.
                var winner = party.Members.Where(m => !Keeps(m.Holding)).OrderBy(m => m.Look.Age == AgeGroup.Child ? 0 : 1).FirstOrDefault();
                if (winner is not null)
                {
                    winner.Holding = Carried.Teddy;
                    party.Mood = Math.Clamp(party.Mood + Rides.Thrill, 0, 1);
                    TeddyWon?.Invoke(stall);
                }
            }
        }

        switch (stall.Kind)
        {
            case StallKind.BucketHats:
                party.BoughtHats = true;
                break;
            case StallKind.Merchandise:
                party.BoughtMerch = true;
                break;
            case StallKind.ShowerBlock:
                party.Showered = true;
                break;
            case StallKind.CampInfo:
                party.Lockered = true;
                break;
            case StallKind.CampShop:
                party.CampShopped = true;
                break;
        }

        party.Spent += paid;
        if (buyers > 0 || menu.Count == 0)
        {
            // Being served is a small pleasure; a bargain is a bigger one, and being overcharged takes it away again.
            var value = buyers == 0 ? 0 : Math.Clamp((1 - (dearness / buyers)) * 0.04, -0.04, 0.02);
            var treat = fancied || stall.Kind == StallKind.Merchandise ? 0.008 : 0;
            party.Mood = Math.Clamp(party.Mood + 0.006 + value + treat, 0, 1);
        }

        party.State = PartyState.Standing;
        party.RetryAt = default;
        party.WaitUntil = now + TimeSpan.FromMinutes(party.Random.Between(1, 5));
        if (handful)
        {
            // It takes a few minutes to eat or drink. Then the wrapper or the cup is rubbish, and some people just let go of it.
            party.HoldUntil = now + TimeSpan.FromMinutes(4 + ((party.Id + party.Spent) % 14));
            if (Untidy(party))
            {
                party.LitterAt = party.HoldUntil;
            }
        }

        if (stall.Kind == StallKind.PhoneCharging && buyers > 0)
        {
            party.Charged = true;
        }

        Served?.Invoke(stall.Kind, party.Members.Count, paid);
        if (info.Serves == Need.Toilet)
        {
            GoAndWash(party, now);
        }
        else if (handful && info.Serves is not null)
        {
            SitToEat(party, now);
        }
    }

    /// <summary>
    /// What one person picks from a stall's menu: something they are allowed, have a use for and
    /// will pay the price of, leaning towards whatever is the best value. -1 if nothing suits.
    /// </summary>
    private int Pick(Party party, PartyMember member, PlacedStall stall, IReadOnlyList<StallItem> menu, DateTime now)
    {
        Span<double> weights = stackalloc double[menu.Count];
        var total = 0.0;
        for (var i = 0; i < menu.Count; i++)
        {
            var dear = stall.Dearness(i);
            if (!MayBuy(member, menu[i]) || !HasUseFor(member, menu[i]) || dear > party.Stretch)
            {
                continue;
            }

            if (menu[i].Gives == Carried.Bag && member.Look.Age == AgeGroup.Child)
            {
                // The grown-ups carry the shopping.
                continue;
            }

            if (stall.NoDrunks && party.Drink >= HadEnoughAt && Catalogue.IsAlcohol(stall.Kind, menu[i]))
            {
                // This bar does not serve anyone who has had too many.
                continue;
            }

            weights[i] = (menu[i].ForChildren ? 4 : 1) / Math.Max(0.05, dear * dear);
            total += weights[i];
        }

        if (total <= 0)
        {
            return -1;
        }

        if (stall.Kind == StallKind.PhoneCharging && member != party.Members[0] && Roll(member.Id, 55, party.Id) > 0.35)
        {
            // One phone between them is usually enough.
            return -1;
        }

        if (stall.Kind is StallKind.CampInfo or StallKind.CampShop && member != party.Members[0] && Roll(member.Id, 57, party.Id) > 0.3)
        {
            // One locker, one bag of shopping, between them.
            return -1;
        }

        if (stall.Kind == StallKind.Merchandise && member != party.Members[0] && Roll(member.Id, 53, party.Id) > 0.5)
        {
            // Not everyone in a party buys a souvenir.
            return -1;
        }

        var roll = Roll(member.Id, 47 + (int)stall.Kind, (int)now.TimeOfDay.TotalMinutes) * total;
        for (var i = 0; i < menu.Count; i++)
        {
            roll -= weights[i];
            if (weights[i] > 0 && roll <= 0)
            {
                return i;
            }
        }

        for (var i = 0; i < menu.Count; i++)
        {
            if (weights[i] > 0)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The colours bucket hats come in at the hat stall.</summary>
    public static readonly string[] HatStallColours = { "#F6B93B", "#E94F7B", "#2FB7A8", "#F6F0E2", "#6FC3E8", "#C59BE0", "#FF7A45", "#7FD6B8" };

    /// <summary>Puts a poncho or a bucket hat on someone who has just bought one.</summary>
    private static void PutOn(PartyMember member, StallItem item, int pick)
    {
        switch (item.Wears)
        {
            case Worn.Poncho:
                member.Look = member.Look with { Poncho = LookGenerator.PonchoColours[member.Id % LookGenerator.PonchoColours.Length] };
                member.Bought |= 1;
                break;
            case Worn.BucketHat:
                member.Look = member.Look with { Hat = Hat.Bucket, HatColour = HatStallColours[member.Id % HatStallColours.Length] };
                member.Bought |= 2;
                break;
        }

        _ = pick;
    }

    /// <summary>Puts back what someone had bought and put on, after loading a save.</summary>
    private static void PutBackOn(PartyMember member, int bought)
    {
        if ((bought & 2) != 0 && (member.Bought & 2) == 0)
        {
            member.Look = member.Look with { Hat = Hat.Bucket, HatColour = HatStallColours[member.Id % HatStallColours.Length] };
        }

        if ((bought & 1) != 0 && member.Look.Poncho is null)
        {
            member.Look = member.Look with { Poncho = LookGenerator.PonchoColours[member.Id % LookGenerator.PonchoColours.Length] };
        }

        member.Bought |= bought;
    }

    /// <summary>
    /// When a set is on, a party that has not yet made up its mind about it decides whether to go
    /// and watch. True if they set off for the stage.
    /// </summary>
    private bool GoAndWatch(Party party, DateTime now)
    {
        for (var i = 0; i < _shows.Length; i++)
        {
            if (GoAndWatch(party, now, _shows[i], i))
            {
                return true;
            }
        }

        return false;
    }

    private bool GoAndWatch(Party party, DateTime now, RunningOrder? show, int number)
    {
        // For an act with a big following people start making their way to the front before the set.
        if (show?.Drawing(now) is not { } slot || show.Stage is not { } stage || party.DecidedOn(number) == slot.Index)
        {
            return false;
        }

        party.Decide(number, slot.Index);
        if (slot.Index != _claimSlot[number])
        {
            // A new act: everyone who wants to watch finds a place afresh.
            _claimSlot[number] = slot.Index;
            Array.Clear(_claims[number]);
        }

        // Fans of the sort of music flock to it; those who cannot stand it mostly stay away.
        if (party.Random.NextDouble() >= FanPull.ChanceToWatch(party.Id, slot.Act.Genre, slot.Act.Draw))
        {
            return false;
        }

        // VIP guests watch from a VIP platform near the stage if there is room on it, or from a VIP area.
        var deck = party.Vip ? PlatformSpot(party, stage) : null;
        var vipArea = party.Vip && deck is null ? VipAreaSpot(party, stage) : null;
        var chosen = deck ?? vipArea ?? PlaceToWatch(stage, party.Random, slot.Act.Draw, party.Members.Count);
        var waitFor = -1;
        if (deck is null && vipArea is null && _turnedAway >= 0 && slot.Act.Draw >= KeenToWait && party.Random.NextDouble() < 0.6 && WaitByGap(_turnedAway, party) is { } wait)
        {
            // A keen fan turned away from a full pen waits by its gap to be let in.
            (chosen, waitFor) = (wait, _turnedAway);
        }

        if (chosen is not { } spot)
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

        if (deck is null)
        {
            Claim(spot, party.Members.Count, number);
        }

        if (waitFor >= 0)
        {
            party.WaitFor = waitFor;
            _waitingAt[waitFor] += party.Members.Count;
        }
        else
        {
            NoteInside(spot, party.Members.Count);
        }

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
    private double Pace(Party party) => (1 - (0.5 * (Mud?.Invoke(party.X, party.Y) ?? 0))) * (Footing?.Invoke(party.X, party.Y) ?? 1) * (Density.StatusAt(party.X, party.Y) switch
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
        var claimed = joining;
        foreach (var claims in _claims)
        {
            claimed += claims[square];
        }

        return claimed / (double)(DensityMap.SquareMetres * DensityMap.SquareMetres);
    }

    /// <summary>The places people have picked to watch from, as written to a save file.</summary>
    public WatchingPlaces SavePlaces() => new(_claimSlot[0], Sparse(_claims[0]), _claimSlot[1], Sparse(_claims[1]))
    {
        Density = Density.Save(),
        MeasuredMinute = _measuredMinute,
        MoreSlots = _claimSlot.Skip(2).ToList(),
        MoreClaims = _claims.Skip(2).Select(c => (Dictionary<int, int>?)Sparse(c)).ToList(),
    };

    public void RestorePlaces(WatchingPlaces? places)
    {
        if (places?.Density is { } density)
        {
            // The crowd was last counted on the minute; counted again now it would walk a little differently.
            Density.Restore(density);
            _measuredMinute = places.MeasuredMinute;
        }

        var slots = new List<int> { places?.Slot ?? -1, places?.SlotSecond ?? -1 };
        var claims = new List<Dictionary<int, int>?> { places?.Claims, places?.ClaimsSecond };
        slots.AddRange(places?.MoreSlots ?? new());
        claims.AddRange(places?.MoreClaims ?? new());
        for (var i = 0; i < _claims.Length; i++)
        {
            Array.Clear(_claims[i]);
            _claimSlot[i] = i < slots.Count ? slots[i] : -1;
            foreach (var (square, people) in (i < claims.Count ? claims[i] : null) ?? new())
            {
                if (square >= 0 && square < _claims[i].Length)
                {
                    _claims[i][square] = people;
                }
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

    /// <summary>Notes that people have picked a place to watch from, for a stage's act (0 for the main stage).</summary>
    private void Claim(TilePoint tile, int people, int stage)
    {
        var (x, y) = FieldMap.CentreOf(tile);
        var (sx, sy) = Density.SquareOf(x, y);
        _claims[Math.Clamp(stage, 0, _claims.Length - 1)][(sy * Density.Across) + sx] += people;
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
        _turnedAway = -1;
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
            if (_site.AreaInside(tile) is >= 0 and var area)
            {
                // Security on the gaps of a full area turn people away; inside a kept one nobody packs in past its limit.
                if (!MayPickInside(area, stage, people))
                {
                    _turnedAway = area;
                    continue;
                }

                if (Kept(area))
                {
                    limit = Math.Min(limit, PackInside(area));
                }
            }

            if (Claimed(x, y, people) <= limit)
            {
                return tile;
            }
        }

        return null;
    }

    /// <summary>
    /// Eases a packed crowd: everyone watching from a busy square moves further back. Returns how
    /// many people moved. With a share under 1 only that share of them listen; the rest stay put.
    /// </summary>
    public int Ease(double share = 1)
    {
        var moved = 0;
        for (var i = 0; i < _shows.Length; i++)
        {
            moved += Ease(_shows[i], i, share);
        }

        return moved;
    }

    /// <summary>True for a party that does as it is asked, when only a share of people do. The same people never listen.</summary>
    public static bool Listens(Party party, double share) => share >= 1 || ((uint)party.Id * 2654435761u % 1000) / 1000.0 < share;

    private int Ease(RunningOrder? show, int number, double share)
    {
        if (show?.Drawing(_now) is not { } slot || show.Stage is not { } stage)
        {
            return 0;
        }

        Array.Clear(_claims[number]);
        var moved = 0;
        foreach (var party in _parties)
        {
            if (party.State != PartyState.Watching || party.Watch != slot.Index || Density.At(party.X, party.Y) < DensityMap.Busy || !Listens(party, share))
            {
                continue;
            }

            if (PlaceToWatch(stage, party.Random, slot.Act.Draw, party.Members.Count, standBack: true) is { } spot)
            {
                var watching = party.Watch;
                Send(party, spot, PartyState.Strolling);
                party.Watch = party.State == PartyState.Strolling ? watching : -1;
                Claim(spot, party.Members.Count, number);
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
        LeaveQueue(party);
        LeaveStall(party);
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
            if (!WalkableFor(party, FieldMap.TileAt(party.X, party.Y)))
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
        LeaveQueue(party);
        LeaveStall(party);
        if (!WalkableFor(party, FieldMap.TileAt(party.X, party.Y)))
        {
            (party.X, party.Y) = FieldMap.CentreOf(NearestWalkable(FieldMap.TileAt(party.X, party.Y)));
        }

        Send(party, Home(party), PartyState.Leaving);
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
        GiveUpSeat(party);
        var from = FieldMap.TileAt(party.X, party.Y);
        party.Target = target ?? from;
        if (state == PartyState.Leaving && target is not null && target != party.Pitch && WayOut(party, from) is { } gate
            && Route(party, from, gate) is { } toGate && Route(party, gate, target.Value) is { } onward)
        {
            // Going home, people use the whole width of every gate instead of all cutting the same corner.
            toGate.AddRange(onward);
            party.Path = toGate;
        }
        else
        {
            party.Path = target is null ? null : Route(party, from, target.Value);
        }

        party.PathIndex = 0;
        party.State = party.Path is null ? PartyState.Standing : state;
    }

    /// <summary>
    /// Someone has stopped for an entertainer: they walk over to a spot in front of them, then stand
    /// and listen until the time given. False if there is no way to the spot.
    /// </summary>
    internal bool StopForBusker(Party party, int busker, double bx, double by, TilePoint spot, DateTime until)
    {
        if (party.State is not (PartyState.Strolling or PartyState.Standing))
        {
            return false;
        }

        var from = FieldMap.TileAt(party.X, party.Y);
        var path = Route(party, from, spot);
        if (path is null)
        {
            return false;
        }

        GiveUpSeat(party);
        party.Target = spot;
        party.Path = path;
        party.PathIndex = 0;
        party.State = PartyState.Strolling;
        party.Busker = busker;
        party.BuskUntil = until;
        (party.BuskX, party.BuskY) = (bx, by);
        return true;
    }

    /// <summary>Everyone listening to an entertainer, or on their way over, moves on: the entertainer has gone.</summary>
    internal void BuskerGone(int busker, DateTime now)
    {
        foreach (var party in _parties)
        {
            if (party.Busker != busker)
            {
                continue;
            }

            party.Busker = -1;
            if (party.State == PartyState.Standing)
            {
                party.RetryAt = now;
                party.WaitUntil = now;
            }
        }
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
            p.Gate, p.Lane, p.Gate >= 0 && p.Gate < _queues.Count ? _queues[p.Gate][p.Lane].Parties.IndexOf(p) : 0, p.ServiceLeft, p.Ticketed, p.Watch, p.Decided)
            {
                Hunger = p.Hunger, Thirst = p.Thirst, Toilet = p.Toilet, Mood = p.Mood, Stall = p.Stall,
                StallPlace = p.Stall >= 0 && p.Stall < _stallQueues.Count ? _stallQueues[p.Stall]?.Parties.IndexOf(p) ?? 0 : 0,
                Server = p.Server, InLine = p.InLine, LinePos = p.LinePos, Checked = p.Checked,
                Cubicles = p.Cubicles?.ToList(), WaitRow = p.WaitRow,
                Visits = p.Cubicles is null || p.Stall < 0 || p.Stall >= _cubicles.Count ? null : _cubicles[p.Stall]
                    .Where(c => ReferenceEquals(c?.Party, p))
                    .Select(c => new CubicleSave(c!.Member, (int)c.Phase, c.Left, c.X, c.Y, c.Heading))
                    .ToList(),
                QueuedSince = p.QueuedSince, NeedRetryAt = p.NeedRetryAt, GaveUp = p.GaveUp, Spent = p.Spent, LeftUnhappy = p.LeftUnhappy, Leaves = p.Leaves, LitterAt = p.LitterAt, DecidedSecond = p.DecidedSecond, DecidedThird = p.DecidedThird, DecidedFourth = p.DecidedFourth, StayDecided = p.StayDecided,
                Holding = p.Members.Any(m => m.Holding != Carried.None) ? p.Members.Select(m => (int)m.Holding).ToList() : null,
                Bought = p.Members.Any(m => m.Bought != 0) ? p.Members.Select(m => m.Bought).ToList() : null,
                HoldUntil = p.HoldUntil, FancyAt = p.FancyAt, GrumbleAt = p.GrumbleAt, BoughtHats = p.BoughtHats, BoughtMerch = p.BoughtMerch, FairGoes = p.FairGoes == 0 ? null : p.FairGoes, Fancying = p.Fancying,
                Moving = p.Moving && p.State == PartyState.AtStall && p.Server >= 0, Drink = p.Drink <= 0 ? null : p.Drink, SickUntil = p.SickUntil == default ? null : p.SickUntil, Opponent = p.Fighting ? p.Opponent : null,
                Charged = p.Charged, OnGround = p.State == PartyState.Sitting && p.OnGround, SitUntil = p.State == PartyState.Sitting ? p.SitUntil : default, RestAfter = p.RestAfter,
                Seat = p.State == PartyState.Sitting && p.Seat is { } sat ? new TilePoint(sat.X, sat.Y) : null,
                Seats = p.State == PartyState.Sitting && p.Seat is not null ? p.Seats?.ToList() : null,
            })
        .ToList();

    /// <summary>Puts back parties from a save. Anyone not in the save has not arrived yet.</summary>
    public void Restore(IEnumerable<PartySave> saved)
    {
        _seats.Clear();
        foreach (var queue in _queues.SelectMany(q => q))
        {
            queue.Parties.Clear();
        }

        foreach (var queue in _stallQueues)
        {
            queue?.Parties.Clear();
        }

        foreach (var serving in _serving)
        {
            Array.Clear(serving);
        }

        foreach (var cubicles in _cubicles)
        {
            Array.Clear(cubicles);
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
            party.DecidedThird = s.DecidedThird;
            party.DecidedFourth = s.DecidedFourth;
            party.Gate = -1;
            party.ServiceLeft = s.ServiceLeft;
            party.Stall = -1;
            party.Server = -1;
            party.InLine = false;
            party.Checked = 0;
            party.Cubicles = null;
            party.Rejoins = 0;
            party.GaveUp = s.GaveUp;
            party.Spent = s.Spent;
            party.LeftUnhappy = s.LeftUnhappy;
            party.LitterAt = s.LitterAt;
            party.HoldUntil = s.HoldUntil;
            party.FancyAt = s.FancyAt;
            party.GrumbleAt = s.GrumbleAt;
            party.BoughtHats = s.BoughtHats;
            party.BoughtMerch = s.BoughtMerch;
            party.FairGoes = s.FairGoes ?? 0;
            party.Charged = s.Charged;
            party.Drink = s.Drink ?? 0;
            party.SickUntil = s.SickUntil ?? default;
            if (s.SickUntil is { } sick && party.State == PartyState.Standing)
            {
                party.RetryAt = sick;
            }

            party.Opponent = s.Opponent ?? -1;
            party.Fighting = s.Opponent is not null;
            if (party.Fighting)
            {
                party.RetryAt = s.WaitUntil;
            }

            party.Fancying = false;
            RestoreSeat(party, s);
            for (var m = 0; m < party.Members.Count; m++)
            {
                party.Members[m].Holding = s.Holding is { } held && m < held.Count ? (Carried)held[m] : Carried.None;
                if (s.Bought is { } bought && m < bought.Count)
                {
                    PutBackOn(party.Members[m], bought[m]);
                }
            }

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
                if (s.Stall >= 0 && s.Stall < _stallQueues.Count && _stallQueues[s.Stall] is { } line)
                {
                    party.Stall = s.Stall;
                    party.Fancying = s.Fancying;
                    party.QueuedSince = s.QueuedSince;
                    if (_cubicles[s.Stall].Length > 0)
                    {
                        // At the toilets: some of the party may be in cubicles, or have been already.
                        if (s.Cubicles is { } been && been.Count == party.Members.Count)
                        {
                            party.Cubicles = been.ToArray();
                            party.WaitRow = s.WaitRow;
                            party.Checked = Math.Clamp(s.Checked, 0, party.Members.Count);
                            foreach (var visit in s.Visits ?? new())
                            {
                                var k = visit.Member >= 0 && visit.Member < been.Count ? been[visit.Member] : -1;
                                if (k >= 0 && k < _cubicles[s.Stall].Length && _cubicles[s.Stall][k] is null)
                                {
                                    _cubicles[s.Stall][k] = new CubicleUse(party, visit.Member)
                                    {
                                        Phase = (CubiclePhase)visit.Phase, Left = visit.Left, X = visit.X, Y = visit.Y, Heading = visit.Heading,
                                    };
                                }
                            }
                        }

                        if (party.Checked == party.Members.Count)
                        {
                            party.Server = 0;
                        }
                        else
                        {
                            line.Parties.Add(party);
                            party.InLine = s.InLine;
                            party.LinePos = s.LinePos;
                        }
                    }
                    else if (s.Server >= 0 && s.Server < _serving[s.Stall].Length && _serving[s.Stall][s.Server] is null)
                    {
                        party.Server = s.Server;
                        party.Moving = s.Moving;
                        _serving[s.Stall][s.Server] = party;
                    }
                    else
                    {
                        line.Parties.Add(party);
                        party.InLine = s.InLine;
                        party.LinePos = s.LinePos;
                        party.ServiceLeft = -1;
                    }

                    party.Path = s.Path is { Count: > 0 } && s.Path.All(t => WalkableFor(party, t)) ? s.Path.ToList() : null;
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
                    party.InLine = s.InLine;
                    party.LinePos = s.LinePos;
                    party.Checked = Math.Clamp(s.Checked, 0, party.Members.Count - 1);
                    _queues[s.Gate][s.Lane].Parties.Add(party);
                }
                else if (s.Gate >= 0)
                {
                    Arrive(party);
                    continue;
                }

                party.Path = s.Path is { Count: > 0 } && s.Path.All(t => WalkableFor(party, t)) ? s.Path.ToList() : null;
                party.PathIndex = 0;
                continue;
            }

            if (party.State is not (PartyState.WalkingIn or PartyState.Strolling or PartyState.Leaving))
            {
                continue;
            }

            if (s.Path is { Count: > 0 } && s.Path.All(t => WalkableFor(party, t)) && WalkableFor(party, FieldMap.TileAt(s.X, s.Y)))
            {
                // Carry on along the very route they were on.
                party.Path = s.Path.ToList();
                party.PathIndex = 0;
            }
            else
            {
                if (!WalkableFor(party, FieldMap.TileAt(s.X, s.Y)))
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

    public bool Charged { get; init; }

    /// <summary>The corner tile of the seat a sitting party is on, and which of its seats each of them has.</summary>
    public TilePoint? Seat { get; init; }

    public List<int>? Seats { get; init; }

    public bool OnGround { get; init; }

    public DateTime SitUntil { get; init; }

    /// <summary>No sitting down just for a rest before this.</summary>
    public DateTime RestAfter { get; init; }

    public double Thirst { get; init; }

    public double Toilet { get; init; }

    public double? Mood { get; init; }

    public int Stall { get; init; } = -1;

    /// <summary>Only in saves from before queues were single file.</summary>
    public int StallLane { get; init; }

    /// <summary>Which server is serving them, or -1.</summary>
    public int Server { get; init; } = -1;

    /// <summary>True if they are standing in the queue, and how many places from the front.</summary>
    public bool InLine { get; init; }

    public double LinePos { get; init; }

    /// <summary>How many of the party have had their tickets checked, or have left the queue for a toilet cubicle.</summary>
    public int Checked { get; init; }

    /// <summary>At the toilets, where each member has got to: -1 waiting, a cubicle's number, or -2 once they have been.</summary>
    public List<int>? Cubicles { get; init; }

    /// <summary>Which row beside the toilets the party gathers in.</summary>
    public int WaitRow { get; init; }

    /// <summary>The members who are at or in a cubicle just now.</summary>
    public List<CubicleSave>? Visits { get; init; }

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

    /// <summary>The last set on the third and fourth stages the party made its mind up about, or -1.</summary>
    public int DecidedThird { get; init; } = -1;

    public int DecidedFourth { get; init; } = -1;

    public DateTime? Leaves { get; init; }

    /// <summary>What each member has in their hand, and what each has bought and put on (1 poncho, 2 hat).</summary>
    public List<int>? Holding { get; init; }

    public List<int>? Bought { get; init; }

    public DateTime HoldUntil { get; init; }

    public DateTime FancyAt { get; init; }

    public DateTime GrumbleAt { get; init; }

    public bool BoughtHats { get; init; }

    public bool BoughtMerch { get; init; }

    public int? FairGoes { get; init; }

    public bool Fancying { get; init; }

    /// <summary>True for a party called forward at a stall or a ride who are still walking up to it.</summary>
    public bool Moving { get; init; }

    /// <summary>How much they have had to drink.</summary>
    public double? Drink { get; init; }

    public DateTime? SickUntil { get; init; }

    /// <summary>The party they are in a fight with, if they are in one.</summary>
    public int? Opponent { get; init; }
}

/// <summary>
/// How many people have picked each square of the crowd map to watch the act now on from, for
/// the main stage and the second stage, and which sets those are.
/// </summary>
public sealed record WatchingPlaces(int Slot, Dictionary<int, int>? Claims, int SlotSecond, Dictionary<int, int>? ClaimsSecond)
{
    /// <summary>How many people were in each square when the crowd was last counted.</summary>
    public Dictionary<int, int>? Density { get; init; }

    /// <summary>The minute of the hour the crowd was last counted in.</summary>
    public int MeasuredMinute { get; init; } = -1;

    /// <summary>The same for the third and fourth stages, or null in an older save.</summary>
    public List<int>? MoreSlots { get; init; }

    public List<Dictionary<int, int>?>? MoreClaims { get; init; }
}

/// <summary>One person's visit to a toilet cubicle as written to a save file.</summary>
public sealed record CubicleSave(int Member, int Phase, int Left, double X, double Y, double Heading);
