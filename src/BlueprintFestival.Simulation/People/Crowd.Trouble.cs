using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.People;

/// <summary>How much someone has had to drink.</summary>
public enum Tipple
{
    Sober,

    /// <summary>A drink or two: in a better mood for it.</summary>
    Merry,

    /// <summary>Swaying a little, and keen on another.</summary>
    Tipsy,

    /// <summary>Staggering. This is where the trouble comes from.</summary>
    Drunk,
}

/// <summary>
/// Drink. What the grown-ups in a party buy at the bar adds up through the day and wears off with
/// time, a meal and water. A little makes for a better day; a lot makes people unsteady, and it
/// is the drunk who start fights, are sick and pass out.
/// </summary>
public sealed partial class Crowd
{
    public const double MerryAt = 1;
    public const double TipsyAt = 2.5;
    public const double DrunkAt = 4;

    /// <summary>
    /// How much someone has had when a bar that does not serve drunks turns them away: a little
    /// short of drunk, because the next round is the one that would do it.
    /// </summary>
    public const double HadEnoughAt = 3;

    /// <summary>How much drink puts someone at risk of passing out.</summary>
    public const double LegLessAt = 6;

    /// <summary>How much wears off in a minute: one drink an hour.</summary>
    public const double SoberPerMinute = 1 / 60.0;

    /// <summary>How long someone stands bent double being sick, in minutes.</summary>
    public const double SickForMinutes = 1.5;

    /// <summary>Drinks the bars would not sell today because the customer had had enough.</summary>
    public int Refused { get; internal set; }

    /// <summary>How far gone a party is for the drink they have had.</summary>
    public static Tipple LevelOf(double drink) => drink >= DrunkAt ? Tipple.Drunk : drink >= TipsyAt ? Tipple.Tipsy : drink >= MerryAt ? Tipple.Merry : Tipple.Sober;

    /// <summary>
    /// What sort of drinkers a party are, the same all day: 0 for the many who have one or two,
    /// 1 for those who like a drink, 2 for the few who are here for a session. Families with
    /// children with them take it easy.
    /// </summary>
    public static int Thirstiness(Party party)
    {
        ArgumentNullException.ThrowIfNull(party);
        if (party.HasChild || !party.Members.Any(GrownUp))
        {
            return 0;
        }

        var roll = (uint)(party.Id * 2246822519u) % 100;
        return roll < 12 ? 2 : roll < 45 ? 1 : 0;
    }

    /// <summary>How unsteady a party is on its feet, from 0 to 1.</summary>
    public static double WobbleOf(double drink) => Math.Clamp((drink - 1.8) / 3.4, 0, 1);

    /// <summary>The grown-ups have had a round: so many alcoholic drinks between them.</summary>
    private void Drank(Party party, int drinks)
    {
        var grownUps = Math.Max(1, party.Members.Count(GrownUp));
        var strength = Thirstiness(party) switch { 2 => 2.2, 1 => 1.4, _ => 0.8 };
        party.Drink += (double)drinks / grownUps * strength;
        party.Mood = Math.Min(1, party.Mood + 0.006);
    }

    /// <summary>Gives a party so many drinks each straight away, for trying things out.</summary>
    public void Pour(Party party, double drinks)
    {
        ArgumentNullException.ThrowIfNull(party);
        party.Drink = Math.Max(0, party.Drink + drinks);
    }

    /// <summary>A meal, or water from the tap, takes the edge off.</summary>
    private static void Lined(Party party, double by) => party.Drink = Math.Max(0, party.Drink - by);

    /// <summary>
    /// The chance that a party who like a drink fancy another round just now. It grows once they
    /// have had a couple and as the day goes on. They go up to a bar that will not serve drunks
    /// just the same, and find out at the counter.
    /// </summary>
    public double AnotherRound(Party party, PlacedStall stall, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(stall);
        var keen = Thirstiness(party);
        if (keen == 0 && party.Drink < MerryAt)
        {
            return 0;
        }

        var started = party.Drink >= MerryAt;
        var pull = keen switch { 2 => started ? 0.2 : 0.06, 1 => started ? 0.08 : 0.025, _ => 0.015 };
        // Hardly anyone is on it before lunch; the evening is when it gets going.
        var hour = now.TimeOfDay.TotalHours;
        var clock = hour < 12 ? 0.3 : hour < 16 ? 0.7 : hour < 19 ? 1.2 : 1.5;
        return pull * clock * (party.Drink >= LegLessAt + 1 ? 0.2 : 1);
    }

    /// <summary>A minute has gone by: everyone who has had a drink sobers up a little.</summary>
    public void SoberUp()
    {
        foreach (var party in _parties)
        {
            if (party.Drink > 0)
            {
                party.Drink = Math.Max(0, party.Drink - SoberPerMinute);
            }
        }
    }

    /// <summary>True for a party out in the festival on their feet, who could be sick or get into a fight where they are.</summary>
    public bool OutAndAbout(Party party)
    {
        ArgumentNullException.ThrowIfNull(party);
        return party.State is PartyState.Standing or PartyState.Strolling or PartyState.Watching
            && !party.Fighting && _now >= party.SickUntil && _map.InFestival(FieldMap.TileAt(party.X, party.Y));
    }

