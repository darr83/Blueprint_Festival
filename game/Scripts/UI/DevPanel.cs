using System.Collections.Generic;
using System.Linq;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.Sound;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.UI;

/// <summary>
/// The dev panel: a drawer down the right of the screen, opened with F10 at any time in the
/// festival, for trying things out while the game runs. Four tabs: Show (crowd singing, the acts,
/// money and the clock), Crowd (how they feel, more of them or none, what they need), World
/// (weather, fireworks, what is drawn) and Trouble (a crush, a fight, sickness, injuries, litter,
/// started where the camera is looking). Nothing on it is saved.
/// </summary>
public partial class DevPanel : Paper
{
    public enum Tab
    {
        Show,
        Crowd,
        World,
        Trouble,
    }

    public const int PanelWidth = 470;

    private static readonly (GameSpeed Speed, string Text)[] Speeds =
    {
        (GameSpeed.Paused, "Pause"), (GameSpeed.Normal, "1×"), (GameSpeed.Fast, "2×"), (GameSpeed.VeryFast, "4×"),
        (GameSpeed.Fastest, "8×"), (GameSpeed.Testing16, "16×"), (GameSpeed.Testing32, "32×"),
    };

    private FestivalScene _scene = null!;
    private FestivalGame _game = null!;
    private VBoxContainer _body = null!;
    private Label _status = null!;
    private Label _said = null!;
    private readonly Dictionary<Tab, Button> _tabs = new();
    private readonly List<(Button Button, System.Func<bool> On)> _toggles = new();
    private readonly List<(Label Label, System.Func<string> Text)> _live = new();
    private double _sinceRefresh;
    private int _stage;
    private int _artist;

    public DevPanel() : base(18, small: true)
    {
    }

    /// <summary>The tab showing.</summary>
    public Tab Showing { get; private set; } = Tab.Show;

    /// <summary>What the last button pressed did, or why it could not.</summary>
    public string LastSaid => _said.Text;

    /// <summary>The stage the acts part works on: 0 for the main stage.</summary>
    public int Stage => _stage;

    public void Initialise(FestivalScene scene)
    {
        _scene = scene;
        _game = scene.Game;
        Name = "DevPanel";
        CustomMinimumSize = new Vector2(PanelWidth, 0);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        AddChild(box);

        box.AddChild(UiStyle.Caps("F10 · while playing · test only", size: 11));
        var top = new HBoxContainer();
        var heading = UiStyle.Heading("Dev panel", 26);
        heading.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        top.AddChild(heading);
        var close = UiStyle.RoundButton("×", () => Visible = false, 34);
        close.TooltipText = "Close (F10)";
        top.AddChild(close);
        box.AddChild(top);

        _status = UiStyle.Body("", muted: true, size: 13);
        box.AddChild(_status);

        var tabs = new HFlowContainer();
        tabs.AddThemeConstantOverride("h_separation", 6);
        foreach (var tab in System.Enum.GetValues<Tab>())
        {
            var chip = UiStyle.ChipButton(tab.ToString(), () => Show(tab), Sounds.Tab);
            chip.ToggleMode = true;
            _tabs[tab] = chip;
            tabs.AddChild(chip);
        }

        box.AddChild(tabs);

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _body.AddThemeConstantOverride("separation", 6);
        var room = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        room.AddThemeConstantOverride("margin_right", 14);
        room.AddChild(_body);
        scroll.AddChild(room);
        box.AddChild(scroll);

        _said = UiStyle.Body("", size: 13, bold: true);
        _said.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _said.AddThemeColorOverride("font_color", UiStyle.StagePink);
        box.AddChild(_said);

        Visible = false;
        Place();
        GetViewport().SizeChanged += Place;
        Show(Tab.Show);
    }

    public override void _ExitTree()
    {
        GetViewport().SizeChanged -= Place;
    }

    /// <summary>Down the right of the screen, from under the top bar nearly to the bottom.</summary>
    private void Place()
    {
        if (!IsInsideTree())
        {
            return;
        }

        var screen = GetViewportRect().Size;
        Position = new Vector2(screen.X - 22 - PanelWidth, UiStyle.TopClear);
        Size = new Vector2(PanelWidth, Mathf.Max(240, screen.Y - UiStyle.TopClear - 22));
    }

