using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation;

/// <summary>What the player can do when a crush is building.</summary>
public enum CrushChoice
{
    /// <summary>Stop the music while the PA asks everyone to step back, then start again.</summary>
    Announce,

    /// <summary>Send the nearest security in to hold the spot and lift people out.</summary>
    SendSecurity,

    /// <summary>End the act's set there and then.</summary>
    CancelAct,

    /// <summary>Leave it and hope the crowd sorts itself out.</summary>
    LeaveIt,
}

/// <summary>
/// Crowd safety. The crowd is measured in two-metre squares every minute. A square that stays a
/// crush risk hurts people; enough of them hurt and the day is called off. The player prevents it
/// with a front barrier, security at the front, and by pausing the act.
/// </summary>
public sealed partial class FestivalGame
{
    /// <summary>How many minutes a square can be a crush risk before people in it start to be hurt.</summary>
    public const int RedGraceMinutes = 6;

    /// <summary>How many people are hurt in crushes before the day is called off.</summary>
    public const int SeriousAt = 5;

    /// <summary>How long an act stops for while the PA asks the crowd to step back.</summary>
    public const double PauseMinutes = 4;

    /// <summary>How many security go when the player sends them into a crush.</summary>
    public const int SecurityCallOut = 3;

    /// <summary>The overtime paid to each member of security sent into a crush, in pounds.</summary>
    public const int Overtime = 25;

    /// <summary>How much mood everyone watching a stage loses when the music stops for an announcement.</summary>
    public const double AnnounceMood = 0.03;

    /// <summary>How much mood the people watching an act lose when it is cancelled, and everyone else on site.</summary>
    public const double CancelMoodWatching = 0.12;

    public const double CancelMoodEveryone = 0.04;

    /// <summary>The chance each minute that a crush the player chose to leave alone eases by itself.</summary>
    public const double SettlesByItself = 0.08;

    /// <summary>How far from where they stand a member of security keeps the crowd in hand, in metres.</summary>
    public const double GuardReach = 3.5;

    /// <summary>How far back from a front barrier each member of security standing behind it keeps the crowd in hand, in metres.</summary>
    public const double PitReach = 8;

    private readonly Dictionary<int, int> _redMinutes = new();
    private readonly Dictionary<PlacedStage, double> _barriers = new();
    private GameRandom _danger = null!;
    private GameRandom _settle = null!;
    private readonly Dictionary<int, DateTime> _cutShort = new();
    private bool _handled;
    private bool _leftAlone;
    private DateTime _pausedUntil;
    private bool _wasRed;
    private DayNote? _crushNote;
    private const string WaitingForChoice = "Waiting for your choice";

    /// <summary>Writes the player's choice against the crush on the day's log.</summary>
    private void Chose(string what)
    {
        if (_crushNote is { } note)
        {
            (note.Outcome, note.Tone) = (what, DayTone.Open);
        }
        else
        {
            Note(DayNoteKind.Choice, what, "", DayTone.Plain);
        }
    }

    /// <summary>How crowded every part of the map is.</summary>
    public DensityMap Density => Crowd.Density;

    /// <summary>The worst the crowd is anywhere right now.</summary>
    public Crowding CrowdStatus => DensityMap.StatusOf(Density.Worst);

    /// <summary>True while some square that security do not have in hand has been a crush risk for two minutes or more: no passing knot of people, but a crowd that is stuck.</summary>
    public bool CrushBuilding => _redMinutes.Any(square => square.Value >= 2 && !InHand(square.Key));

    /// <summary>True if security have the crowd in hand in a square of the crowd map, by the square's number.</summary>
    private bool InHand(int square)
    {
        var (x, y) = DensityMap.CentreOf(square % Density.Across, square / Density.Across);
        return Stewarded(x, y);
    }

    /// <summary>People hurt in a crush today.</summary>
    public int CrushInjuries { get; private set; }

    /// <summary>People security lifted out of a crush before they were hurt.</summary>
    public int PulledOut { get; private set; }

    /// <summary>True once so many people have been hurt that the day was called off.</summary>
    public bool SeriousIncident { get; private set; }

    /// <summary>How many times the player has stopped the music for an announcement today.</summary>
    public int ActsPaused { get; private set; }

