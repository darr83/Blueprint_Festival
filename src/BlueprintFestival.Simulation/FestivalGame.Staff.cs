using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation;

/// <summary>What a member of staff is doing.</summary>
public enum StaffState
{
    /// <summary>Standing where they were posted.</summary>
    AtPost,

    /// <summary>On the way to someone who needs them.</summary>
    Going,

    /// <summary>Treating someone, or sorting out trouble.</summary>
    Busy,

    /// <summary>Walking someone to the first aid tent.</summary>
    Escorting,

    /// <summary>Walking back to their post.</summary>
    Returning,
}

/// <summary>Something that needs a member of staff.</summary>
public enum IncidentKind
{
    /// <summary>Someone taken ill or hurt. A first-aider's job.</summary>
    Ill,

    /// <summary>A scuffle. Security's job.</summary>
    Scuffle,

    /// <summary>A lost child. Security's job.</summary>
    LostChild,
}

/// <summary>One person hired for the day.</summary>
public sealed class StaffMember
{
    internal StaffMember(int id, StaffKind kind, TilePoint tile, double postX, double postY, double heading)
    {
        Id = id;
        Tile = tile;
        Kind = kind;
        (PostX, PostY) = (postX, postY);
        (X, Y) = (postX, postY);
        Heading = PostHeading = heading;
    }

    public int Id { get; }

    public StaffKind Kind { get; }

    /// <summary>The tile the player posted them on. For ticket staff it is a tile of their gate.</summary>
    public TilePoint Tile { get; }

    /// <summary>Where they stand at their post, in metres.</summary>
    public double PostX { get; }

    public double PostY { get; }

    public double PostHeading { get; }

    public double X { get; internal set; }

    public double Y { get; internal set; }

    public double Heading { get; internal set; }

    public StaffState State { get; internal set; }

    /// <summary>The number of the incident they are dealing with, or -1.</summary>
    public int Incident { get; internal set; } = -1;

    public bool IsWalking => State is StaffState.Going or StaffState.Escorting or StaffState.Returning;

    public bool IsFree => State is StaffState.AtPost or StaffState.Returning;

    internal List<TilePoint>? Path { get; set; }

    internal int PathIndex { get; set; }
}

/// <summary>Something that has happened and needs staff: where, when, and who is dealing with it.</summary>
public sealed class Incident
{
    internal Incident(int id, IncidentKind kind, double x, double y, int party, DateTime started)
    {
        Id = id;
        Kind = kind;
        (X, Y) = (x, y);
        Party = party;
        Started = started;
    }

    public int Id { get; }

    public IncidentKind Kind { get; }

    public double X { get; internal set; }

    public double Y { get; internal set; }

    /// <summary>The party it has happened to, or -1 for trouble that is just somewhere.</summary>
    public int Party { get; }

    public DateTime Started { get; }

    /// <summary>The member of staff dealing with it, or -1 while nobody is.</summary>
    public int Staff { get; internal set; } = -1;

    /// <summary>Ticks of treatment, sorting out or rest still to go.</summary>
    public int Left { get; internal set; }

    /// <summary>True while the person is resting in the first aid tent.</summary>
    public bool Resting { get; internal set; }

    /// <summary>True for someone hurt in a crush. They always need more than a sit down.</summary>
    public bool Crush { get; internal init; }

    /// <summary>True while it is waiting for someone to come.</summary>
    public bool Waiting => Staff < 0 && !Resting;
}

public sealed partial class FestivalGame
{
    /// <summary>How long someone who is ill waits for help before going home in a bad way.</summary>
    public const double IllPatienceMinutes = 20;

    /// <summary>How long trouble goes on with nobody dealing with it before it spoils things for everyone nearby.</summary>
    public const double TroublePatienceMinutes = 10;

    private const int TreatTicks = 240;
    private const int SortTicks = 180;
    private const int RestTicks = 1200;

    private readonly List<StaffMember> _staff = new();
    private readonly List<Incident> _incidents = new();
    private PathFinder _staffPaths = null!;
    private GameRandom _chance = null!;
    private int _nextStaff;
    private int _nextIncident;

    /// <summary>Everyone hired for the day.</summary>
    public IReadOnlyList<StaffMember> Staff => _staff;

    /// <summary>What needs staff right now.</summary>
    public IReadOnlyList<Incident> Incidents => _incidents;

    /// <summary>
    /// True for a festival that has to hire its own staff: gates need ticket staff, and people
    /// are taken ill and get into trouble. A festival with nothing to plan runs itself.
    /// </summary>
    public bool NeedsStaff => Planned;

