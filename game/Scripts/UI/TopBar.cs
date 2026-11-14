using System.Collections.Generic;
using System.Globalization;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using Godot;

namespace BlueprintFestival.UI;

/// <summary>
/// The badges along the top: a ticket with the festival's name, the clock, the bank and the crowd;
/// who is on stage in the middle; and the speed buttons on the right. The field shows between them.
/// </summary>
public partial class TopBar : PanelContainer
{
    private static readonly CultureInfo British = CultureInfo.GetCultureInfo("en-GB");

    private static readonly (GameSpeed Speed, string Text, string Tip)[] Speeds =
    {
        (GameSpeed.Paused, "Pause", "Pause (Space)"),
        (GameSpeed.Normal, "1x", "Normal speed (1)"),
        (GameSpeed.Fast, "2x", "Double speed (2)"),
        (GameSpeed.VeryFast, "4x", "Four times speed (3)"),
        (GameSpeed.Fastest, "8x", "Eight times speed (4)"),
        (GameSpeed.Testing16, "16x", "Sixteen times speed, for testing (5)"),
        (GameSpeed.Testing32, "32x", "Thirty-two times speed, for testing (6)"),
    };

    private readonly Dictionary<GameSpeed, Button> _speedButtons = new();
    private FestivalGame _game = null!;
    private Label _name = null!;
    private Label _date = null!;
    private Label _weather = null!;
    private Label _time = null!;
    private Label _light = null!;
    private Label _people = null!;
    private Label _money = null!;
    private Label _queue = null!;
    private Label _show = null!;
    private Label _mood = null!;
    private Button _skip = null!;

    /// <summary>
    /// The button that hurries the day on to the next act. The scene hangs it just below the bar,
    /// under the line that says who is on.
    /// </summary>
    public Button SkipButton => _skip;

    private Button _gates = null!;
    private PanelContainer _ticket = null!;

    /// <summary>
    /// The button that opens the gates on the morning of the festival. The scene hangs it just
    /// below the ticket badge; it shows only while the gates are shut.
    /// </summary>
    public Button GateButton => _gates;

    /// <summary>How happy people are, as the bar says it.</summary>
    public string MoodText { get; private set; } = "";

    /// <summary>Raised when the money is clicked.</summary>
    public event System.Action? MoneyPressed;

    /// <summary>What the bar says about the music: who is on now, or who is next.</summary>
    public string ShowText { get; private set; } = "";

    public string MoneyText => _money.Text;

    public string QueueText { get; private set; } = "";

    public string NameText => _name.Text;

    public string TimeText => _time.Text;

    public string PeopleText { get; private set; } = "";

    private Label _showKicker = null!;
    private Label _showTail = null!;
    private Label _year = null!;
    private Label _peopleCaption = null!;
    private Control _queueCell = null!;
    private Label _queueCaption = null!;