    /// <summary>How many times the player has sent security into a crush today.</summary>
    public int SecuritySent { get; private set; }

    /// <summary>How many acts the player has cancelled today.</summary>
    public int ActsCancelled { get; private set; }

    /// <summary>How many times the player has chosen to leave a crush alone today.</summary>
    public int TimesLeftAlone { get; private set; }

    /// <summary>People hurt in a crush the player did nothing about. The council hears about every one.</summary>
    public int HurtLeftAlone { get; private set; }

    /// <summary>True once the player has done something about the crush going on now (announced, sent security or cancelled the act).</summary>
    public bool CrushHandled => _handled;

    /// <summary>True once the player has chosen to leave the crush going on now alone.</summary>
    public bool CrushLeftAlone => _leftAlone;

    /// <summary>How much of the crowd still listens to an announcement: everyone the first time, fewer each time after.</summary>
    public double AnnouncementHeard => ActsPaused switch { 0 => 1, 1 => 0.67, _ => 0.34 };

    /// <summary>How many security could be sent into a crush right now.</summary>
    public int SecurityFree => Math.Min(SecurityCallOut, _staff.Count(s => s.Kind == StaffKind.Security && s.IsFree));

    /// <summary>The act on at the stage nearest the crush, if one is on.</summary>
    public Slot? ActAtCrush => ShowAtCrush()?.Drawing(Now);

    /// <summary>Raised when the player stops the music for an announcement, with which announcement it is (counting from 0).</summary>
    public event Action<int>? Announced;

    /// <summary>Raised when the player cancels an act.</summary>
    public event Action<Slot>? ActCancelled;

    /// <summary>True while an act is stopped to let the crowd ease.</summary>
    public bool ActPaused => Clock.Now < _pausedUntil;

    /// <summary>Raised the moment somewhere first becomes a crush risk, with the place in metres.</summary>
    public event Action<double, double>? CrushWarning;

    /// <summary>Raised when someone is hurt in a crush, with the place in metres.</summary>
    public event Action<double, double>? CrushInjury;

    private void StartSafety()
    {
        _danger = GameRandom.FromSeed(Seed, 47);
        _settle = GameRandom.FromSeed(Seed, 48);
        Crowd.Stewarded = Stewarded;
        Crowd.BarrierDepth = BarrierDepth;
        Site.Changed += _barriers.Clear;
    }

    /// <summary>
    /// How far in front of a stage its front barrier stands, in metres, or 0 if it has none. A
    /// run of crowd barrier counts when it crosses most of the stage's width a little way in front.
    /// </summary>
    public double BarrierDepth(PlacedStage stage)
    {
        ArgumentNullException.ThrowIfNull(stage);
        if (_barriers.TryGetValue(stage, out var known))
        {
            return known;
        }

        var (_, _, dx, dy) = RunningOrder.Front(stage);
        var rect = stage.Rect;
        var width = dx == 0 ? rect.Width : rect.Height;
        var found = 0.0;
        for (var deep = 2; deep <= 12 && found == 0; deep++)
        {
            var covered = 0;
            for (var i = 0; i < width; i++)
            {
                var tile = dx == 0
                    ? new TilePoint(rect.X + i, dy > 0 ? rect.Bottom - 1 + deep : rect.Y - deep)
                    : new TilePoint(dx > 0 ? rect.Right - 1 + deep : rect.X - deep, rect.Y + i);
                if (Site.FenceAt(tile) == FenceKind.CrowdBarrier || Site.IsAreaBarrier(tile))
                {
                    covered++;
                }
            }

            if (covered * 10 >= width * 7)
            {
                found = deep * FieldMap.TileMetres;
            }
        }

        _barriers[stage] = found;
        return found;
    }

    /// <summary>How deep in front of a stage a place is, and how far to one side of its middle, in metres.</summary>
    private static (double Deep, double Across) InFrontOf(PlacedStage stage, double x, double y)
    {
        var (fx, fy, dx, dy) = RunningOrder.Front(stage);
        return (((x - fx) * dx) + ((y - fy) * dy), ((x - fx) * -dy) + ((y - fy) * dx));
    }