    /// <summary>People helped by a first-aider today.</summary>
    public int Treated { get; private set; }

    /// <summary>People taken ill who waited too long and went home with no help.</summary>
    public int Untreated { get; private set; }

    /// <summary>Scuffles and lost children that security dealt with.</summary>
    public int TroubleSorted { get; private set; }

    /// <summary>Trouble nobody dealt with.</summary>
    public int TroubleIgnored { get; private set; }

    private void StartStaff()
    {
        _staffPaths = new PathFinder(Field);
        _chance = GameRandom.FromSeed(Seed, 31);
        Site.Changed += () => _staffPaths.Refresh();
        if (NeedsStaff)
        {
            Crowd.OpenLanes = OpenLanes;
        }
    }

    /// <summary>How many lanes of a gate have ticket staff on them.</summary>
    public int OpenLanes(int gate)
    {
        if (gate < 0 || gate >= Site.Gates.Count)
        {
            return 0;
        }

        var rect = Site.Gates[gate].Rect;
        return Math.Min(Catalogue.Of(Site.Gates[gate].Kind).Lanes, _staff.Count(s => s.Kind == StaffKind.TicketStaff && rect.Contains(s.Tile)));
    }

    /// <summary>Whether someone could be posted on a tile, counting the money.</summary>
    public BuildCheck CheckStaff(StaffKind kind, TilePoint tile)
    {
        var wage = Catalogue.Of(kind).Wage;
        if (!CanBuild)
        {
            return new BuildCheck(false, wage, "Nobody can be hired just now.");
        }

        if (kind == StaffKind.TicketStaff)
        {
            var gate = Site.Gates.ToList().FindIndex(g => g.Rect.Contains(tile));
            if (gate < 0)
            {
                return new BuildCheck(false, wage, "Ticket staff work at a gate. Click a gate.");
            }

            if (OpenLanes(gate) >= Catalogue.Of(Site.Gates[gate].Kind).Lanes)
            {
                return new BuildCheck(false, wage, "Every lane of this gate has someone on it.");
            }
        }
        else if (!Field.IsWalkable(tile) || !Field.Field.Contains(tile))
        {
            return new BuildCheck(false, wage, "They need a clear bit of the field to stand on.");
        }
        else if (_staff.Any(s => s.Tile == tile))
        {
            return new BuildCheck(false, wage, "Someone is posted here already.");
        }

        return wage > Money ? new BuildCheck(false, wage, $"That costs £{wage:N0} and there is £{Money:N0} in the bank.") : new BuildCheck(true, wage, null);
    }

    /// <summary>Hires someone for the day and posts them on a tile, if they can go there and there is the money.</summary>
    public BuildCheck HireStaff(StaffKind kind, TilePoint tile)
    {
        var check = CheckStaff(kind, tile);
        if (!check.Ok)
        {
            return check;
        }

        var (x, y) = FieldMap.CentreOf(tile);
        var heading = 0.0;
        if (kind == StaffKind.TicketStaff)
        {
            // They stand just inside their lane, facing the people coming in.
            var gate = Site.Gates.ToList().FindIndex(g => g.Rect.Contains(tile));
            var lane = OpenLanes(gate);
            var use = Site.GateUses[gate];
            (x, y) = Site.LaneMouth(gate, lane, -0.8);
            var info = Catalogue.Of(Site.Gates[gate].Kind);
            var aside = Math.Min(0.7, info.Width * FieldMap.TileMetres / info.Lanes * 0.3);
            (x, y) = (x - (use.OutY * aside), y + (use.OutX * aside));
            heading = Math.Atan2(use.OutX, use.OutY);
        }

        Spend("Staff wages", check.Cost);
        _staff.Add(new StaffMember(_nextStaff++, kind, tile, x, y, heading));
        return check;
    }

    /// <summary>The member of staff posted on or standing by a tile, if any.</summary>
    public StaffMember? StaffAt(TilePoint tile) =>
        _staff.FirstOrDefault(s => s.Tile == tile)
        ?? _staff.FirstOrDefault(s => Math.Abs(s.PostX - FieldMap.CentreOf(tile).X) < 0.6 && Math.Abs(s.PostY - FieldMap.CentreOf(tile).Y) < 0.6);

    /// <summary>
    /// Sends someone home. On the build day their wage comes back; once the gates are open they
    /// have turned up and are paid. Returns the money given back.
    /// </summary>
    public int DismissStaff(StaffMember member)
    {
        if (!CanBuild || !_staff.Remove(member))
        {
            return 0;
        }

        if (member.Incident >= 0 && _incidents.FirstOrDefault(i => i.Id == member.Incident) is { } incident)
        {
            incident.Staff = -1;
        }

        var refund = Phase == Phase.Build ? Catalogue.Of(member.Kind).Wage : 0;
        if (refund > 0)
        {
            Spend("Staff wages", -refund);
        }

        return refund;
    }

