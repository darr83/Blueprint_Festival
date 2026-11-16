using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation;

/// <summary>Where someone has been sick on the ground. It stays until a cleaner mops it up.</summary>
public sealed record SickPatch(int Id, double X, double Y, DateTime At)
{
    /// <summary>The line in the day's log about it, or -1.</summary>
    public int Note { get; init; } = -1;
}

/// <summary>
/// Drink and what comes of it: fights, people being sick, and people passing out. Fights are for
/// security to break up, and both of those fighting are thrown out; left alone, someone gets hurt.
/// Sick stays on the ground, spoiling the day of everyone who walks past, until a cleaner mops it up.
/// </summary>
public sealed partial class FestivalGame
{
    /// <summary>How long a fight goes on before there is any chance of someone getting hurt, in minutes.</summary>
    public const double FightGraceMinutes = 3;

    /// <summary>The chance each minute after that, with nobody there to stop it, that someone is hurt.</summary>
    public const double FightHurtChance = 0.2;

    /// <summary>The chance each minute that someone drunk is sick, starts a fight, or (very drunk) passes out.</summary>
    public const double SickChance = 0.0006;
    public const double FightChance = 0.0006;
    public const double PassOutChance = 0.0008;

    /// <summary>How much of the day's ordinary trouble turns into a fight: one time in ten.</summary>
    public const double TroubleIsAFight = 0.1;

    /// <summary>How long security take to pull two people apart, in ticks.</summary>
    public const int BreakUpTicks = 45;

    /// <summary>How long a cleaner takes to mop up, in ticks.</summary>
    public const int MopTicks = 75;

    /// <summary>How near a puddle of sick spoils things, in metres, and how much mood it costs each minute.</summary>
    public const double SickReach = 3;
    public const double SickMoodPerMinute = 0.0015;

    /// <summary>How long sick lies there if nobody mops it up, before it is trodden into the grass, in minutes.</summary>
    public const double SickLastsMinutes = 120;

    private readonly List<SickPatch> _sick = new();
    private GameRandom _rowdy = null!;
    private int _nextSick;

    /// <summary>Where people have been sick and nobody has mopped up yet.</summary>
    public IReadOnlyList<SickPatch> Sick => _sick;

    /// <summary>Fights so far today.</summary>
    public int Fights { get; private set; }

    /// <summary>People thrown out by security for fighting.</summary>
    public int ThrownOut { get; private set; }

    /// <summary>People hurt in a fight nobody stopped.</summary>
    public int FightInjuries { get; private set; }

    /// <summary>How many times someone has been sick on the ground today.</summary>
    public int TimesSick { get; private set; }

    /// <summary>Puddles of sick the cleaners have mopped up.</summary>
    public int SickMopped { get; private set; }

    /// <summary>Puddles of sick nobody mopped up, left until they were trodden in.</summary>
    public int SickLeft { get; private set; }

    /// <summary>People who passed out from drink.</summary>
    public int PassedOut { get; private set; }

    /// <summary>Drinks the bars refused to sell today to people who had had enough.</summary>
    public int DrinksRefused => Crowd.Refused;

    /// <summary>How many grown-ups on site are at each stage of drink just now.</summary>
    public (int Merry, int Tipsy, int Drunk) Drinkers
    {
        get
        {
            var (merry, tipsy, drunk) = (0, 0, 0);
            foreach (var party in Crowd.Parties)
            {
                if (!party.IsOnSite || party.Drink < People.Crowd.MerryAt)
                {
                    continue;
                }

                var grownUps = party.Members.Count(m => m.Look.Age is AgeGroup.Adult or AgeGroup.Older);
                switch (People.Crowd.LevelOf(party.Drink))
                {
                    case Tipple.Merry:
                        merry += grownUps;
                        break;
                    case Tipple.Tipsy:
                        tipsy += grownUps;
                        break;
                    default:
                        drunk += grownUps;
                        break;
                }
            }

            return (merry, tipsy, drunk);
        }
    }

    private void StartTrouble()
    {
        _rowdy = GameRandom.FromSeed(Seed, 73);
    }