    private static double HalfWidth(PlacedStage stage)
    {
        var (_, _, dx, _) = RunningOrder.Front(stage);
        return (dx == 0 ? stage.Rect.Width : stage.Rect.Height) * FieldMap.TileMetres / 2;
    }

    /// <summary>True if a member of security is at their post in the clear strip between a stage and its front barrier.</summary>
    public bool HasPitCrew(PlacedStage stage) => PitCrew(stage) > 0;

    /// <summary>How many security are at their posts in the clear strip between a stage and its front barrier.</summary>
    public int PitCrew(PlacedStage stage)
    {
        ArgumentNullException.ThrowIfNull(stage);
        var barrier = BarrierDepth(stage);
        if (barrier <= 0)
        {
            return 0;
        }

        var half = HalfWidth(stage) + 1.5;
        var crew = 0;
        foreach (var s in _staff)
        {
            if (s.Kind != StaffKind.Security || s.State != StaffState.AtPost)
            {
                continue;
            }

            var (deep, across) = InFrontOf(stage, s.X, s.Y);
            if (deep >= 0 && deep <= barrier && Math.Abs(across) <= half)
            {
                crew++;
            }
        }

        return crew;
    }

    /// <summary>True for security whose post is in the clear strip between a stage and its front barrier.</summary>
    private bool InPit(StaffMember member)
    {
        if (member.Kind != StaffKind.Security)
        {
            return false;
        }

        foreach (var stage in Site.Stages)
        {
            var barrier = BarrierDepth(stage);
            var (deep, across) = InFrontOf(stage, member.PostX, member.PostY);
            if (barrier > 0 && deep >= 0 && deep <= barrier && Math.Abs(across) <= HalfWidth(stage) + 1.5)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True for a place (in metres) where security have the crowd in hand: close to anyone
    /// standing at their post, or behind a front barrier that has security in the strip.
    /// </summary>
    public bool Stewarded(double x, double y)
    {
        foreach (var s in _staff)
        {
            if (s.Kind == StaffKind.Security && s.State is StaffState.AtPost or StaffState.Holding or StaffState.Patrolling or StaffState.Looking && Distance(s.X, s.Y, x, y) <= GuardReach)
            {
                return true;
            }
        }

        foreach (var stage in Site.Stages)
        {
            var crew = PitCrew(stage);
            if (crew == 0)
            {
                continue;
            }

            // Each of the pit crew looks after another stretch of the crowd, further back and wider.
            var (deep, across) = InFrontOf(stage, x, y);
            if (deep >= 0 && deep <= BarrierDepth(stage) + (PitReach * crew) + 1 && Math.Abs(across) <= HalfWidth(stage) + 2 + (5 * crew))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Stops the act for a few minutes so the crowd can ease: everyone watching from a busy square
    /// moves further back. The audience do not thank you for it. False if no act is on, or one is
    /// already stopped.
    /// </summary>
    public bool PauseAct()
    {
        // With a crowd packing in early for a big act, the PA can ask them to step back before the set too.
        if (Phase != Phase.Day || ActPaused || !(MusicOn || CrowdWaiting))
        {
            return false;
        }

        var heard = AnnouncementHeard;
        _pausedUntil = Now.AddMinutes(PauseMinutes);
        Crowd.Ease(heard);
        foreach (var show in new[] { Show, SecondShow })
        {
            if (show.Drawing(Now) is not null && show.Stage is { } stage)
            {
                var (fx, fy, _, _) = RunningOrder.Front(stage);
                Crowd.Upset(fx, fy, 40, AnnounceMood);
            }
        }

        Chose("You stopped the music for an announcement");
        Announced?.Invoke(ActsPaused);
        ActsPaused++;
        _handled = true;
        return true;
    }

    /// <summary>Stops the music while the PA asks everyone to step back. The same as pausing the act.</summary>
    public bool Announce() => PauseAct();

    /// <summary>Does what the player chose for the crush going on. False if it could not be done.</summary>
    public bool Choose(CrushChoice choice) => choice switch
    {
        CrushChoice.Announce => Announce(),
        CrushChoice.SendSecurity => SendSecurity() > 0,
        CrushChoice.CancelAct => CancelAct(),
        _ => LeaveIt(),
    };

    /// <summary>
    /// Where the crush is worst, in metres: the most packed square security do not have in hand,
    /// or the most packed square of all.
    /// </summary>
    public (double X, double Y) CrushSpot
    {
        get
        {
            var best = -1;
            var worst = 0.0;
            foreach (var square in _redMinutes.Keys)
            {
                var (sx, sy) = (square % Density.Across, square / Density.Across);
                var perMetre = Density.In(sx, sy);
                if (!InHand(square) && perMetre > worst)
                {
                    (best, worst) = (square, perMetre);
                }
            }

            return best < 0 ? Density.WorstAt : DensityMap.CentreOf(best % Density.Across, best / Density.Across);
        }
    }

    /// <summary>The line-up of the stage with an act on (or a big act about to come on) whose front is nearest the crush, or null if there is none.</summary>
    private RunningOrder? ShowAtCrush()
    {
        var (x, y) = CrushSpot;
        return new[] { Show, SecondShow }
            .Where(show => show.Drawing(Now) is not null && show.Stage is not null)
            .OrderBy(show =>
            {
                var (fx, fy, _, _) = RunningOrder.Front(show.Stage!);
                return Distance(fx, fy, x, y);
            })
            .FirstOrDefault();
    }

    /// <summary>
    /// Sends the nearest free security into the crush. They walk there, stand in it for ten
    /// minutes keeping the crowd back and lifting people out, then go back to their posts, which
    /// stand empty meanwhile. Each is paid overtime. The music plays on. Returns how many went.
    /// </summary>
    public int SendSecurity()
    {
        if (Phase != Phase.Day || Density.RedSquares == 0)
        {
            return 0;
        }

        var (x, y) = CrushSpot;
        var going = _staff.Where(s => s.Kind == StaffKind.Security && s.IsFree)
            .OrderBy(s => Distance(s.X, s.Y, x, y)).Take(SecurityCallOut).ToList();
        for (var i = 0; i < going.Count; i++)
        {
            // Spread a little across the crush rather than all in one spot.
            var (hx, hy) = (x + ((i - 1) * 2.0), y + ((i % 2) * 1.0));
            if (!Field.IsWalkable(FieldMap.TileAt(hx, hy)))
            {
                (hx, hy) = (x, y);
            }

            var member = going[i];
            (member.HoldX, member.HoldY) = (hx, hy);
            Walk(member, hx, hy, StaffState.Sent);
        }

        if (going.Count > 0)
        {
            Spend("Security overtime", Overtime * going.Count);
            SecuritySent++;
            Chose($"You sent {going.Count} security in");
            _handled = true;
        }

        return going.Count;
    }

    /// <summary>
    /// Cancels the act on at the stage nearest the crush. They walk off there and then, and the
    /// crowd spreads out at once. Everyone who was watching is let down, everyone else a little, and
    /// some of the act's fans go home. The fee is not given back. False if no act is on.
    /// </summary>
    public bool CancelAct()
    {
        if (Phase != Phase.Day || ShowAtCrush() is not { } show || show.Drawing(Now) is not { } slot || show.Stage is null)
        {
            return false;
        }

        Crowd.Ease();
        Crowd.Upset(0, 0, 1e6, CancelMoodEveryone);
        foreach (var party in Crowd.Parties.Where(p => p.IsOnSite && p.Watch == slot.Index).ToList())
        {
            party.Mood = Math.Max(0, party.Mood - (CancelMoodWatching - CancelMoodEveryone));
            if ((uint)party.Id * 2246822519u % 4 == 0)
            {
                // One fan in four has come for this act alone and goes home.
                Crowd.GoHome(party, unhappy: false);
            }
        }

        show.CutShort(slot.Index, Now);
        _cutShort[slot.Index] = Now;
        ActsCancelled++;
        Chose($"You cancelled {slot.Act.Name}");
        _handled = true;
        ActCancelled?.Invoke(slot);
        return true;
    }

    /// <summary>
    /// Leaves the crush alone. The music plays on and nobody is put out, and now and then a crowd
    /// sorts itself out. Anyone hurt in it counts against the festival. False if there is no crush.
    /// </summary>
    public bool LeaveIt()
    {
        if (Phase != Phase.Day || _leftAlone || (!CrushBuilding && Density.RedSquares == 0))
        {
            return false;
        }

        _leftAlone = true;
        TimesLeftAlone++;
        Chose("You left it alone");
        return true;
    }

    /// <summary>Once a minute: counts how long each square has been a crush risk, and hurts people in the ones left too long.</summary>
    private void TickSafety(DateTime now)
    {
        if (!Planned || Phase != Phase.Day || Clock.TickCount % 60 != 30)
        {
            return;
        }

        var density = Crowd.Density;
        LiftOut();
        if (density.RedSquares == 0)
        {
            // The crush is over: the next one is a fresh choice.
            _redMinutes.Clear();
            if (_crushNote is { Tone: DayTone.Open, Outcome: WaitingForChoice } eased)
            {
                (eased.Outcome, eased.Tone) = ("Eased off by itself", DayTone.Good);
            }
            else if (_crushNote is { } over && over.Tone == DayTone.Open)
            {
                over.Tone = DayTone.Good;
            }

            _crushNote = null;
            _wasRed = false;
            _handled = false;
            _leftAlone = false;
            return;
        }

        if (_leftAlone && !_handled && _settle.NextDouble() < SettlesByItself)
        {
            // Left alone, people at the front sometimes sort it out themselves.
            Crowd.Ease(0.6);
        }

        for (var sy = 0; sy < density.Down; sy++)
        {
            for (var sx = 0; sx < density.Across; sx++)
            {
                var key = (sy * density.Across) + sx;
                var perMetre = density.In(sx, sy);
                if (perMetre <= DensityMap.Crush)
                {
                    _redMinutes.Remove(key);
                    continue;
                }

                var minutes = _redMinutes.GetValueOrDefault(key) + 1;
                _redMinutes[key] = minutes;
                if (minutes >= 2 && !_wasRed && !InHand(key))
                {
                    // Red for a second minute running is no passing knot of people: warn the player,
                    // unless security are right there and already lifting people out.
                    _wasRed = true;
                    var (wx, wy) = DensityMap.CentreOf(sx, sy);
                    _crushNote = Note(DayNoteKind.Crush, $"Crowd too tight {Whereabouts(wx, wy)}".TrimEnd(), WaitingForChoice, DayTone.Open);
                    CrushWarning?.Invoke(wx, wy);
                }

                if (minutes > RedGraceMinutes && _danger.NextDouble() < 0.05 + ((perMetre - DensityMap.Crush) * 0.05))
                {
                    Hurt(sx, sy, now);
                }
            }
        }
    }

    /// <summary>Once a minute, security sent into a crush lift a few people out of the packed squares around them.</summary>
    private void LiftOut()
    {
        foreach (var guard in _staff.Where(s => s.State == StaffState.Holding))
        {
            var lifted = 0;
            foreach (var party in Crowd.Parties.Where(p => p.State is PartyState.Watching or PartyState.Standing && Distance(p.X, p.Y, guard.X, guard.Y) <= GuardReach + 1
                && Density.At(p.X, p.Y) > DensityMap.Busy).ToList())
            {
                Crowd.MoveBack(party);
                party.Mood = Math.Max(0, party.Mood - 0.02);
                PulledOut++;
                if (++lifted == 3)
                {
                    break;
                }
            }
        }
    }

    /// <summary>Someone in a packed square goes down, unless security can get them out first.</summary>
    private void Hurt(int squareX, int squareY, DateTime now)
    {
        var inSquare = Crowd.Parties
            .Where(p => p.State is PartyState.Watching or PartyState.Standing or PartyState.Strolling or PartyState.Leaving
                && p.Members.Any(m => Crowd.Density.SquareOf(p.X + m.OffsetX, p.Y + m.OffsetY) == (squareX, squareY)))
            .ToList();
        if (inSquare.Count == 0)
        {
            return;
        }

        var party = inSquare[_danger.Next(inSquare.Count)];
        if (Stewarded(party.X, party.Y))
        {
            PulledOut++;
            Crowd.MoveBack(party);
            return;
        }

        Crowd.TakeIll(party);
        Raise(new Incident(_nextIncident++, IncidentKind.Ill, party.X, party.Y, party.Id, now) { Crush = true }, DayNoteKind.Hurt, "Hurt in a crush");
        CrushInjuries++;
        if (!_handled)
        {
            HurtLeftAlone++;
        }

        Crowd.Upset(party.X, party.Y, 8, 0.04);
        CrushInjury?.Invoke(party.X, party.Y);
        if (CrushInjuries >= SeriousAt && !SeriousIncident)
        {
            // Too many people hurt: the music stops and everyone is sent home.
            SeriousIncident = true;
            Note(DayNoteKind.CalledOff, "The day was called off", $"{CrushInjuries} hurt in crushes: everyone sent home", DayTone.Bad);
            Show.CallOff(now);
            SecondShow.CallOff(now);
            Crowd.SendEveryoneHome(now, 0.25);
        }
    }

    private SafetySave SaveSafety() => new(CrushInjuries, PulledOut, SeriousIncident, ActsPaused, _pausedUntil, _danger.State, Show.CalledOff)
    {
        RedMinutes = _redMinutes.Count > 0 ? new Dictionary<int, int>(_redMinutes) : null,
        Warned = _wasRed,
        SecuritySent = SecuritySent,
        ActsCancelled = ActsCancelled,
        TimesLeftAlone = TimesLeftAlone,
        HurtLeftAlone = HurtLeftAlone,
        Handled = _handled,
        LeftAlone = _leftAlone,
        Settle = _settle.State,
        CutShort = _cutShort.Count > 0 ? new Dictionary<int, DateTime>(_cutShort) : null,
        Places = Crowd.SavePlaces(),
    };

    private void RestoreSafety(SafetySave save)
    {
        (CrushInjuries, PulledOut, SeriousIncident, ActsPaused, _pausedUntil) = (save.CrushInjuries, save.PulledOut, save.SeriousIncident, save.ActsPaused, save.PausedUntil);
        _danger = new GameRandom(save.Danger);
        _wasRed = save.Warned;
        (SecuritySent, ActsCancelled, TimesLeftAlone, HurtLeftAlone, _handled, _leftAlone) =
            (save.SecuritySent, save.ActsCancelled, save.TimesLeftAlone, save.HurtLeftAlone, save.Handled, save.LeftAlone);
        if (save.Settle != 0)
        {
            _settle = new GameRandom(save.Settle);
        }

        foreach (var (set, at) in save.CutShort ?? new())
        {
            (set >= RunningOrder.SecondBase ? SecondShow : Show).CutShort(set, at);
            _cutShort[set] = at;
        }
        Crowd.RestorePlaces(save.Places);
        _redMinutes.Clear();
        foreach (var (square, minutes) in save.RedMinutes ?? new())
        {
            _redMinutes[square] = minutes;
        }

        if (save.CalledOff is { } when)
        {
            Show.CallOff(when);
            SecondShow.CallOff(when);
        }
    }
}

/// <summary>What is saved about crowd safety on the day.</summary>
public sealed record SafetySave(int CrushInjuries, int PulledOut, bool SeriousIncident, int ActsPaused, DateTime PausedUntil, ulong Danger, DateTime? CalledOff)
{
    /// <summary>How many minutes running each square has been a crush risk, by the square's number.</summary>
    public Dictionary<int, int>? RedMinutes { get; init; }

    /// <summary>True if the player has already been warned about the crush that is going on.</summary>
    public bool Warned { get; init; }

    /// <summary>The places the audience have picked to watch from.</summary>
    public WatchingPlaces? Places { get; init; }

    public int SecuritySent { get; init; }

    public int ActsCancelled { get; init; }

    public int TimesLeftAlone { get; init; }

    public int HurtLeftAlone { get; init; }

    /// <summary>True if the player had already done something about the crush going on.</summary>
    public bool Handled { get; init; }

    /// <summary>True if the player had chosen to leave the crush going on alone.</summary>
    public bool LeftAlone { get; init; }

    /// <summary>The dice for a crush left alone sorting itself out.</summary>
    public ulong Settle { get; init; }

    /// <summary>Sets that were cancelled, by number, and when.</summary>
    public Dictionary<int, DateTime>? CutShort { get; init; }
}
