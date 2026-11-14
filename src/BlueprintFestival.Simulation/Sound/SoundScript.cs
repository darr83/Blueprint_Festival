using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;

namespace BlueprintFestival.Simulation.Sound;

/// <summary>The names of every sound effect. Each is a file of the same name in the game's Audio folder.</summary>
public static class Sounds
{
    public const string Day = "amb_day";
    public const string Night = "amb_night";
    public const string RainLight = "amb_rain_light";
    public const string RainHeavy = "amb_rain_heavy";
    public const string CrowdSmall = "crowd_small";
    public const string CrowdMedium = "crowd_medium";
    public const string CrowdLarge = "crowd_large";
    public const string CrowdQueue = "crowd_queue";
    public const string CheerSmall = "cheer_small";
    public const string CheerMedium = "cheer_medium";
    public const string CheerBig = "cheer_big";
    public const string ApplauseSmall = "applause_small";
    public const string ApplauseBig = "applause_big";
    public const string Groan = "crowd_groan";
    public const string Alarm = "crowd_alarm";
    public const string GateTicket = "gate_ticket";
    public const string GateTurnstile = "gate_turnstile";
    public const string Till = "till";
    public const string PourDrink = "pour_drink";
    public const string Fry = "fry";
    public const string TeaCup = "tea_cup";
    public const string ToiletDoor = "toilet_door";
    public const string WaterTap = "water_tap";
    public const string Click = "ui_click";
    public const string Tab = "ui_tab";
    public const string Refuse = "ui_refuse";
    public const string BuildStage = "build_stage";
    public const string BuildFence = "build_fence";
    public const string BuildGate = "build_gate";
    public const string BuildStall = "build_stall";
    public const string BuildGround = "build_ground";
    public const string TakeDown = "take_down";
    public const string HireStaff = "hire_staff";
    public const string Coins = "coins";
    public const string Ponchos = "ponchos";
    public const string Notebook = "notebook";
    public const string RubberStamp = "rubber_stamp";
    public const string GatesOpen = "gates_open";
    public const string RadioAlert = "radio_alert";
    public const string FirstAid = "first_aid";
    public const string Trouble = "trouble";
    public const string AllClear = "all_clear";
    public const string PaStop = "pa_stop";
    public const string PaStart = "pa_start";
    public const string StepBack1 = "pa_step_back_1";
    public const string StepBack2 = "pa_step_back_2";
    public const string StepBack3 = "pa_step_back_3";
    public const string PaRestart = "pa_restart";
    public const string RainStart = "rain_start";
    public const string MudSquelch = "mud_squelch";
    public const string LitterPick = "litter_pick";
    public const string DayEnd = "day_end";
    public const string ResultsGood = "results_good";
    public const string ResultsBad = "results_bad";
    public const string GateClosed = "gate_closed";
    public const string Clap1 = "clap_1";
    public const string Clap2 = "clap_2";
    public const string Clap3 = "clap_3";

    /// <summary>One clap of the crowd clapping along, each a little different; one plays on each clap of the beat.</summary>
    public static readonly IReadOnlyList<string> Claps = [Clap1, Clap2, Clap3];

    /// <summary>The PA announcements asking a packed crowd to step back, in a different voice each; they take turns.</summary>
    public static readonly IReadOnlyList<string> StepBack = new[] { StepBack1, StepBack2, StepBack3 };

    /// <summary>The sounds that play round and round for as long as they are wanted.</summary>
    public static readonly IReadOnlyList<string> Loops =
        [Day, Night, RainLight, RainHeavy, CrowdSmall, CrowdMedium, CrowdLarge, CrowdQueue];
}

/// <summary>
/// Listens to a festival and says what should be heard: how loud each background sound is
/// (the field, the rain, the murmur of the crowd) and which one-off sounds have just happened
/// (a cheer as an act walks on, a till, the radio call for a crush). It makes no noise itself;
/// the game plays whatever this asks for. The acts' music is not part of this.
/// </summary>
public sealed class SoundScript : IDisposable
{
    private readonly FestivalGame _game;
    private readonly Dictionary<string, double> _levels = Sounds.Loops.ToDictionary(id => id, _ => 0.0);
    private Phase _phase;
    private int _playing = -1;
    private int _playingSecond = -1;
    private bool _paused;
    private bool _calledOff;
    private bool _raining;
    private int _incidents;
    private int _sorted;
    private int _litter;
    private int _polls;