    private Party? PartyOf(Incident incident) => incident.Party >= 0 && incident.Party < Crowd.Parties.Count ? Crowd.Parties[incident.Party] : null;

    private void TickStaff(DateTime now)
    {
        if (NeedsStaff && Phase == Phase.Day && Clock.TickCount % 60 == 0)
        {
            Happenings(now);
        }

        for (var i = _incidents.Count - 1; i >= 0; i--)
        {
            var incident = _incidents[i];
            var party = PartyOf(incident);
            if (incident.Kind == IncidentKind.Ill && party?.State != PartyState.Unwell)
            {
                // They have gone home, or the night is over.
                Close(incident);
                continue;
            }

            if (incident.Resting)
            {
                if (--incident.Left <= 0)
                {
                    Crowd.Recover(party!);
                    Treated++;
                    Close(incident);
                }

                continue;
            }

            if (!incident.Waiting)
            {
                continue;
            }

            var wanted = incident.Kind == IncidentKind.Ill ? StaffKind.FirstAider : StaffKind.Security;
            var nearest = _staff.Where(s => s.Kind == wanted && s.IsFree).OrderBy(s => Distance(s.X, s.Y, incident.X, incident.Y)).FirstOrDefault();
            if (nearest is not null)
            {
                nearest.Incident = incident.Id;
                incident.Staff = nearest.Id;
                Walk(nearest, incident.X, incident.Y, StaffState.Going);
            }
            else if (incident.Kind == IncidentKind.Ill && now - incident.Started > TimeSpan.FromMinutes(IllPatienceMinutes))
            {
                // Nobody came. They get themselves home, and everyone who saw it thinks less of the festival.
                Untreated++;
                Crowd.Upset(incident.X, incident.Y, 8, 0.05);
                Crowd.GoHome(party!, unhappy: true);
                Close(incident);
            }
            else if (incident.Kind != IncidentKind.Ill && now - incident.Started > TimeSpan.FromMinutes(TroublePatienceMinutes))
            {
                TroubleIgnored++;
                Crowd.Upset(incident.X, incident.Y, 10, 0.06);
                Close(incident);
            }
        }

        foreach (var member in _staff)
        {
            Move(member);
        }
    }

    /// <summary>Once a minute: is anyone taken ill, and does any trouble start?</summary>
    private void Happenings(DateTime now)
    {
        // Both rolls are always made, so the day plays out the same whatever is going on.
        var illRoll = _chance.NextDouble();
        var whoIll = _chance.NextDouble();
        var troubleRoll = _chance.NextDouble();
        var whoTrouble = _chance.NextDouble();

        var about = Crowd.Parties.Where(p => p.State is PartyState.Standing or PartyState.Strolling or PartyState.Watching && Field.Field.Contains(FieldMap.TileAt(p.X, p.Y))).ToList();
        if (about.Count == 0)
        {
            return;
        }

        var evening = now.TimeOfDay >= TimeSpan.FromHours(17);
        var people = about.Sum(p => p.Members.Count);

        // A handful of people are taken ill over a day; more if people are going thirsty in the sun, and after an evening at the bar.
        // People packed in tight faint more, and lose their tempers more.
        var parched = about.Where(p => p.Thirst >= People.Crowd.Pressing).Sum(p => p.Members.Count);
        var packed = about.Where(p => Crowd.Density.At(p.X, p.Y) >= DensityMap.Busy).Sum(p => p.Members.Count);
        // Mud turns ankles.
        var slipping = Wetness <= 0 ? 0 : about.Where(p => MudAtPlace(p.X, p.Y) >= Slippery).Sum(p => p.Members.Count);
        var illChance = (people + (parched * 3) + (packed * 2) + (slipping * 3)) * (evening ? 0.007 : 0.005) / 60;
        if (illRoll < illChance)
        {
            var party = about[(int)(whoIll * about.Count)];
            Crowd.TakeIll(party);
            _incidents.Add(new Incident(_nextIncident++, IncidentKind.Ill, party.X, party.Y, party.Id, now));
        }

        // Trouble is rare by day and less rare once the bar has been open a while.
        var troubleChance = (people + (packed * 2)) / 180.0 * (evening ? 0.6 : 0.25) / 60;
        if (troubleRoll < troubleChance)
        {
            var party = about[(int)(whoTrouble * about.Count)];
            if (party.State != PartyState.Unwell)
            {
                _incidents.Add(new Incident(_nextIncident++, party.HasChild ? IncidentKind.LostChild : IncidentKind.Scuffle, party.X, party.Y, -1, now));
            }
        }
    }