    public void Toggle()
    {
        Visible = !Visible;
        if (Visible)
        {
            Place();
            Show(Showing);
        }
    }

    /// <summary>Opens a tab.</summary>
    public void Show(Tab tab)
    {
        Showing = tab;
        foreach (var (each, chip) in _tabs)
        {
            chip.SetPressedNoSignal(each == tab);
        }

        Build();
    }

    public override void _Process(double delta)
    {
        _sinceRefresh += delta;
        if (!Visible || _sinceRefresh < 0.25)
        {
            return;
        }

        _sinceRefresh = 0;
        Refresh();
    }

    /// <summary>Brings the live parts up to date: the line at the top, which choices are on, and the act on now.</summary>
    private void Refresh()
    {
        var phase = _game.Phase == Phase.Day ? $"{_game.Now:HH:mm}" : _game.Phase.ToString();
        _status.Text = $"{phase}  ·  £{_game.Money:N0}  ·  {_game.Crowd.Happiness}% happy  ·  {_game.Crowd.OnSite:N0} here";
        foreach (var (button, on) in _toggles)
        {
            button.SetPressedNoSignal(on());
        }

        foreach (var (label, text) in _live)
        {
            label.Text = text();
        }
    }

    private void Say(string text)
    {
        _said.Text = text;
        Refresh();
    }

    /// <summary>Lays the tab out afresh.</summary>
    private void Build()
    {
        foreach (var child in _body.GetChildren())
        {
            _body.RemoveChild(child);
            child.QueueFree();
        }

        _toggles.Clear();
        _live.Clear();
        switch (Showing)
        {
            case Tab.Show:
                ShowTab();
                break;
            case Tab.Crowd:
                CrowdTab();
                break;
            case Tab.World:
                WorldTab();
                break;
            default:
                TroubleTab();
                break;
        }

        Refresh();
    }

    private void Section(string title)
    {
        _body.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
        _body.AddChild(UiStyle.Caps(title, pink: true, size: 11));
    }

    private void Note(string text)
    {
        var label = UiStyle.Body(text, muted: true, size: 12);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _body.AddChild(label);
    }

    private HFlowContainer Row(string? title = null)
    {
        var row = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("h_separation", 6);
        row.AddThemeConstantOverride("v_separation", 6);
        if (title is not null)
        {
            var label = UiStyle.Body(title, size: 13, bold: true);
            label.CustomMinimumSize = new Vector2(92, 30);
            label.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(label);
        }

        _body.AddChild(row);
        return row;
    }

    private static Button Action(HFlowContainer row, string text, System.Action pressed)
    {
        var button = UiStyle.ChipButton(text, pressed);
        row.AddChild(button);
        return button;
    }

    /// <summary>A chip that shows as filled while something is so.</summary>
    private void Choice(HFlowContainer row, string text, System.Func<bool> on, System.Action pressed)
    {
        var button = UiStyle.ChipButton(text, () =>
        {
            pressed();
            Refresh();
        });
        button.ToggleMode = true;
        row.AddChild(button);
        _toggles.Add((button, on));
    }

    private void Live(System.Func<string> text, bool muted = false)
    {
        var label = UiStyle.Body("", muted: muted, size: 13);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _body.AddChild(label);
        _live.Add((label, text));
    }

    private bool OnTheDay(string what)
    {
        if (_game.Phase == Phase.Day)
        {
            return true;
        }

        Say($"{what} only works on the festival day.");
        return false;
    }

    // ---------------------------------------------------------------- Show

