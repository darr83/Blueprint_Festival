namespace BlueprintFestival.Simulation.Core;

/// <summary>The speeds the player can run the game at.</summary>
public enum GameSpeed
{
    Paused = 0,
    Normal = 1,
    Fast = 2,
    VeryFast = 4,
    Fastest = 8,
}

/// <summary>
/// The game clock. Real time is collected and turned into whole one-second ticks, so the festival
/// plays out the same at every speed and frame rate.
/// </summary>
public sealed class GameClock
{
    /// <summary>Game time that passes in one tick.</summary>
    public static readonly TimeSpan TickLength = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Game seconds per real second at 1x. An hour takes eight real minutes: slow enough to watch
    /// people walk about. At 8x an hour takes one minute.
    /// </summary>
    public const double GameSecondsPerRealSecond = 7.5;

    /// <summary>The most ticks run in one frame, so a long stall cannot freeze the game.</summary>
    public const int MaxTicksPerAdvance = 240;

    /// <summary>A new game opens on the morning of the festival: a Saturday in July.</summary>
    public static readonly DateTime DefaultStart = new(2027, 7, 17, 8, 0, 0, DateTimeKind.Unspecified);

    private double _owedGameSeconds;

    public GameClock(DateTime? start = null)
    {
        Now = start ?? DefaultStart;
    }

    public DateTime Now { get; private set; }

    public long TickCount { get; private set; }

    public GameSpeed Speed { get; private set; } = GameSpeed.Normal;

    public bool IsPaused => Speed == GameSpeed.Paused;

    /// <summary>Raised once per tick, after the clock has moved on.</summary>
    public event Action<GameClock>? Ticked;

    public void SetSpeed(GameSpeed speed)
    {
        if (!Enum.IsDefined(speed))
        {
            throw new ArgumentOutOfRangeException(nameof(speed), speed, "Unknown game speed.");
        }

        Speed = speed;
    }

    /// <summary>Moves the clock on by an amount of real time. Returns the number of ticks run.</summary>
    public int Advance(double realSeconds)
    {
        if (realSeconds < 0 || double.IsNaN(realSeconds))
        {
            throw new ArgumentOutOfRangeException(nameof(realSeconds), realSeconds, "Real time cannot go backwards.");
        }

        if (IsPaused)
        {
            return 0;
        }

        _owedGameSeconds += realSeconds * GameSecondsPerRealSecond * (int)Speed;

        var ticks = 0;
        while (_owedGameSeconds >= TickLength.TotalSeconds && ticks < MaxTicksPerAdvance)
        {
            _owedGameSeconds -= TickLength.TotalSeconds;
            Step();
            ticks++;
        }

        if (ticks == MaxTicksPerAdvance)
        {
            // Drop what is left of the backlog rather than falling further and further behind.
            _owedGameSeconds = 0;
        }

        return ticks;
    }

    /// <summary>Runs exactly one tick whatever the speed. Used by tests.</summary>
    public void Step()
    {
        Now = Now.Add(TickLength);
        TickCount++;
        Ticked?.Invoke(this);
    }

    public ClockState CaptureState() => new(Now, TickCount, Speed);

    public void RestoreState(ClockState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        SetSpeed(state.Speed);
        Now = state.Now;
        TickCount = state.TickCount;
        _owedGameSeconds = 0;
    }
}

/// <summary>A snapshot of the clock for saving.</summary>
public sealed record ClockState(DateTime Now, long TickCount, GameSpeed Speed);