    /// <summary>Stops a party where they are until a time: they do not wander off, queue or watch.</summary>
    public void Halt(Party party, DateTime until)
    {
        ArgumentNullException.ThrowIfNull(party);
        if (!OutAndAbout(party))
        {
            return;
        }

        GiveUpSeat(party);
        party.State = PartyState.Standing;
        party.Watch = -1;
        party.Busker = -1;
        party.Path = null;
        party.Moving = false;
        party.RetryAt = until;
        party.WaitUntil = until;
    }

    /// <summary>Someone in the party is sick on the ground in front of them. Returns where it lands.</summary>
    public (double X, double Y) BeSick(Party party)
    {
        ArgumentNullException.ThrowIfNull(party);
        var until = _now + TimeSpan.FromMinutes(SickForMinutes);
        Halt(party, until);
        party.SickUntil = until;
        party.Mood = Math.Max(0, party.Mood - 0.08);
        Lined(party, 1.2);
        DropDrinks(party);
        var (x, y, heading) = Place(party, 0);
        return (x + (Math.Sin(heading) * 0.75), y + (Math.Cos(heading) * 0.75));
    }

    /// <summary>
    /// Two parties square up: the first of each steps in until they are toe to toe, facing each
    /// other, and neither party goes anywhere until it is over.
    /// </summary>
    public void SquareUp(Party one, Party other)
    {
        ArgumentNullException.ThrowIfNull(one);
        ArgumentNullException.ThrowIfNull(other);
        var until = _now + TimeSpan.FromHours(2);
        Halt(one, until);
        Halt(other, until);
        (one.Fighting, other.Fighting) = (true, true);
        var (dx, dy) = (other.X - one.X, other.Y - one.Y);
        (one.Heading, other.Heading) = (Math.Atan2(dx, dy), Math.Atan2(-dx, -dy));
        (one.Opponent, other.Opponent) = (other.Id, one.Id);
        DropDrinks(one);
        DropDrinks(other);
    }

    /// <summary>The fight is over: the party may move again.</summary>
    public void Unhand(Party party)
    {
        ArgumentNullException.ThrowIfNull(party);
        if (!party.Fighting)
        {
            return;
        }

        party.Fighting = false;
        party.Opponent = -1;
        party.RetryAt = default;
        party.WaitUntil = _now;
    }

    /// <summary>Whoever is in the thick of it drops what they were holding, and it lands on the grass as litter.</summary>
    private void DropDrinks(Party party)
    {
        var member = party.Members[0];
        if (member.Holding != Carried.None && !Keeps(member.Holding))
        {
            member.Holding = Carried.None;
            Dropped?.Invoke(party.X + 0.4, party.Y + 0.3);
        }
    }

    /// <summary>
    /// People standing right by a fight step back out of the way, and everyone near turns to look.
    /// </summary>
    public void MakeRoom(double x, double y, Party one, Party other)
    {
        const double clear = 2.6;
        const double looking = 7;
        foreach (var party in _parties)
        {
            if (ReferenceEquals(party, one) || ReferenceEquals(party, other) || party.State is not (PartyState.Standing or PartyState.Watching) || party.Fighting)
            {
                continue;
            }

            var (dx, dy) = (party.X - x, party.Y - y);
            var away = Math.Sqrt((dx * dx) + (dy * dy));
            if (away > looking)
            {
                continue;
            }

            if (away < clear)
            {
                var (ux, uy) = away < 0.05 ? (1.0, 0.0) : (dx / away, dy / away);
                var (nx, ny) = (x + (ux * clear), y + (uy * clear));
                if (WalkableFor(party, FieldMap.TileAt(nx, ny)))
                {
                    (party.X, party.Y) = (nx, ny);
                }
            }

            if (party.State == PartyState.Standing)
            {
                party.Heading = Math.Atan2(x - party.X, y - party.Y);
            }
        }
    }

    /// <summary>The nearest party to another that could be in a fight with them: grown-ups, no children along, close by.</summary>
    public Party? SomeoneToFight(Party party, double within = 7)
    {
        ArgumentNullException.ThrowIfNull(party);
        Party? best = null;
        var nearest = within;
        foreach (var other in _parties)
        {
            if (ReferenceEquals(other, party) || !CouldFight(other))
            {
                continue;
            }

            var away = Math.Sqrt(((other.X - party.X) * (other.X - party.X)) + ((other.Y - party.Y) * (other.Y - party.Y)));
            if (away < nearest && away > 0.6)
            {
                (best, nearest) = (other, away);
            }
        }

        return best;
    }

    /// <summary>True for a party that could get into a fight: out and about, grown up, with no children along.</summary>
    public bool CouldFight(Party party)
    {
        ArgumentNullException.ThrowIfNull(party);
        return OutAndAbout(party) && !party.HasChild && GrownUp(party.Members[0]);
    }

    /// <summary>The party with this number, if there is one.</summary>
    public Party? PartyById(int id) => id >= 0 && id < _parties.Count && _parties[id].Id == id ? _parties[id] : _parties.FirstOrDefault(p => p.Id == id);
}