    public SoundScript(FestivalGame game)
    {
        _game = game;
        Remember();
        game.Crowd.TicketsSold += OnTickets;
        game.Crowd.Served += OnServed;
        game.CrushWarning += OnCrushWarning;
        game.CrushInjury += OnCrushInjury;
        game.Announced += OnAnnounced;
        game.ActCancelled += OnActCancelled;
        Poll();
    }

    /// <summary>A one-off sound has just happened.</summary>
    public event Action<string>? Cue;

    /// <summary>How loud each background sound should be now, from 0 (silent) to 1.</summary>
    public IReadOnlyDictionary<string, double> Levels => _levels;

    public void Dispose()
    {
        _game.Crowd.TicketsSold -= OnTickets;
        _game.Crowd.Served -= OnServed;
        _game.CrushWarning -= OnCrushWarning;
        _game.CrushInjury -= OnCrushInjury;
        _game.Announced -= OnAnnounced;
        _game.ActCancelled -= OnActCancelled;
    }

    private void OnAnnounced(int which) => Cue?.Invoke(Sounds.StepBack[which % Sounds.StepBack.Count]);

    private void OnActCancelled(Slot slot)
    {
        Cue?.Invoke(Sounds.PaStop);
        Cue?.Invoke(Sounds.Groan);
    }

    /// <summary>Which crowd murmur fits this many people, each as a loudness from 0 to 1.</summary>
    public static (double Small, double Medium, double Large) Murmur(int people)
    {
        var toMedium = Math.Clamp((people - 150) / 250.0, 0, 1);
        var toLarge = Math.Clamp((people - 800) / 1200.0, 0, 1);
        return (Math.Clamp(people / 40.0, 0, 1) * (1 - toMedium), toMedium * (1 - toLarge), toLarge);
    }

    /// <summary>The cheer that greets an act watched by this many people.</summary>
    public static string CheerFor(int watching) => watching < 60 ? Sounds.CheerSmall : watching < 500 ? Sounds.CheerMedium : Sounds.CheerBig;

    /// <summary>The sound of someone being served at a stall of this kind.</summary>
    public static string ServedAt(StallKind kind) => kind switch
    {
        StallKind.TeaAndCake => Sounds.TeaCup,
        StallKind.FoodVan => Sounds.Fry,
        StallKind.Bar => Sounds.PourDrink,
        StallKind.WaterPoint => Sounds.WaterTap,
        _ => Sounds.ToiletDoor,
    };

    /// <summary>
    /// Looks at the festival and works out what has changed since the last look. Call it a few
    /// times a second while the game is on screen.
    /// </summary>
    public void Poll()
    {
        _polls++;
        var game = _game;
        var day = game.Phase == Phase.Day;

        // The field itself: birdsong by day, owls and crickets by night, both hushed by rain.
        var rain = game.RainNow;
        var hush = 1 - (0.6 * Math.Clamp(rain * 2, 0, 1));
        _levels[Sounds.Day] = game.Light * hush;
        _levels[Sounds.Night] = (1 - game.Light) * hush;
        _levels[Sounds.RainLight] = rain <= 0.05 ? 0 : Math.Clamp(rain * 2.5, 0.3, 1) * (1 - Math.Clamp((rain - 0.45) / 0.3, 0, 1));
        _levels[Sounds.RainHeavy] = Math.Clamp((rain - 0.45) / 0.3, 0, 1);

        var (small, medium, large) = day ? Murmur(game.Crowd.InField) : (0, 0, 0);
        _levels[Sounds.CrowdSmall] = small;
        _levels[Sounds.CrowdMedium] = medium;
        _levels[Sounds.CrowdLarge] = large;
        _levels[Sounds.CrowdQueue] = day ? Math.Clamp(game.Crowd.Queuing / 40.0, 0, 1) : 0;

        if (game.Phase != _phase)
        {
            PhaseChanged(_phase, game.Phase);
        }

        if (day)
        {
            Acts();
            Trouble();
            if (rain > 0.05 && !_raining)
            {
                Cue?.Invoke(Sounds.RainStart);
            }

            if (game.Wetness > 0.5 && _polls % 40 == 0 && game.Crowd.InField > 0)
            {
                Cue?.Invoke(Sounds.MudSquelch);
            }
        }

        if (game.Phase == Phase.Tidy && game.Litter.Count < _litter)
        {
            Cue?.Invoke(Sounds.LitterPick);
        }

        Remember();
    }

