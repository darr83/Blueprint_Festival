using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.People;

/// <summary>What a party of festival-goers is doing.</summary>
public enum PartyState
{
    /// <summary>Not here yet.</summary>
    Expected,

    /// <summary>Walking in along the lane and through the gap.</summary>
    WalkingIn,

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

    public bool IsWalking => State is PartyState.WalkingIn or PartyState.Strolling or PartyState.Leaving;

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

    private readonly FieldMap _map;
    private readonly PathFinder _paths;
    private readonly List<Party> _parties = new();

    public Crowd(FieldMap map, int seed, DateTime festivalDay)
    {
        _map = map;
        _paths = new PathFinder(map);
        Seed = seed;
        Plan(festivalDay.Date);
    }

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

    /// <summary>A place to stand in the field, well clear of the hedge.</summary>
    private TilePoint SomewhereInField(GameRandom random)
    {
        const int inset = 4;
        var f = _map.Field;
        return new TilePoint(f.X + inset + random.Next(f.Width - (2 * inset)), f.Y + inset + random.Next(f.Height - (2 * inset)));
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
                        Send(party, SomewhereInField(party.Random), PartyState.WalkingIn);
                    }

                    break;

                case PartyState.Standing:
                    if (now >= party.Leaves || closing)
                    {
                        Send(party, LaneEnd(party), PartyState.Leaving);
                    }
                    else if (now >= party.WaitUntil)
                    {
                        Send(party, SomewhereInField(party.Random), PartyState.Strolling);
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
                            party.WaitUntil = now + TimeSpan.FromMinutes(party.Random.Between(2, 12));
                            party.Heading += party.Random.Between(-1.2, 1.2);
                        }
                    }

                    break;
            }
        }
    }

    private void Send(Party party, TilePoint target, PartyState state)
    {
        var from = FieldMap.TileAt(party.X, party.Y);
        party.Target = target;
        party.Path = _paths.FindPath(from, target);
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
        .Select(p => new PartySave(p.Id, p.State, p.X, p.Y, p.Heading, p.Target.X, p.Target.Y, p.WaitUntil, p.Random.State, p.Path?.Skip(p.PathIndex).ToList()))
        .ToList();

    /// <summary>Puts back parties from a save. Anyone not in the save has not arrived yet.</summary>
    public void Restore(IEnumerable<PartySave> saved)
    {
        foreach (var s in saved)
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
            if (!party.IsWalking)
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
public sealed record PartySave(int Id, PartyState State, double X, double Y, double Heading, int TargetX, int TargetY, DateTime WaitUntil, ulong Random, List<TilePoint>? Path = null);