    /// <summary>Once a minute of the day: drink wears off, the drunk get into bother, and sick on the ground puts people off.</summary>
    private void TickTrouble(DateTime now)
    {
        if (Phase != Phase.Day || Clock.TickCount % 60 != 30)
        {
            return;
        }

        Crowd.SoberUp();
        for (var i = _sick.Count - 1; i >= 0; i--)
        {
            var patch = _sick[i];
            Crowd.Upset(patch.X, patch.Y, SickReach, SickMoodPerMinute);
            if (now - patch.At >= TimeSpan.FromMinutes(SickLastsMinutes))
            {
                // Nobody came. By now it has been walked through and trodden in.
                _sick.RemoveAt(i);
                SickLeft++;
                if (_notes.FirstOrDefault(n => n.Id == patch.Note) is { } note)
                {
                    (note.Outcome, note.Tone) = ("Nobody mopped it up: trodden into the grass", DayTone.Bad);
                }
            }
        }

        CarryOnFighting(now);
        if (!NeedsStaff)
        {
            return;
        }

        var evening = now.TimeOfDay >= TimeSpan.FromHours(17);
        var parties = Crowd.Parties;
        for (var i = 0; i < parties.Count; i++)
        {
            var party = parties[i];
            if (party.Drink < People.Crowd.TipsyAt || !Crowd.OutAndAbout(party))
            {
                continue;
            }

            var drunk = party.Drink >= People.Crowd.DrunkAt;
            var roll = _rowdy.NextDouble();
            var sickChance = drunk ? SickChance * party.Drink / People.Crowd.DrunkAt : SickChance / 8;
            if (roll < sickChance)
            {
                BeSick(party, "Someone was sick");
                continue;
            }

            roll -= sickChance;
            var outChance = party.Drink >= People.Crowd.LegLessAt ? PassOutChance : 0;
            if (roll < outChance)
            {
                PassedOut++;
                Crowd.TakeIll(party);
                Raise(new Incident(_nextIncident++, IncidentKind.Ill, party.X, party.Y, party.Id, now), DayNoteKind.Ill, "Passed out from drink");
                continue;
            }

            roll -= outChance;
            // Security standing near keep a lid on it; a packed crowd does the opposite.
            var fightChance = (drunk ? FightChance : FightChance / 10) * (evening ? 1 : 0.6)
                * (Stewarded(party.X, party.Y) ? 0.4 : 1)
                * (Crowd.Density.At(party.X, party.Y) >= DensityMap.Busy ? 1.5 : 1);
            if (roll < fightChance)
            {
                StartFight(party, now);
            }
        }
    }

    /// <summary>
    /// A party picks a fight with whoever is nearest. False if they are in no state to, or there
    /// is nobody near enough to fight.
    /// </summary>
    private bool StartFight(Party party, DateTime now, bool sober = false)
    {
        if (sober && _rowdy.NextDouble() >= TroubleIsAFight)
        {
            return false;
        }

        if (!Crowd.CouldFight(party) || Crowd.SomeoneToFight(party) is not { } other)
        {
            return false;
        }

        Crowd.SquareUp(party, other);
        var (x, y) = ((party.X + other.X) / 2, (party.Y + other.Y) / 2);
        Crowd.MakeRoom(x, y, party, other);
        Fights++;
        Raise(new Incident(_nextIncident++, IncidentKind.Fight, x, y, party.Id, now) { Other = other.Id }, DayNoteKind.Trouble, "A fight");
        return true;
    }

    /// <summary>Starts a fight between the two parties nearest a spot, for trying things out. False if there are not two who could.</summary>
    public bool StartFightAt(double x, double y)
    {
        var nearest = Crowd.Parties.Where(Crowd.CouldFight).OrderBy(p => Distance(p.X, p.Y, x, y)).FirstOrDefault();
        return Phase == Phase.Day && nearest is not null && StartFight(nearest, Now);
    }

    /// <summary>Makes someone in the party nearest a spot sick there, for trying things out.</summary>
    public bool BeSickAt(double x, double y)
    {
        var nearest = Crowd.Parties.Where(Crowd.OutAndAbout).OrderBy(p => Distance(p.X, p.Y, x, y)).FirstOrDefault();
        if (Phase != Phase.Day || nearest is null)
        {
            return false;
        }

        BeSick(nearest, "Someone was sick");
        return true;
    }