    private void ShowTab()
    {
        Section("Crowd singing");
        var singing = Row();
        Choice(singing, "Off", () => SingAlong.Setting == SingingSetting.Off, () => SetSinging(SingingSetting.Off));
        Choice(singing, "Normal", () => SingAlong.Setting == SingingSetting.Normal, () => SetSinging(SingingSetting.Normal));
        Choice(singing, "Just the crowd", () => SingAlong.Setting == SingingSetting.JustTheCrowd, () => SetSinging(SingingSetting.JustTheCrowd));
        Note("Just the crowd: in every chorus the band and the music drop out and only the crowd sings. Needs a song with its crowd file.");

        Section("Acts");
        var stages = Row("Stage");
        for (var i = 0; i < _game.Shows.Count; i++)
        {
            if (_game.Shows[i].Stage is null)
            {
                continue;
            }

            var stage = i;
            Choice(stages, i == 0 ? "Main" : $"Stage {i + 1}", () => _stage == stage, () => _stage = stage);
        }

        Live(() =>
        {
            if (_stage >= _game.Shows.Count || _game.Shows[_stage].Stage is null)
            {
                return "No stage built.";
            }

            var show = _game.Shows[_stage];
            var music = _scene.Music.For(show);
            if (show.Playing(_game.Now) is { } on)
            {
                var song = music is { SongTitle.Length: > 0 } ? $", playing \"{music.SongTitle}\"" : music?.SongCount == 0 ? " (no songs in their folder)" : "";
                return $"On now: {on.Act.Name} until {on.End:HH:mm}{song}";
            }

            return show.Next(_game.Now) is { } next ? $"Next: {next.Act.Name} at {next.Start:HH:mm}" : "Nothing more on this stage today.";
        });
        var acts = Row();
        Action(acts, "Next song", NextSong);
        Action(acts, "Skip to next act", () =>
        {
            if (OnTheDay("Skipping"))
            {
                Say(_game.SkipToNextAct() ? "Hurrying on to the next act." : "No act still to come.");
            }
        });
        Action(acts, "End set", () => Say(_game.DevEndSet(_stage) ? "Set ended. The stage stands empty until the next one." : "Nothing is playing on that stage."));

        var swap = Row("Put on");
        var pick = new OptionButton { FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(210, 0) };
        var artists = Artists();
        foreach (var artist in artists)
        {
            pick.AddItem($"{artist.Name}  {new string('★', artist.Rating)}  {Roster.NameOf(artist.Genre)}");
        }

        pick.Selected = Mathf.Clamp(_artist, 0, artists.Count - 1);
        pick.ItemSelected += index => _artist = (int)index;
        swap.AddChild(pick);
        Action(swap, "Now", () =>
        {
            var artist = artists[Mathf.Clamp(_artist, 0, artists.Count - 1)];
            Say(_game.DevSwapAct(_stage, artist)
                ? $"{artist.Name} {(_game.Shows[_stage].Playing(_game.Now) is null ? "are on next" : "are on now")}."
                : "Nothing booked on that stage to swap.");
        });
        Note("Swaps the act playing now (or the next act, between sets). They keep the set's times.");

        Section("Money");
        var money = Row();
        foreach (var amount in new[] { 1_000, 10_000, 100_000 })
        {
            Action(money, $"+ £{amount:N0}", () =>
            {
                _game.DevAddMoney(amount);
                Say($"Added £{amount:N0}.");
            });
        }

        Action(money, "Set to £0", () =>
        {
            _game.DevEmptyBank();
            Say("The bank is empty.");
        });

        Section("Time");
        var speed = Row("Speed");
        foreach (var (each, text) in Speeds)
        {
            Choice(speed, text, () => _game.Clock.Speed == each, () => _game.Clock.SetSpeed(each));
        }

        var jump = Row("Jump to");
        foreach (var hour in new[] { 12, 15, 18, 21 })
        {
            Action(jump, $"{hour}:00", () => Jump(System.TimeSpan.FromHours(hour), $"{hour}:00"));
        }

        Action(jump, "Fireworks", () =>
        {
            if (_game.NextFireworksAt is { } at)
            {
                // A minute before, so the start is seen.
                Jump(at - System.TimeSpan.FromMinutes(1), $"the fireworks at {at:hh\\:mm}");
            }
            else
            {
                Say("No firework show still to come today.");
            }
        });
        Action(jump, "End of day", () => Jump(Crowd.EveryoneOut, "the end of the day"));
        Note("Every second in between still happens, as fast as the computer can. A crush stops the jump.");
    }

