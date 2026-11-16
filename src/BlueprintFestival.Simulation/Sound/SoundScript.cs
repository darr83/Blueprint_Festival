using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

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
    public const string SongEndSmall = "song_end_small";
    public const string SongEndBig = "song_end_big";
    public const string SetEndOvation = "set_end_ovation";
    public const string ApplauseLoop = "applause_loop";
    public const string FireworkLaunch = "firework_launch";
    public const string FireworkWhistle = "firework_whistle";
    public const string FireworkBurst = "firework_burst";
    public const string FireworkBurst2 = "firework_burst_2";
    public const string FireworkWillow = "firework_burst_3";
    public const string FireworkCrackle = "firework_crackle";
    public const string FireworkFountain = "firework_fountain";
    public const string FireworkCandle = "firework_candle";
    public const string FireworkMine = "firework_mine";
    public const string FireworkOoh = "firework_ooh";
    public const string FireworkCheer = "firework_cheer";

    /// <summary>One clap of the crowd clapping along, each a little different; one plays on each clap of the beat.</summary>
    public static readonly IReadOnlyList<string> Claps = [Clap1, Clap2, Clap3];

    /// <summary>The PA announcements asking a packed crowd to step back, in a different voice each; they take turns.</summary>
    public static readonly IReadOnlyList<string> StepBack = new[] { StepBack1, StepBack2, StepBack3 };

    public const string BuildLight = "build_light";
    public const string BuildFairy = "build_fairy";
    public const string BuildBunting = "build_bunting";
    public const string BuildScreen = "build_screen";
    public const string BuildLaunchPad = "build_launch_pad";
    public const string BuildStraw = "build_straw";
    public const string BuildBench = "build_bench";
    public const string BuildDeckchair = "build_deckchair";
    public const string BuildParasol = "build_parasol";
    public const string BuildHandWash = "build_hand_wash";
    public const string BuildHandGel = "build_hand_gel";
    public const string BuildShadeSail = "build_shade_sail";
    public const string BuildSign = "build_sign";
    public const string BuildPlanter = "build_planter";
    public const string BuildFlag = "build_flag";
    public const string BuildFirePit = "build_fire_pit";
    public const string BuildLetters = "build_letters";
    public const string BuildTape = "build_tape";
    public const string BuildVip = "build_vip";
    public const string CoffeeMachine = "coffee_machine";
    public const string SlushMachine = "slush_machine";
    public const string CocktailShaker = "cocktail_shaker";
    public const string IceCreamScoop = "ice_cream_scoop";
    public const string PizzaBox = "pizza_box";
    public const string WokSizzle = "wok_sizzle";
    public const string MerchBag = "merch_bag";
    public const string ChargePlug = "charge_plug";
    public const string ChampagnePop = "champagne_pop";
    public const string BinDrop = "bin_drop";
    public const string FireCrackle = "amb_fire_pit";
    public const string Fairground = "amb_fairground";
    public const string BuildRide = "build_ride";
    public const string RideBell = "ride_bell";
    public const string HookADuck = "hook_a_duck";
    public const string TeddyWin = "teddy_win";

    /// <summary>The sounds that play round and round for as long as they are wanted.</summary>
    public static readonly IReadOnlyList<string> Loops =
        [Day, Night, RainLight, RainHeavy, CrowdSmall, CrowdMedium, CrowdLarge, CrowdQueue, ApplauseLoop, FireCrackle, Fairground];

    /// <summary>
    /// The clapping that goes on under the cheers between songs and at the end of a set. The game
    /// sets how loud it is from how many people are clapping, so it is not one of the levels the
    /// sound script works out.
    /// </summary>
    public static bool SetByTheGame(string loop) => loop == ApplauseLoop;

    /// <summary>The cheer when a song ends, for this many people watching.</summary>
    public static string SongEndFor(int watching) => watching < 400 ? SongEndSmall : SongEndBig;
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
    private readonly Dictionary<string, double> _levels = Sounds.Loops.Where(id => !Sounds.SetByTheGame(id)).ToDictionary(id => id, _ => 0.0);
    private Phase _phase;
    private bool _shut;
    private readonly int[] _playingOn = { -1, -1, -1, -1 };
    private bool _paused;
    private bool _calledOff;
    private bool _raining;
    private int _incidents;
    private int _sorted;
    private int _litter;
    private int _polls;
    private readonly HashSet<(int Stage, DateTime Start)> _announced = [];
    private readonly Dictionary<string, int> _lastTake = [];

    public SoundScript(FestivalGame game)
    {
        _game = game;
        Remember();
        game.Crowd.TicketsSold += OnTickets;
        game.Crowd.Served += OnServed;
        game.Crowd.TeddyWon += OnTeddy;
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
        _game.Crowd.TeddyWon -= OnTeddy;
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

    /// <summary>How many rides are going round with somebody on them just now.</summary>
    public static int RidesGoing(FestivalGame game)
    {
        var going = 0;
        for (var i = 0; i < game.Site.Stalls.Count; i++)
        {
            var stall = game.Site.Stalls[i];
            going += Rides.IsRide(stall.Kind) && !Rides.TurnAt(stall, game.Now).Boarding && game.Crowd.Riding(i) > 0 ? 1 : 0;
        }

        return going;
    }

    /// <summary>How loud the fairground is with this many rides going: one is plain to hear, more are a little louder.</summary>
    public static double FairgroundLevel(int rides) => rides <= 0 ? 0 : Math.Clamp(0.55 + (rides * 0.15), 0, 1);

    /// <summary>The quietest a ride's bell, a duck or a teddy win can be and still be played: anything fainter is not heard at all.</summary>
    public const double FairCueHeard = 0.12;

    private (double X, double Y, double ViewHeight)? _ear;
    private bool _filming;

    /// <summary>
    /// Where the player is listening from: the point of the field in the middle of the view (in
    /// metres) and the height of the view. The fairground is only heard near where the player is
    /// looking. Filming is true while a stage camera is on, when the fair is not heard at all.
    /// Until this is called the whole field is heard as if close to.
    /// </summary>
    public void Listen(double x, double y, double viewHeight, bool filming)
    {
        _ear = (x, y, viewHeight);
        _filming = filming;
    }

    /// <summary>The middle of a stall on the field, in metres.</summary>
    public static (double X, double Y) MiddleOf(PlacedStall stall)
    {
        var r = stall.Rect;
        return ((r.X + (r.Width / 2.0)) * FieldMap.TileMetres, (r.Y + (r.Height / 2.0)) * FieldMap.TileMetres);
    }

    /// <summary>How loud one fairground stall is from where the player is listening, from 0 to 1.</summary>
    public double HeardAt(PlacedStall stall)
    {
        if (_filming)
        {
            return 0;
        }

        if (_ear is not { } ear)
        {
            return 1;
        }

        var (fx, fy) = MiddleOf(stall);
        return StageMix.Fair(ear.X, ear.Y, ear.ViewHeight, fx, fy);
    }

    /// <summary>
    /// How loud the fairground's organ and squeals are, from where the player is listening: the
    /// nearest ride going round with people on it, and a little more for each other one heard.
    /// </summary>
    public double FairgroundHeard()
    {
        var game = _game;
        var loudest = 0.0;
        var all = 0.0;
        var going = 0;
        for (var i = 0; i < game.Site.Stalls.Count; i++)
        {
            var stall = game.Site.Stalls[i];
            if (!Rides.IsRide(stall.Kind) || Rides.TurnAt(stall, game.Now).Boarding || game.Crowd.Riding(i) <= 0)
            {
                continue;
            }

            going++;
            var heard = HeardAt(stall);
            loudest = Math.Max(loudest, heard);
            all += heard;
        }

        if (going == 0)
        {
            return 0;
        }

        // Close to (or before anyone is listening) this is the old rule: more rides, a little louder.
        var close = FairgroundLevel(going);
        return Math.Clamp(close * (loudest + (0.25 * (all - loudest))), 0, 1);
    }

    /// <summary>How loud the nearest stall of this kind is from where the player is listening.</summary>
    private double NearestHeard(StallKind kind)
    {
        var best = 0.0;
        foreach (var stall in _game.Site.Stalls)
        {
            if (stall.Kind == kind)
            {
                best = Math.Max(best, HeardAt(stall));
            }
        }

        return best;
    }

    private void OnTeddy(PlacedStall stall)
    {
        if (HeardAt(stall) >= FairCueHeard)
        {
            Cue?.Invoke(Sounds.TeddyWin);
        }
    }

    /// <summary>The cheer that greets an act watched by this many people.</summary>
    public static string CheerFor(int watching) => watching < 60 ? Sounds.CheerSmall : watching < 500 ? Sounds.CheerMedium : Sounds.CheerBig;

    /// <summary>The sound of someone being served at a stall of this kind.</summary>
    public static string? ServedAt(StallKind kind) => kind switch
    {
        StallKind.Coffee => Sounds.CoffeeMachine,
        StallKind.HotDogs or StallKind.Burgers or StallKind.Fries => Sounds.Fry,
        StallKind.Pizza => Sounds.PizzaBox,
        StallKind.Noodles => Sounds.WokSizzle,
        StallKind.Bar => Sounds.PourDrink,
        StallKind.Slush => Sounds.SlushMachine,
        StallKind.Cocktails => Sounds.CocktailShaker,
        StallKind.IceCream => Sounds.IceCreamScoop,
        StallKind.Ponchos => Sounds.Ponchos,
        StallKind.BucketHats or StallKind.Merchandise => Sounds.MerchBag,
        StallKind.PhoneCharging => Sounds.ChargePlug,
        StallKind.VipLounge or StallKind.VipPlatform => Sounds.ChampagnePop,
        StallKind.WaterPoint or StallKind.ShowerBlock or StallKind.UrinalStand or StallKind.UrinalTrough => Sounds.WaterTap,
        StallKind.CampInfo or StallKind.CampShop => Sounds.MerchBag,
        StallKind.Bin => Sounds.BinDrop,
        StallKind.Toilets or StallKind.ToiletBlock or StallKind.BackToBackToilets or StallKind.AccessibleToilet => Sounds.ToiletDoor,
        StallKind.HookADuck => Sounds.HookADuck,
        StallKind.BigWheel or StallKind.Waltzer or StallKind.Twister or StallKind.Carousel or StallKind.Skylark => Sounds.RideBell,
        // The first aid and medical tents are heard through the radio instead.
        _ => null,
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

        // Fire pits crackle once it is getting dark, more of them a little louder; rain dampens them.
        var pits = game.Site.Decorations.Count(d => d.Kind == DecorKind.FirePit);
        _levels[Sounds.FireCrackle] = pits == 0 ? 0 : Math.Clamp(0.5 + (pits * 0.15), 0, 1) * Math.Clamp((0.7 - game.Light) / 0.4, 0, 1) * hush;

        // The fairground: its organ and the squeals from the rides, for as long as a ride is going round with people on it.
        // Only heard near where the player is looking, and not at all on a stage camera.
        _levels[Sounds.Fairground] = day ? FairgroundHeard() : 0;

        var (small, medium, large) = day ? Murmur(game.Crowd.InField) : (0, 0, 0);
        _levels[Sounds.CrowdSmall] = small;
        _levels[Sounds.CrowdMedium] = medium;
        _levels[Sounds.CrowdLarge] = large;
        _levels[Sounds.CrowdQueue] = day ? Math.Clamp(game.Crowd.Queuing / 40.0, 0, 1) : 0;

        if (game.Phase != _phase)
        {
            PhaseChanged(_phase, game.Phase);
        }
        else if (day && _shut && !game.GatesShut)
        {
            // The day began with the gates shut, and they have just been opened.
            Cue?.Invoke(Sounds.GatesOpen);
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
        _shut = game.GatesShut;
        for (var i = 0; i < game.Shows.Count; i++)
        {
            _playingOn[i] = game.Shows[i].Playing(game.Now)?.Index ?? -1;
        }

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
            case Phase.Day when from == Phase.Build && !_game.GatesShut:
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

        for (var i = 0; i < game.Shows.Count; i++)
        {
            Announce(game.Shows[i], i, Announcements.StageMinutes[i]);
        }

        // People are still walking over as an act comes on, so count some of the field as well as those already watching.
        var crowd = Math.Max(game.Crowd.Watching, game.Crowd.InField / 4);
        for (var i = 0; i < game.Shows.Count; i++)
        {
            Stage(game.Shows[i].Playing(game.Now), _playingOn[i], game.Shows[i], i == 0 ? crowd : crowd / 3);
        }
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
            // The act that was on has finished: the loudest cheer of their set, clapping and
            // cheering together, as the crowd clap them off.
            var headliner = show.Slots.Any(s => s.Index == before && s.Headliner);
            var big = headliner || watching >= 500;
            Cue?.Invoke(big ? Sounds.ApplauseBig : Sounds.ApplauseSmall);
            Cue?.Invoke(big ? Sounds.SetEndOvation : CheerFor(watching));
        }

        if (now is not null)
        {
            Cue?.Invoke(CheerFor(watching));
        }
    }

    /// <summary>
    /// A few minutes before an act goes on, the PA tells the field who is coming, using the
    /// artist's two announcements in turn. Each set is announced once.
    /// </summary>
    private void Announce(Show.RunningOrder show, int stage, int minutes)
    {
        var game = _game;
        if (show.Stage is null || show.CalledOff is not null)
        {
            return;
        }

        var next = show.Next(game.Now);
        if (next is null || next.Start - game.Now > TimeSpan.FromMinutes(minutes) || !_announced.Add((stage, next.Start))
            || Show.Roster.Named(next.Act.Name) is null)
        {
            return;
        }

        var take = (_lastTake.GetValueOrDefault(next.Act.Name) % Announcements.Takes) + 1;
        _lastTake[next.Act.Name] = take;
        Cue?.Invoke(Announcements.IdFor(next.Act.Name, take));
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
        // The fairground's bells, ducks and tills are only heard near where the player is looking.
        if (Rides.IsFair(kind) && NearestHeard(kind) < FairCueHeard)
        {
            return;
        }

        if (ServedAt(kind) is { } sound)
        {
            Cue?.Invoke(sound);
        }

        if (paid > 0)
        {
            Cue?.Invoke(Sounds.Till);
        }
    }

    private void OnCrushWarning(double x, double y) => Cue?.Invoke(Sounds.RadioAlert);

    private void OnCrushInjury(double x, double y) => Cue?.Invoke(Sounds.Alarm);
}
