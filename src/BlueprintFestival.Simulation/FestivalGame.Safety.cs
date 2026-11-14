using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation;

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

    /// <summary>How long an act stops for when the player pauses it.</summary>
    public const double PauseMinutes = 6;

    /// <summary>How far from where they stand a member of security keeps the crowd in hand, in metres.</summary>
    public const double GuardReach = 3.5;

    /// <summary>How far back from a front barrier security standing behind it keep the crowd in hand, in metres.</summary>
    public const double PitReach = 8;

    private readonly Dictionary<int, int> _redMinutes = new();
    private readonly Dictionary<PlacedStage, double> _barriers = new();
    private GameRandom _danger = null!;
    private DateTime _pausedUntil;
    private bool _wasRed;

    /// <summary>How crowded every part of the map is.</summary>
    public DensityMap Density => Crowd.Density;

    /// <summary>The worst the crowd is anywhere right now.</summary>
    public Crowding CrowdStatus => DensityMap.StatusOf(Density.Worst);

    /// <summary>People hurt in a crush today.</summary>
    public int CrushInjuries { get; private set; }

    /// <summary>People security lifted out of a crush before they were hurt.</summary>
    public int PulledOut { get; private set; }

    /// <summary>True once so many people have been hurt that the day was called off.</summary>
    public bool SeriousIncident { get; private set; }

    /// <summary>How many times the player has paused an act today.</summary>
    public int ActsPaused { get; private set; }

    /// <summary>True while an act is stopped to let the crowd ease.</summary>
    public bool ActPaused => Clock.Now < _pausedUntil;

    /// <summary>Raised the moment somewhere first becomes a crush risk, with the place in metres.</summary>
    public event Action<double, double>? CrushWarning;

    /// <summary>Raised when someone is hurt in a crush, with the place in metres.</summary>
    public event Action<double, double>? CrushInjury;

    private void StartSafety()
    {
        _danger = GameRandom.FromSeed(Seed, 47);
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
                if (Site.FenceAt(tile) == FenceKind.CrowdBarrier)
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
    public bool HasPitCrew(PlacedStage stage)
    {
        var barrier = BarrierDepth(stage);
        if (barrier <= 0)
        {
            return false;
        }

        var half = HalfWidth(stage) + 1.5;
        return _staff.Any(s =>
        {
            if (s.Kind != StaffKind.Security || s.State != StaffState.AtPost)
            {
                return false;
            }

            var (deep, across) = InFrontOf(stage, s.X, s.Y);
            return deep >= 0 && deep <= barrier && Math.Abs(across) <= half;
        });
    }

    /// <summary>
    /// True for a place (in metres) where security have the crowd in hand: close to anyone
    /// standing at their post, or behind a front barrier that has security in the strip.
    /// </summary>
    public bool Stewarded(double x, double y)
    {
        foreach (var s in _staff)
        {
            if (s.Kind == StaffKind.Security && s.State == StaffState.AtPost && Distance(s.X, s.Y, x, y) <= GuardReach)
            {
                return true;
            }
        }

        foreach (var stage in Site.Stages)
        {
            if (!HasPitCrew(stage))
            {
                continue;
            }

            var (deep, across) = InFrontOf(stage, x, y);
            if (deep >= 0 && deep <= BarrierDepth(stage) + PitReach && Math.Abs(across) <= HalfWidth(stage) + 4)
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
        if (Phase != Phase.Day || ActPaused || Show.Playing(Now) is null || Show.Stage is not { } stage)
        {
            return false;
        }

        _pausedUntil = Now.AddMinutes(PauseMinutes);
        ActsPaused++;
        Crowd.Ease();
        var (fx, fy, _, _) = RunningOrder.Front(stage);
        Crowd.Upset(fx, fy, 40, 0.015);
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
        if (density.RedSquares == 0)
        {
            _redMinutes.Clear();
            _wasRed = false;
            return;
        }

        for (var sy = 0; sy < density.Down; sy++)
        {
            for (var sx = 0; sx < density.Across; sx++)
            {
                var key = (sy * density.Across) + sx;
                var perMetre = density.In(sx, sy);
                if (perMetre < DensityMap.Crush)
                {
                    _redMinutes.Remove(key);
                    continue;
                }

                var minutes = _redMinutes.GetValueOrDefault(key) + 1;
                _redMinutes[key] = minutes;
                if (minutes == 2 && !_wasRed)
                {
                    // Red for a second minute running is no passing knot of people: warn the player.
                    _wasRed = true;
                    var (wx, wy) = DensityMap.CentreOf(sx, sy);
                    CrushWarning?.Invoke(wx, wy);
                }

                if (minutes > RedGraceMinutes && _danger.NextDouble() < 0.05 + ((perMetre - DensityMap.Crush) * 0.05))
                {
                    Hurt(sx, sy, now);
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
        _incidents.Add(new Incident(_nextIncident++, IncidentKind.Ill, party.X, party.Y, party.Id, now) { Crush = true });
        CrushInjuries++;
        Crowd.Upset(party.X, party.Y, 8, 0.04);
        CrushInjury?.Invoke(party.X, party.Y);
        if (CrushInjuries >= SeriousAt && !SeriousIncident)
        {
            // Too many people hurt: the music stops and everyone is sent home.
            SeriousIncident = true;
            Show.CallOff(now);
            Crowd.SendEveryoneHome(now, 0.25);
        }
    }

    private SafetySave SaveSafety() => new(CrushInjuries, PulledOut, SeriousIncident, ActsPaused, _pausedUntil, _danger.State, Show.CalledOff)
    {
        RedMinutes = _redMinutes.Count > 0 ? new Dictionary<int, int>(_redMinutes) : null,
        Warned = _wasRed,
    };

    private void RestoreSafety(SafetySave save)
    {
        (CrushInjuries, PulledOut, SeriousIncident, ActsPaused, _pausedUntil) = (save.CrushInjuries, save.PulledOut, save.SeriousIncident, save.ActsPaused, save.PausedUntil);
        _danger = new GameRandom(save.Danger);
        _wasRed = save.Warned;
        _redMinutes.Clear();
        foreach (var (square, minutes) in save.RedMinutes ?? new())
        {
            _redMinutes[square] = minutes;
        }

        if (save.CalledOff is { } when)
        {
            Show.CallOff(when);
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
}