    private static VBoxContainer Cell(Label big, Control caption, string tip)
    {
        var cell = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Stop, TooltipText = tip };
        cell.AddThemeConstantOverride("separation", -2);
        big.MouseFilter = MouseFilterEnum.Ignore;
        caption.MouseFilter = MouseFilterEnum.Ignore;
        cell.AddChild(big);
        cell.AddChild(caption);
        return cell;
    }

    private static Label Figure(int size = 18)
    {
        var label = UiStyle.Body("", size: size);
        label.AddThemeFontOverride("font", UiStyle.SansBold);
        return label;
    }

    public void Initialise(FestivalGame game, System.Action openMenu)
    {
        _game = game;
        Theme = UiStyle.Theme;
        MouseFilter = MouseFilterEnum.Ignore;
        var none = new StyleBoxEmpty { ContentMarginLeft = 22, ContentMarginRight = 22, ContentMarginTop = 22 };
        AddThemeStyleboxOverride("panel", none);

        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 16);
        AddChild(row);

        // The ticket: the festival's name on the pink end, then the clock, the bank and the crowd.
        var ticket = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkBegin };
        ticket.AddThemeStyleboxOverride("panel", UiStyle.Card(0, 12));
        var parts = new HBoxContainer();
        parts.AddThemeConstantOverride("separation", 0);
        ticket.AddChild(parts);
        row.AddChild(ticket);

        var pink = new PanelContainer();
        var pinkBox = UiStyle.Box(UiStyle.StagePink, 12);
        pinkBox.CornerRadiusTopRight = pinkBox.CornerRadiusBottomRight = 0;
        pinkBox.ContentMarginLeft = pinkBox.ContentMarginRight = 18;
        pinkBox.ContentMarginTop = pinkBox.ContentMarginBottom = 10;
        pink.AddThemeStyleboxOverride("panel", pinkBox);
        var title = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        title.AddThemeConstantOverride("separation", -2);
        _name = UiStyle.Heading(game.Name, 22);
        _name.AddThemeFontOverride("font", UiStyle.SerifHeavy);
        _name.AddThemeColorOverride("font_color", Colors.White);
        _name.ClipText = true;
        _name.CustomMinimumSize = new Vector2(Mathf.Clamp(UiStyle.SerifHeavy.GetStringSize(game.Name, fontSize: 22).X + 4, 90, 250), 0);
        title.AddChild(_name);
        _year = UiStyle.Caps("", size: 11);
        _year.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.92f));
        title.AddChild(_year);
        pink.AddChild(title);
        parts.AddChild(pink);
        parts.AddChild(new TearLine());

        var cells = new MarginContainer();
        cells.AddThemeConstantOverride("margin_left", 18);
        cells.AddThemeConstantOverride("margin_right", 20);
        cells.AddThemeConstantOverride("margin_top", 9);
        cells.AddThemeConstantOverride("margin_bottom", 9);
        var figures = new HBoxContainer();
        figures.AddThemeConstantOverride("separation", 22);
        cells.AddChild(figures);
        parts.AddChild(cells);
        _ticket = ticket;
        _gates = UiStyle.Button("Open the gates", () => _game.OpenGates());
        _gates.Theme = UiStyle.Theme;
        _gates.Visible = false;

        _time = UiStyle.Heading("", 30);
        _time.CustomMinimumSize = new Vector2(84, 0);
        var when = new HBoxContainer();
        when.AddThemeConstantOverride("separation", 6);
        _date = UiStyle.Caps("", size: 11);
        _weather = UiStyle.Caps("", size: 11);
        _light = UiStyle.Body("", muted: true, size: 14);
        _light.Visible = false;
        when.AddChild(_date);
        when.AddChild(_weather);
        when.AddChild(_light);
        var clock = Cell(_time, when, "");
        _weather.MouseFilter = MouseFilterEnum.Stop;
        figures.AddChild(clock);

        _money = Figure();
        var bank = Cell(_money, UiStyle.Caps("In the bank", size: 11), "Money in the festival's bank account. Click to see today's takings.");
        bank.MouseDefaultCursorShape = CursorShape.PointingHand;
        bank.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                MoneyPressed?.Invoke();
            }
        };
        figures.AddChild(bank);

        _mood = Figure();
        figures.AddChild(Cell(_mood, UiStyle.Caps("Happy", size: 11), "How happy the people who have come today are"));

        _people = Figure();
        _peopleCaption = UiStyle.Caps("", size: 11);
        figures.AddChild(Cell(_people, _peopleCaption, "People on the site now, out of everyone expected today"));

        _queue = Figure();
        _queueCaption = UiStyle.Caps("Queuing", size: 11);
        _queueCell = Cell(_queue, _queueCaption, "People queuing at the gates, or waiting outside because there is no way in");
        figures.AddChild(_queueCell);

        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });

        // Who is on stage, in the middle.
        var now = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkBegin, MouseFilter = MouseFilterEnum.Stop };
        var nowBox = UiStyle.Card(0, 999);
        nowBox.ContentMarginLeft = nowBox.ContentMarginRight = 22;
        nowBox.ContentMarginTop = nowBox.ContentMarginBottom = 9;
        now.AddThemeStyleboxOverride("panel", nowBox);
        var playing = new HBoxContainer();
        playing.AddThemeConstantOverride("separation", 10);
        _showKicker = UiStyle.Caps("", pink: true, size: 11);
        _show = UiStyle.Heading("", 19);
        _showTail = UiStyle.Body("", muted: true, size: 14);
        playing.AddChild(_showKicker);
        playing.AddChild(_show);
        playing.AddChild(_showTail);
        now.AddChild(playing);
        _showPill = now;
        var nowHolder = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ShrinkBegin };
        nowHolder.AddThemeConstantOverride("margin_top", 8);
        nowHolder.AddChild(now);
        row.AddChild(nowHolder);
        _skip = UiStyle.SecondaryButton("Skip to next act", () =>
        {
            if (_game.SkippingTo is null)
            {
                _game.SkipToNextAct();
            }
            else
            {
                _game.StopSkipping();
            }
        });
        _skip.Theme = UiStyle.Theme;

        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });

        // Pause, the speeds and the menu, in a pill on the right.
        var pill = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkBegin };
        pill.AddThemeStyleboxOverride("panel", UiStyle.Card(7, 999));
        var speeds = new HBoxContainer();
        speeds.AddThemeConstantOverride("separation", 4);
        pill.AddChild(speeds);
        row.AddChild(pill);
        foreach (var (speed, text, tip) in Speeds)
        {
            var button = UiStyle.RoundButton(speed == GameSpeed.Paused ? "pause" : text.Replace("x", "×"), () => _game.Clock.SetSpeed(speed));
            button.ToggleMode = true;
            button.TooltipText = tip;
            speeds.AddChild(button);
            _speedButtons[speed] = button;
        }

        var menu = UiStyle.RoundButton("menu", openMenu);
        menu.TooltipText = "Menu (Esc)";
        speeds.AddChild(menu);
        SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide, LayoutPresetMode.Minsize);
        Refresh();
    }

    private PanelContainer _showPill = null!;

    /// <summary>The weather in a word or two, as the top bar shows it.</summary>
    public static string WeatherWord(FestivalGame game) =>
        game.RainNow > 0.7 ? "Heavy rain" : game.RainNow > 0.05 ? "Raining" :
        game.Weather.Kind == WeatherKind.Hot ? $"Hot {game.Weather.High}°C" :
        game.Weather.Wet ? (game.Phase == Phase.Day ? "Dry for now" : game.Weather.Word) : $"Fine {game.Weather.High}°C";

    public string WeatherText => _weather.Text;

    public override void _Process(double delta) => Refresh();

    private void Refresh()
    {
        var now = _game.Now;
        _date.Text = now.ToString("ddd d MMM", British) + "  ·";
        _date.TooltipText = now.ToString("dddd d MMMM yyyy", British);
        _time.Text = now.ToString("HH:mm", British);
        _year.Text = $"Year {_game.Year}";
        PeopleText = $"On site {_game.Crowd.OnSite} of {_game.Crowd.Expected}";
        _people.Text = $"{_game.Crowd.OnSite:N0}";
        _peopleCaption.Text = $"of {_game.Crowd.Expected:N0} here";
        QueueText = _game.Crowd.Queuing > 0 ? $"Queuing {_game.Crowd.Queuing}" : "";
        _queueCaption.Text = _game.Phase == Phase.Day && _game.GatesShut ? "Outside" : "Queuing";
        _queue.Text = $"{_game.Crowd.Queuing:N0}";
        _queueCell.Visible = QueueText.Length > 0;

        // On the day itself the weather takes the date's place: the date does not change, and the weather does.
        _date.Visible = _game.Phase != Phase.Day;
        _time.TooltipText = _date.TooltipText;
        _money.Text = $"£{_game.Money:N0}";
        MoodText = $"Happy {_game.Crowd.Happiness}%";
        _mood.Text = $"{_game.Crowd.Happiness}%";
        var music = _game.Show;
        var (kicker, name, tail) = music.Stage is null ? ("", "No stage, no music", "")
            : music.CalledOff is not null ? ("", "The music has been called off", "")
            : _game.ActPaused && music.Playing(now) is { } stopped ? ("Stopped", stopped.Act.Name, "")
            : music.Playing(now) is { } on ? ("Now", on.Act.Name, $"until {on.End.ToString("HH:mm", British)}")
            : music.Next(now) is { } next ? ("Next", next.Act.Name, $"at {next.Start.ToString("HH:mm", British)}")
            : ("", "The music has finished", "");
        if (kicker == "Next" && music.Changeover(now))
        {
            // The half hour after a set, while people go for a drink and the next act sets up.
            kicker = "Stage empty. Next";
        }

        ShowText = kicker.Length == 0 ? name : tail.Length == 0 ? $"{kicker}: {name}" : $"{kicker}: {name} {tail}";
        var other = _game.SecondShow;
        if (other.Stage is not null && other.CalledOff is null)
        {
            // The second stage gets the line when the main stage is between sets, and the tooltip always.
            var second = other.Playing(now) is { } playing ? ("Second stage", playing.Act.Name, $"until {playing.End.ToString("HH:mm", British)}")
                : other.Next(now) is { } coming ? ("Second stage next", coming.Act.Name, $"at {coming.Start.ToString("HH:mm", British)}")
                : ("", "The second stage has finished", "");
            _showPill.TooltipText = second.Item1.Length == 0 ? second.Item2 : $"{second.Item1}: {second.Item2} {second.Item3}";
            if (music.Stage is not null && music.CalledOff is null && music.Playing(now) is null && other.Playing(now) is not null)
            {
                (kicker, name, tail) = second;
                ShowText = _showPill.TooltipText;
            }
        }
        else
        {
            _showPill.TooltipText = "Who is on stage now, or who is on next";
        }

        _gates.Visible = _game.Phase == Phase.Day && _game.GatesShut;
        if (_gates.Visible)
        {
            var waiting = _game.Crowd.Queuing;
            _gates.TooltipText = waiting > 0
                ? $"{waiting:N0} {(waiting == 1 ? "person is" : "people are")} queuing outside. Ticket staff start checking tickets once the gates are open (G)"
                : "Nobody is let in until the gates are open (G)";
            _gates.Position = new Vector2(_ticket.GlobalPosition.X, _ticket.GlobalPosition.Y + _ticket.Size.Y + 10);
        }

        var upNext = _game.Phase == Phase.Day ? _game.NextAct : null;
        _skip.Visible = _game.Phase == Phase.Day && music.Stage is not null;
        _skip.Disabled = upNext is null && _game.SkippingTo is null;
        _skip.Position = new Vector2(_showPill.GlobalPosition.X + ((_showPill.Size.X - _skip.Size.X) / 2), _showPill.GlobalPosition.Y + _showPill.Size.Y + 8);
        _skip.Text = _game.SkippingTo is not null ? "Skipping..." : "Skip to next act";
        _skip.TooltipText = _game.SkippingTo is not null ? "Click to stop skipping"
            : upNext is null ? "There are no more acts to come today"
            : $"Hurry the day on to {upNext.Act.Name} at {upNext.Start.ToString("HH:mm", British)} (N)";

        _showKicker.Text = kicker == "Now" ? "On now" : kicker;
        _showKicker.Visible = kicker.Length > 0;
        _show.Text = name;
        _showTail.Text = tail;
        _showTail.Visible = tail.Length > 0;
        _weather.Text = WeatherWord(_game);
        _weather.TooltipText = _game.Weather.Summary + (_game.Wetness > 0.05 ? $" The ground is {(_game.Wetness > 0.6 ? "sodden" : _game.Wetness > 0.3 ? "wet" : "damp")}." : "");
        var light = _game.Light;
        _light.Text = light >= 1 ? "Daylight" : light <= 0 ? "Night" : now.Hour < 12 ? "Dawn" : "Dusk";
        foreach (var (speed, button) in _speedButtons)
        {
            button.SetPressedNoSignal(_game.Clock.Speed == speed);
        }
    }
}