    private static double Distance(double ax, double ay, double bx, double by) => Math.Sqrt(((ax - bx) * (ax - bx)) + ((ay - by) * (ay - by)));

    private void Close(Incident incident)
    {
        _incidents.Remove(incident);
        if (_staff.FirstOrDefault(s => s.Id == incident.Staff) is { } member)
        {
            member.Incident = -1;
            Walk(member, member.PostX, member.PostY, StaffState.Returning);
        }
    }

    private void Walk(StaffMember member, double x, double y, StaffState state)
    {
        member.State = state;
        member.Path = _staffPaths.FindPath(FieldMap.TileAt(member.X, member.Y), FieldMap.TileAt(x, y));
        member.PathIndex = 0;
    }

    /// <summary>Staff walk a little faster than the crowd: they have somewhere to be.</summary>
    private const double StaffSpeed = People.Crowd.WalkMetresPerTick * 1.35;

    /// <summary>Moves someone along their path. True when they have got there (or there is no way).</summary>
    private static bool Step(StaffMember member)
    {
        var path = member.Path;
        if (path is null)
        {
            return true;
        }

        var left = StaffSpeed;
        while (left > 0 && member.PathIndex < path.Count)
        {
            var (wx, wy) = FieldMap.CentreOf(path[member.PathIndex]);
            var dx = wx - member.X;
            var dy = wy - member.Y;
            var distance = Math.Sqrt((dx * dx) + (dy * dy));
            if (distance <= left)
            {
                (member.X, member.Y) = (wx, wy);
                left -= distance;
                member.PathIndex++;
            }
            else
            {
                member.X += dx / distance * left;
                member.Y += dy / distance * left;
                member.Heading = Math.Atan2(dx, dy);
                left = 0;
            }
        }

        if (member.PathIndex >= path.Count)
        {
            member.Path = null;
            return true;
        }

        return false;
    }

    private void Move(StaffMember member)
    {
        var incident = member.Incident >= 0 ? _incidents.FirstOrDefault(i => i.Id == member.Incident) : null;
        switch (member.State)
        {
            case StaffState.Going:
                if (incident is null)
                {
                    Walk(member, member.PostX, member.PostY, StaffState.Returning);
                }
                else if (Step(member))
                {
                    member.State = StaffState.Busy;
                    member.Heading = Math.Atan2(incident.X - member.X, incident.Y - member.Y);
                    incident.Left = incident.Kind == IncidentKind.Ill ? TreatTicks : SortTicks;
                }

                break;

            case StaffState.Busy:
                if (incident is null)
                {
                    Walk(member, member.PostX, member.PostY, StaffState.Returning);
                }
                else if (--incident.Left <= 0)
                {
                    Finish(member, incident);
                }

                break;

            case StaffState.Escorting:
                if (incident is null || PartyOf(incident) is not { } patient)
                {
                    Walk(member, member.PostX, member.PostY, StaffState.Returning);
                }
                else
                {
                    var there = Step(member);
                    Crowd.Carry(patient, member.X, member.Y);
                    (incident.X, incident.Y) = (member.X, member.Y);
                    if (there)
                    {
                        // Into a bed for a rest. The first-aider goes back to their post.
                        if (Tent() is { } tent)
                        {
                            var bed = _incidents.Count(i => i.Resting) % 2;
                            var (bx, by) = BedPlace(tent, bed);
                            Crowd.Carry(patient, bx, by);
                            (incident.X, incident.Y) = (bx, by);
                        }

                        incident.Resting = true;
                        incident.Left = RestTicks;
                        incident.Staff = -1;
                        member.Incident = -1;
                        Walk(member, member.PostX, member.PostY, StaffState.Returning);
                    }
                }

                break;

            case StaffState.Returning:
                if (Step(member))
                {
                    member.State = StaffState.AtPost;
                    member.Heading = member.PostHeading;
                }

                break;
        }
    }

    private PlacedStall? Tent() => Site.Stalls.FirstOrDefault(s => s.Kind == StallKind.FirstAidTent);

    /// <summary>Where someone lies on one of the tent's two beds, in metres.</summary>
    public static (double X, double Y) BedPlace(PlacedStall tent, int bed)
    {
        var (fx, fy, dx, dy) = SiteLayout.Front(tent.Rect, tent.Facing);
        var across = bed == 0 ? -1.15 : 1.15;
        return (fx - (dx * 1.6) - (dy * across), fy - (dy * 1.6) + (dx * across));
    }