    private static List<Artist> Artists() => Roster.Artists.OrderBy(a => a.Name, System.StringComparer.OrdinalIgnoreCase).ToList();

    private void SetSinging(SingingSetting setting)
    {
        SingAlong.Setting = setting;
        Say(setting switch
        {
            SingingSetting.Off => "Nobody sings along.",
            SingingSetting.JustTheCrowd => "Every chorus is left to the crowd.",
            _ => "Singing as normal.",
        });
    }

    private void NextSong()
    {
        if (_stage >= _game.Shows.Count || _scene.Music.For(_game.Shows[_stage]) is not { } music || !music.SkipSong())
        {
            Say("No act with songs playing on that stage.");
            return;
        }

        Say($"Next song: {music.SongTitle}.");
    }

    private void Jump(System.TimeSpan to, string what)
    {
        if (!OnTheDay("Jumping"))
        {
            return;
        }

        Say(_game.DevJumpTo(to) ? $"Hurrying on to {what}." : $"It is already past {what}.");
    }

    // ---------------------------------------------------------------- Crowd

    private void CrowdTab()
    {
        Section("Happiness");
        Live(() => $"Now {_game.Crowd.Happiness}%. Sets everyone here; it then changes with their day as normal.", muted: true);
        var mood = Row();
        foreach (var level in new[] { 10, 35, 50, 65, 80, 100 })
        {
            Action(mood, $"{level}%", () =>
            {
                if (OnTheDay("Setting happiness"))
                {
                    Say($"Everyone here is {level}% happy ({_game.Crowd.SetMood(level):N0} people).");
                }
            });
        }

        Section("People");
        Live(() => $"{_game.Crowd.OnSite:N0} here, {_game.Crowd.Expected:N0} expected today, licence for {_game.Licence:N0}.", muted: true);
        var people = Row();
        foreach (var more in new[] { 100, 1_000 })
        {
            Action(people, $"+ {more:N0}", () =>
            {
                if (OnTheDay("Adding people"))
                {
                    Say($"{_game.DevAddPeople(more):N0} more on their way, arriving over the next few minutes.");
                }
            });
        }

        Action(people, "Fill to licence", () =>
        {
            if (OnTheDay("Filling up"))
            {
                var added = _game.DevFillToLicence();
                Say(added > 0 ? $"{added:N0} more on their way." : "Already at the licence.");
            }
        });
        Action(people, "Send everyone home", () =>
        {
            if (OnTheDay("Sending people home"))
            {
                Say($"{_game.DevSendAllHome():N0} people heading home, and nobody else is coming.");
            }
        });

        Section("Needs");
        var needs = Row();
        Action(needs, "Everyone thirsty", () => Need(Simulation.Site.Need.Drink, "thirsty"));
        Action(needs, "Everyone hungry", () => Need(Simulation.Site.Need.Food, "hungry"));
        Action(needs, "Everyone needs the toilet", () => Need(Simulation.Site.Need.Toilet, "in need of the toilet"));
        Action(needs, "Everyone drunk", () =>
        {
            if (OnTheDay("Drinking"))
            {
                Say($"{_game.Crowd.MakeEveryoneDrunk():N0} groups of grown-ups are drunk.");
            }
        });
        Action(needs, "Reset needs", () =>
        {
            if (OnTheDay("Resetting needs"))
            {
                Say($"Everyone is fed, watered and sober ({_game.Crowd.ResetNeeds():N0} people).");
            }
        });
    }

    private void Need(Need need, string word)
    {
        if (OnTheDay("Changing needs"))
        {
            Say($"{_game.Crowd.MakeEveryoneNeed(need):N0} people are {word}.");
        }
    }

    // ---------------------------------------------------------------- World

