using System.Collections.Generic;
using System.Globalization;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using Godot;

namespace BlueprintFestival.UI;

/// <summary>The bar along the top: the festival's name, the day and time, and the speed buttons.</summary>
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
    private Label _time = null!;
    private Label _light = null!;
    private Label _people = null!;
    private Label _money = null!;
    private Label _queue = null!;
    private Label _show = null!;
    private Label _mood = null!;

    /// <summary>How happy people are, as the bar says it.</summary>
    public string MoodText => _mood.Text;

    /// <summary>Raised when the money is clicked.</summary>
    public event System.Action? MoneyPressed;

    /// <summary>What the bar says about the music: who is on now, or who is next.</summary>
    public string ShowText => _show.Text;

    public string MoneyText => _money.Text;

    public string QueueText => _queue.Text;

    public string NameText => _name.Text;

    public string TimeText => _time.Text;

    public string PeopleText => _people.Text;

    public void Initialise(FestivalGame game, System.Action openMenu)
    {
        _game = game;
        Theme = UiStyle.Theme;
        var box = UiStyle.Card(margin: 0, radius: 0);
        box.ContentMarginLeft = box.ContentMarginRight = 18;
        box.ContentMarginTop = box.ContentMarginBottom = 8;
        AddThemeStyleboxOverride("panel", box);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 14);
        AddChild(row);

        var flag = new ColorRect { Color = UiStyle.StagePink, CustomMinimumSize = new Vector2(6, 34), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        row.AddChild(flag);
        _name = UiStyle.Heading(game.Name, 22);
        row.AddChild(_name);

        _people = UiStyle.Body("", size: 16);
        _people.TooltipText = "People on the site now, out of everyone expected today";
        _people.MouseFilter = MouseFilterEnum.Stop;
        row.AddChild(new Control { CustomMinimumSize = new Vector2(10, 0) });
        row.AddChild(_people);
        _queue = UiStyle.Body("", muted: true, size: 16);
        _queue.TooltipText = "People queuing at the gates, or waiting outside because there is no way in";
        _queue.MouseFilter = MouseFilterEnum.Stop;
        row.AddChild(_queue);
        _money = UiStyle.Heading("", 20);
        _money.TooltipText = "Money in the festival's bank account. Click to see today's takings.";
        _money.MouseFilter = MouseFilterEnum.Stop;
        _money.MouseDefaultCursorShape = CursorShape.PointingHand;
        _money.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                MoneyPressed?.Invoke();
            }
        };
        _mood = UiStyle.Body("", size: 15);
        _mood.TooltipText = "How happy the people who have come today are";
        _mood.MouseFilter = MouseFilterEnum.Stop;
        row.AddChild(new Control { CustomMinimumSize = new Vector2(10, 0) });
        row.AddChild(_money);
        row.AddChild(_mood);
        _show = UiStyle.Body("", size: 15);
        _show.TooltipText = "Who is on stage now, or who is on next";
        _show.MouseFilter = MouseFilterEnum.Stop;
        row.AddChild(new Control { CustomMinimumSize = new Vector2(10, 0) });
        row.AddChild(_show);

        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        _light = UiStyle.Body("", muted: true, size: 14);
        _light.Visible = false;
        row.AddChild(_light);
        _date = UiStyle.Body("", muted: true);
        _date.MouseFilter = MouseFilterEnum.Stop;
        row.AddChild(_date);
        _time = UiStyle.Heading("", 26);
        _time.CustomMinimumSize = new Vector2(78, 0);
        _time.HorizontalAlignment = HorizontalAlignment.Center;
        row.AddChild(_time);

        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        var speeds = new HBoxContainer();
        speeds.AddThemeConstantOverride("separation", 6);
        row.AddChild(speeds);
        foreach (var (speed, text, tip) in Speeds)
        {
            var button = UiStyle.SecondaryButton(text, () => _game.Clock.SetSpeed(speed));
            button.ToggleMode = true;
            button.TooltipText = tip;
            speeds.AddChild(button);
            _speedButtons[speed] = button;
        }

        var menu = UiStyle.SecondaryButton("Menu", openMenu);
        menu.TooltipText = "Menu (Esc)";
        row.AddChild(menu);
        SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide, LayoutPresetMode.Minsize);
        Refresh();
    }

    public override void _Process(double delta) => Refresh();

    private void Refresh()
    {
        var now = _game.Now;
        _date.Text = now.ToString("ddd d MMM", British);
        _date.TooltipText = now.ToString("dddd d MMMM yyyy", British);
        _time.Text = now.ToString("HH:mm", British);
        _people.Text = $"On site {_game.Crowd.OnSite} of {_game.Crowd.Expected}";
        _queue.Text = _game.Crowd.Queuing > 0 ? $"Queuing {_game.Crowd.Queuing}" : "";

        // There is not room for both: the queue matters more than the date while there is one.
        _date.Visible = _queue.Text.Length == 0;
        _money.Text = $"£{_game.Money:N0}";
        _mood.Text = $"Happy {_game.Crowd.Happiness}%";
        var music = _game.Show;
        _show.Text = music.Stage is null ? "No stage, no music"
            : music.CalledOff is not null ? "The music has been called off"
            : _game.ActPaused && music.Playing(now) is { } stopped ? $"Stopped: {stopped.Act.Name}"
            : music.Playing(now) is { } on ? $"Now: {on.Act.Name} until {on.End.ToString("HH:mm", British)}"
            : music.Next(now) is { } next ? $"Next: {next.Act.Name} at {next.Start.ToString("HH:mm", British)}"
            : "The music has finished";
        var light = _game.Light;
        _light.Text = light >= 1 ? "Daylight" : light <= 0 ? "Night" : now.Hour < 12 ? "Dawn" : "Dusk";
        foreach (var (speed, button) in _speedButtons)
        {
            button.SetPressedNoSignal(_game.Clock.Speed == speed);
        }
    }
}