    private void Remember()
    {
        var game = _game;
        _phase = game.Phase;
        _playing = game.Show.Playing(game.Now)?.Index ?? -1;
        _playingSecond = game.SecondShow.Playing(game.Now)?.Index ?? -1;
        _paused = game.ActPaused;
        _calledOff = game.Show.CalledOff is not null;
        _raining = game.RainNow > 0.05;
        _incidents = Math.Max(_incidents, game.Incidents.Count == 0 ? 0 : game.Incidents.Max(i => i.Id) + 1);
        _sorted = game.Treated + game.TroubleSorted;
        _litter = game.Litter.Count;
    }

    private void PhaseChanged(Phase from, Phase to)
    {
        switch (to)
        {
            case Phase.Build when from == Phase.Plan:
                Cue?.Invoke(Sounds.RubberStamp);
                break;
            case Phase.Day when from == Phase.Build:
                Cue?.Invoke(Sounds.GatesOpen);
                break;
            case Phase.Tidy:
                Cue?.Invoke(Sounds.DayEnd);
                break;
            case Phase.Results:
                Cue?.Invoke(_game.Result is { } result && result.LicenceNext >= result.LicenceNow && !result.Loan && result.Happy >= 60
                    ? Sounds.ResultsGood : Sounds.ResultsBad);
                break;
            case Phase.Closed:
                Cue?.Invoke(Sounds.GateClosed);
                break;
        }
    }

    private void Acts()
    {
        var game = _game;
        if (game.Show.CalledOff is not null && !_calledOff)
        {
            // The music has been stopped for the day.
            Cue?.Invoke(Sounds.PaStop);
            Cue?.Invoke(Sounds.Groan);
            return;
        }

        if (game.ActPaused != _paused)
        {
            Cue?.Invoke(game.ActPaused ? Sounds.PaStop : Sounds.PaStart);
            if (game.ActPaused)
            {
                Cue?.Invoke(Sounds.Groan);
            }
            else if (game.MusicOn)
            {
                // The music comes back with a thank you over the PA.
                Cue?.Invoke(Sounds.PaRestart);
            }
        }

        // People are still walking over as an act comes on, so count some of the field as well as those already watching.
        var crowd = Math.Max(game.Crowd.Watching, game.Crowd.InField / 4);
        Stage(game.Show.Playing(game.Now), _playing, game.Show, crowd);
        Stage(game.SecondShow.Playing(game.Now), _playingSecond, game.SecondShow, crowd / 3);
    }

    private void Stage(Show.Slot? now, int before, Show.RunningOrder show, int watching)
    {
        var index = now?.Index ?? -1;
        if (index == before || watching == 0 || show.CalledOff is not null)
        {
            return;
        }

        if (before >= 0)
        {
            // The act that was on has finished: a hand for them as they go.
            var headliner = show.Slots.Any(s => s.Index == before && s.Headliner);
            Cue?.Invoke(headliner || watching >= 500 ? Sounds.ApplauseBig : Sounds.ApplauseSmall);
            if (headliner)
            {
                Cue?.Invoke(CheerFor(watching));
            }
        }

        if (now is not null)
        {
            Cue?.Invoke(CheerFor(watching));
        }
    }

    private void Trouble()
    {
        var game = _game;
        foreach (var incident in game.Incidents)
        {
            if (incident.Id >= _incidents && !incident.Crush)
            {
                Cue?.Invoke(incident.Kind == IncidentKind.Ill ? Sounds.FirstAid : Sounds.Trouble);
            }
        }

        if (game.Treated + game.TroubleSorted > _sorted)
        {
            Cue?.Invoke(Sounds.AllClear);
        }
    }

    private void OnTickets(int people) =>
        Cue?.Invoke(_game.Site.Gates.Any(g => g.Kind == GateKind.TurnstileRow) ? Sounds.GateTurnstile : Sounds.GateTicket);

    private void OnServed(StallKind kind, int people, int paid)
    {
        Cue?.Invoke(ServedAt(kind));
        if (paid > 0)
        {
            Cue?.Invoke(Sounds.Till);
        }
    }

    private void OnCrushWarning(double x, double y) => Cue?.Invoke(Sounds.RadioAlert);

    private void OnCrushInjury(double x, double y) => Cue?.Invoke(Sounds.Alarm);
}