    private void WorldTab()
    {
        Section("Weather");
        Live(() => $"{_game.Weather.Word}, {_game.Weather.High}°C. Rain now {_game.RainNow:P0}, ground {_game.Wetness:P0} wet.", muted: true);
        var weather = Row();
        foreach (var kind in System.Enum.GetValues<WeatherKind>())
        {
            Choice(weather, DayWeather.Of(kind).Word, () => _game.Weather.Kind == kind, () =>
            {
                _game.DevSetWeather(kind);
                Say(kind is WeatherKind.Showers or WeatherKind.Rain ? $"{DayWeather.Of(kind).Word}, starting now." : $"{DayWeather.Of(kind).Word} for the rest of the day.");
            });
        }

        var ground = Row("Ground");
        Action(ground, "Dry the ground", () =>
        {
            _game.DevSetWetness(0);
            Say("The ground is dry.");
        });
        Action(ground, "Soak the ground", () =>
        {
            _game.DevSetWetness(1);
            Say("The ground is sodden.");
        });

        Section("Fireworks");
        var fireworks = Row();
        Action(fireworks, "Launch a show now", () =>
        {
            if (OnTheDay("Fireworks"))
            {
                Say(_game.DevLaunchFireworks() ? "Fireworks going off now (the example show)." : "Put down a launch pad first, from the Fireworks build button.");
            }
        });
        Choice(fireworks, "Hold planned shows", () => _game.FireworksHeld, () =>
        {
            _game.FireworksHeld = !_game.FireworksHeld;
            Say(_game.FireworksHeld ? "Planned shows will not go off." : "Planned shows go off as normal.");
        });

        Section("View");
        var view = Row();
        Choice(view, "Hide the other panels", () => _scene.PanelsHidden, () => _scene.PanelsHidden = !_scene.PanelsHidden);
        Choice(view, "Frame rate", () => _scene.FrameRateShowing, () => _scene.FrameRateShowing = !_scene.FrameRateShowing);
        Choice(view, "Crowd density map", () => _scene.Density.Showing, () => _scene.SetCrowdView(!_scene.Density.Showing));
        Note("With the panels hidden, Esc brings them back.");
    }

    // ---------------------------------------------------------------- Trouble

    private void TroubleTab()
    {
        Note("Trouble starts where the camera is looking: move the view to where you want it first.");
        Section("Start");
        var start = Row();
        Action(start, "Crush", () => Trouble("A crush", (x, y) =>
        {
            var packed = _game.DevCrushAt(x, y);
            return packed > 0 ? $"{packed:N0} people packed in. The warning comes after two minutes." : null;
        }));
        Action(start, "Fight", () => Trouble("A fight", (x, y) => _game.StartFightAt(x, y) ? "A fight has started." : null));
        Action(start, "Someone sick", () => Trouble("Sick", (x, y) => _game.BeSickAt(x, y) ? "Someone has been sick." : null));
        Action(start, "Someone hurt", () => Trouble("An injury", (x, y) => _game.DevHurtAt(x, y) ? "Someone is hurt and needs first aid." : null));
        Action(start, "Litter everywhere", () => Trouble("Litter", (x, y) =>
        {
            var pieces = _game.DevLitterAt(x, y);
            return pieces > 0 ? $"{pieces} pieces of litter dropped." : null;
        }));

        Section("Clear");
        var clear = Row();
        Action(clear, "Clear crush, litter and sick", () =>
        {
            Say(_game.DevClearTrouble() ? "Cleared. Anyone hurt still waits for first aid, and fights still need security." : "Nothing to clear.");
        });
    }

    private void Trouble(string what, System.Func<double, double, string?> start)
    {
        if (!OnTheDay(what))
        {
            return;
        }

        var (x, y) = LookingAt();
        Say(start(x, y) ?? "Nobody about there. Look somewhere with people in it.");
    }

    /// <summary>Where the camera is looking, in metres on the field: the stage on a stage camera.</summary>
    private (double X, double Y) LookingAt()
    {
        if (_scene.StageCam.Showing is not null && _scene.StageCam.FilmingStage is { } stage)
        {
            var (fx, fy, dx, dy) = RunningOrder.Front(stage);
            // A few metres out from the front, in the crowd.
            return (fx + (dx * 4), fy + (dy * 4));
        }

        return (_scene.Camera.Target.X, _scene.Camera.Target.Z);
    }
}
