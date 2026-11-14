using System.Collections.Generic;
using System.Globalization;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Show;
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
    private VBoxContainer _showLines = null!;
    private StyleBoxFlat _showBox = null!;
    private readonly System.Collections.Generic.List<ShowLine> _lines = [];
    private Label _mood = null!;
    private Button _skip = null!;

    /// <summary>
    /// The button that hurries the day on to the next act. The scene hangs it just below the bar,
    /// under the line that says who is on.
    /// </summary>
    public Button SkipButton => _skip;

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

    private Label _year = null!;
    private Label _peopleCaption = null!;
    private Control _queueCell = null!;

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
        _queueCell = Cell(_queue, UiStyle.Caps("Queuing", size: 11), "People queuing at the gates, or waiting outside because there is no way in");
        figures.AddChild(_queueCell);

        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });

        // Who is on stage, in the middle.
        var now = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkBegin, MouseFilter = MouseFilterEnum.Stop };
        var nowBox = UiStyle.Card(0, 999);
        nowBox.ContentMarginLeft = nowBox.ContentMarginRight = 22;
        nowBox.ContentMarginTop = nowBox.ContentMarginBottom = 9;
        now.AddThemeStyleboxOverride("panel", nowBox);
        // One line for each stage with music: one line with a single stage, as many as there are stages.
        _showLines = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _showLines.AddThemeConstantOverride("separation", 2);
        now.AddChild(_showLines);
        _showBox = nowBox;
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

    /// <summary>Fills the pill in the middle with a line for each stage.</summary>
    private void ShowLines(System.Collections.Generic.IReadOnlyList<StageLine> lines)
    {
        var several = lines.Count > 1;
        while (_lines.Count < lines.Count)
        {
            var line = new ShowLine();
            _showLines.AddChild(line.Row);
            _lines.Add(line);
        }

        for (var i = 0; i < _lines.Count; i++)
        {
            _lines[i].Row.Visible = i < lines.Count;
            if (i < lines.Count)
            {
                _lines[i].Set(lines[i], several);
            }
        }

        // Two lines fit in the same height of bar with a little less room above and below.
        _showBox.ContentMarginTop = _showBox.ContentMarginBottom = several ? 5 : 9;
        ShowText = string.Join("\n", System.Linq.Enumerable.Select(lines, l => l.Text));
        _showPill.TooltipText = several ? ShowText : "Who is on stage now, or who is on next";
    }

    /// <summary>One stage's line in the pill: the stage, "On now" or "Next", the act and the time.</summary>
    private sealed class ShowLine
    {
        public readonly HBoxContainer Row = new() { MouseFilter = MouseFilterEnum.Ignore };
        private readonly Label _stage = UiStyle.Caps("", size: 11);
        private readonly Label _kicker = UiStyle.Caps("", pink: true, size: 11);
        private readonly Label _name = UiStyle.Heading("", 19);
        private readonly Label _tail = UiStyle.Body("", muted: true, size: 14);

        public ShowLine()
        {
            Row.AddThemeConstantOverride("separation", 10);
            foreach (var label in new Label[] { _stage, _kicker, _name, _tail })
            {
                label.VerticalAlignment = VerticalAlignment.Center;
                Row.AddChild(label);
            }

            _stage.CustomMinimumSize = new Vector2(58, 0);
        }

        public void Set(StageLine line, bool several)
        {
            _stage.Text = line.Stage;
            _stage.Visible = several && line.Stage.Length > 0;
            _kicker.Text = line.Kicker;
            _kicker.Visible = line.Kicker.Length > 0;
            _name.Text = line.Name;
            _name.AddThemeFontSizeOverride("font_size", several ? 15 : 19);
            _tail.Text = line.Tail;
            _tail.Visible = line.Tail.Length > 0;
        }
    }

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
        _queue.Text = $"{_game.Crowd.Queuing:N0}";
        _queueCell.Visible = QueueText.Length > 0;

        // On the day itself the weather takes the date's place: the date does not change, and the weather does.
        _date.Visible = _game.Phase != Phase.Day;
        _time.TooltipText = _date.TooltipText;
        _money.Text = $"£{_game.Money:N0}";
        MoodText = $"Happy {_game.Crowd.Happiness}%";
        _mood.Text = $"{_game.Crowd.Happiness}%";
        var music = _game.Show;
        ShowLines(OnNow.Lines(_game));
        var upNext = _game.Phase == Phase.Day ? _game.NextAct : null;
        _skip.Visible = _game.Phase == Phase.Day && music.Stage is not null;
        _skip.Disabled = upNext is null && _game.SkippingTo is null;
        _skip.Position = new Vector2(_showPill.GlobalPosition.X + ((_showPill.Size.X - _skip.Size.X) / 2), _showPill.GlobalPosition.Y + _showPill.Size.Y + 8);
        _skip.Text = _game.SkippingTo is not null ? "Skipping..." : "Skip to next act";
        _skip.TooltipText = _game.SkippingTo is not null ? "Click to stop skipping"
            : upNext is null ? "There are no more acts to come today"
            : $"Hurry the day on to {upNext.Act.Name} at {upNext.Start.ToString("HH:mm", British)} (N)";

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