    private void BeSick(Party party, string what)
    {
        var (x, y) = Crowd.BeSick(party);
        TimesSick++;
        var note = Note(DayNoteKind.Sick, $"{what} {Whereabouts(x, y)}".TrimEnd(), "Waiting for a cleaner", DayTone.Open);
        _sick.Add(new SickPatch(_nextSick++, Math.Round(x, 2), Math.Round(y, 2), Now) { Note = note.Id });
    }

    /// <summary>One in ten people taken ill are sick where they stand first.</summary>
    private void SickWhereTheyFell(Party party)
    {
        if (_rowdy.NextDouble() < 0.1)
        {
            TimesSick++;
            var note = Note(DayNoteKind.Sick, $"Someone was sick {Whereabouts(party.X, party.Y)}".TrimEnd(), "Waiting for a cleaner", DayTone.Open);
            _sick.Add(new SickPatch(_nextSick++, Math.Round(party.X + 0.6, 2), Math.Round(party.Y + 0.3, 2), Now) { Note = note.Id });
        }
    }

    /// <summary>Every minute a fight goes on, people keep back from it, and after a while someone gets hurt.</summary>
    private void CarryOnFighting(DateTime now)
    {
        for (var i = _incidents.Count - 1; i >= 0; i--)
        {
            var fight = _incidents[i];
            if (fight.Kind != IncidentKind.Fight)
            {
                continue;
            }

            var one = Crowd.PartyById(fight.Party);
            var other = Crowd.PartyById(fight.Other);
            if (one is not { Fighting: true } || other is not { Fighting: true })
            {
                // One of them has gone: it is over.
                EndFight(fight, one, other);
                Settle(fight, "It fizzled out", DayTone.Plain);
                Close(fight);
                continue;
            }

            if (_staff.Any(s => s.Id == fight.Staff && s.State == StaffState.Busy))
            {
                // Security have hold of them.
                continue;
            }

            Crowd.MakeRoom(fight.X, fight.Y, one, other);
            Crowd.Upset(fight.X, fight.Y, 7, 0.008);
            if (now - fight.Started >= TimeSpan.FromMinutes(FightGraceMinutes) && _rowdy.NextDouble() < FightHurtChance)
            {
                SomeoneHurt(fight, one, other, now);
            }
        }
    }

    private void EndFight(Incident fight, Party? one, Party? other)
    {
        if (one is not null)
        {
            Crowd.Unhand(one);
        }

        if (other is not null)
        {
            Crowd.Unhand(other);
        }
    }

    /// <summary>Nobody stopped it: one of them goes down hurt and the other makes off.</summary>
    private void SomeoneHurt(Incident fight, Party one, Party other, DateTime now)
    {
        var (loser, winner) = _rowdy.NextDouble() < 0.5 ? (one, other) : (other, one);
        EndFight(fight, one, other);
        FightInjuries++;
        TroubleIgnored++;
        Settle(fight, "Nobody stopped it: someone was hurt", DayTone.Bad);
        Crowd.Upset(fight.X, fight.Y, 10, 0.06);
        Close(fight);
        Crowd.GoHome(winner, unhappy: false);
        Crowd.TakeIll(loser);
        Raise(new Incident(_nextIncident++, IncidentKind.Ill, loser.X, loser.Y, loser.Id, now), DayNoteKind.Hurt, "Hurt in a fight");
    }

    /// <summary>Security have pulled them apart. Both are thrown out, and the member of staff walks one of them to the gate.</summary>
    private void BreakUp(StaffMember member, Incident fight)
    {
        var one = Crowd.PartyById(fight.Party);
        var other = Crowd.PartyById(fight.Other);
        EndFight(fight, one, other);
        TroubleSorted++;
        Settle(fight, "Broken up: both thrown out", DayTone.Good);
        _incidents.Remove(fight);
        member.Incident = -1;
        foreach (var party in new[] { one, other })
        {
            if (party is not null)
            {
                ThrownOut++;
                Crowd.GoHome(party, unhappy: false);
            }
        }

        if (one is { State: PartyState.Leaving })
        {
            member.State = StaffState.SeeingOut;
            member.Seeing = one.Id;
            member.Path = null;
        }
        else
        {
            Walk(member, member.PostX, member.PostY, StaffState.Returning);
        }
    }