    private void Finish(StaffMember member, Incident incident)
    {
        if (incident.Kind != IncidentKind.Ill)
        {
            TroubleSorted++;
            Close(incident);
            return;
        }

        var patient = PartyOf(incident)!;

        // Most people are right as rain after some water and a sit down. The rest need a lie down.
        var roll = _chance.NextDouble();
        if (!incident.Crush && roll < 0.65)
        {
            Crowd.Recover(patient);
            Treated++;
            Close(incident);
        }
        else if (Tent() is { } tent && _incidents.Count(i => i.Resting) < Catalogue.Of(StallKind.FirstAidTent).Servers)
        {
            var (fx, fy, dx, dy) = SiteLayout.Front(tent.Rect, tent.Facing);
            Walk(member, fx + (dx * 0.6), fy + (dy * 0.6), StaffState.Escorting);
        }
        else
        {
            // Nowhere for them to rest: they are seen to, then sent home.
            Treated++;
            Crowd.GoHome(patient, unhappy: false);
            Close(incident);
        }
    }

    private StaffSave SaveStaff() => new(
        _staff.Select(s => new StaffMemberSave(s.Id, s.Kind, s.Tile, s.PostX, s.PostY, s.PostHeading, s.X, s.Y, s.Heading, s.State, s.Incident)).ToList(),
        _incidents.Select(i => new IncidentSave(i.Id, i.Kind, i.X, i.Y, i.Party, i.Started, i.Staff, i.Left, i.Resting, i.Crush)).ToList(),
        _chance.State, _nextStaff, _nextIncident, Treated, Untreated, TroubleSorted, TroubleIgnored);

    private void RestoreStaff(StaffSave save)
    {
        foreach (var s in save.Staff ?? new())
        {
            var member = new StaffMember(s.Id, s.Kind, s.Tile, s.PostX, s.PostY, s.PostHeading) { X = s.X, Y = s.Y, Heading = s.Heading, State = Enum.IsDefined(s.State) ? s.State : StaffState.AtPost, Incident = s.Incident };
            _staff.Add(member);
        }

        foreach (var i in save.Incidents ?? new())
        {
            _incidents.Add(new Incident(i.Id, i.Kind, i.X, i.Y, i.Party, i.Started) { Staff = i.Staff, Left = i.Left, Resting = i.Resting, Crush = i.Crush });
        }

        // Anyone who was walking somewhere works out their way again.
        foreach (var member in _staff)
        {
            var incident = _incidents.FirstOrDefault(i => i.Id == member.Incident);
            switch (member.State)
            {
                case StaffState.Going when incident is not null:
                    Walk(member, incident.X, incident.Y, StaffState.Going);
                    break;
                case StaffState.Escorting when Tent() is { } tent:
                    var (fx, fy, dx, dy) = SiteLayout.Front(tent.Rect, tent.Facing);
                    Walk(member, fx + (dx * 0.6), fy + (dy * 0.6), StaffState.Escorting);
                    break;
                case StaffState.Returning:
                    Walk(member, member.PostX, member.PostY, StaffState.Returning);
                    break;
            }
        }

        _chance = new GameRandom(save.Chance);
        _nextStaff = Math.Max(save.NextStaff, _staff.Count == 0 ? 0 : _staff.Max(s => s.Id) + 1);
        _nextIncident = Math.Max(save.NextIncident, _incidents.Count == 0 ? 0 : _incidents.Max(i => i.Id) + 1);
        (Treated, Untreated, TroubleSorted, TroubleIgnored) = (save.Treated, save.Untreated, save.TroubleSorted, save.TroubleIgnored);
    }
}

/// <summary>One member of staff as written to a save file.</summary>
public sealed record StaffMemberSave(int Id, StaffKind Kind, TilePoint Tile, double PostX, double PostY, double PostHeading, double X, double Y, double Heading, StaffState State, int Incident);

/// <summary>One incident as written to a save file.</summary>
public sealed record IncidentSave(int Id, IncidentKind Kind, double X, double Y, int Party, DateTime Started, int Staff, int Left, bool Resting, bool Crush = false);

/// <summary>The staff and what they are dealing with, as written to a save file.</summary>
public sealed record StaffSave(List<StaffMemberSave>? Staff, List<IncidentSave>? Incidents, ulong Chance, int NextStaff, int NextIncident, int Treated, int Untreated, int TroubleSorted, int TroubleIgnored);