    /// <summary>Security walk beside someone they have thrown out until they are off the festival ground.</summary>
    private void SeeOut(StaffMember member)
    {
        var party = Crowd.PartyById(member.Seeing);
        if (Phase != Phase.Day || party is not { State: PartyState.Leaving } || !Field.InFestival(FieldMap.TileAt(party.X, party.Y)))
        {
            member.Seeing = -1;
            Walk(member, member.PostX, member.PostY, StaffState.Returning);
            return;
        }

        // At their shoulder, half a pace behind.
        var (tx, ty) = (party.X + (Math.Cos(party.Heading) * 0.55) - (Math.Sin(party.Heading) * 0.3), party.Y - (Math.Sin(party.Heading) * 0.55) - (Math.Cos(party.Heading) * 0.3));
        var (dx, dy) = (tx - member.X, ty - member.Y);
        var away = Math.Sqrt((dx * dx) + (dy * dy));
        if (away > 0.01)
        {
            var step = Math.Min(away, StaffSpeed * 1.3);
            member.X += dx / away * step;
            member.Y += dy / away * step;
        }

        member.Heading = party.Heading;
    }

    /// <summary>The nearest puddle of sick a cleaner could go to that no other cleaner is already seeing to.</summary>
    private SickPatch? SickToMop(StaffMember member, double within)
    {
        SickPatch? best = null;
        foreach (var patch in _sick)
        {
            var away = Distance(patch.X, patch.Y, member.X, member.Y);
            if (away < within && Field.IsWalkableForStaff(FieldMap.TileAt(patch.X, patch.Y))
                && !_staff.Any(s => s != member && s.State is StaffState.Cleaning or StaffState.Mopping && Distance(s.HoldX, s.HoldY, patch.X, patch.Y) <= 1.6))
            {
                (best, within) = (patch, away);
            }
        }

        return best;
    }

    /// <summary>The cleaner has finished mopping: whatever sick was within reach is gone.</summary>
    private void Mopped(StaffMember member)
    {
        foreach (var patch in _sick.Where(p => Distance(p.X, p.Y, member.HoldX, member.HoldY) <= 1.6).ToList())
        {
            _sick.Remove(patch);
            SickMopped++;
            if (_notes.FirstOrDefault(n => n.Id == patch.Note) is { } note)
            {
                (note.Outcome, note.Tone) = ("Mopped up", DayTone.Good);
            }
        }
    }

    /// <summary>The day is over: nobody is left fighting.</summary>
    private void EndTrouble()
    {
        foreach (var party in Crowd.Parties)
        {
            Crowd.Unhand(party);
        }

        foreach (var note in _notes.Where(n => n.Kind == DayNoteKind.Sick && n.Tone == DayTone.Open))
        {
            (note.Outcome, note.Tone) = ("Still there in the morning", DayTone.Bad);
        }
    }

    private TroubleSave SaveTrouble() => new(_rowdy.State, _sick.ToList(), _nextSick, Fights, ThrownOut, FightInjuries, TimesSick, SickMopped, PassedOut, Crowd.Refused) { SickLeft = SickLeft };

    private void RestoreTrouble(TroubleSave? save)
    {
        if (save is null)
        {
            return;
        }

        _rowdy = new GameRandom(save.Rowdy);
        _sick.Clear();
        _sick.AddRange(save.Sick ?? new());
        _nextSick = Math.Max(save.NextSick, _sick.Count == 0 ? 0 : _sick.Max(s => s.Id) + 1);
        (Fights, ThrownOut, FightInjuries, TimesSick, SickMopped, PassedOut) = (save.Fights, save.ThrownOut, save.FightInjuries, save.TimesSick, save.SickMopped, save.PassedOut);
        Crowd.Refused = save.Refused;
        SickLeft = save.SickLeft;
    }
}

/// <summary>Fights, sick and drink, as written to a save file.</summary>
public sealed record TroubleSave(ulong Rowdy, List<SickPatch>? Sick, int NextSick, int Fights, int ThrownOut, int FightInjuries, int TimesSick, int SickMopped, int PassedOut, int Refused)
{
    public int SickLeft { get; init; }
}
