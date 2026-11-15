using System;
using System.Linq;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.UI;

/// <summary>
/// Crowd safety on screen: the key to the crowd view's three colours, top left while the view is
/// on, and a warning card under the top bar whenever somewhere is a crush risk, an act has been
/// stopped, or the day has been called off. While a crush builds the card offers four choices:
/// stop the music for an announcement, send security in, cancel the act, or leave it.
/// </summary>
public partial class CrowdPanel : Control
{
    private static readonly System.Globalization.CultureInfo British = System.Globalization.CultureInfo.GetCultureInfo("en-GB");

    private FestivalGame _game = null!;
    private DensityView _view = null!;
    private PanelContainer _key = null!;
    private PanelContainer _warning = null!;
    private ColorRect _stripe = null!;
    private Label _heading = null!;
    private Label _body = null!;
    private GridContainer _choices = null!;
    private Button _announce = null!;
    private Button _security = null!;
    private Button _cancel = null!;
    private Button _leave = null!;
    private Label _announceNote = null!;
    private Label _securityNote = null!;
    private Label _cancelNote = null!;
    private Label _leaveNote = null!;
    private Button _again = null!;
    private Button _show = null!;
    private bool _reopened;
    private double _sinceRefresh = 1;

    /// <summary>Raised when the player asks to be shown where the trouble is, with the place in metres.</summary>
    public event Action<double, double>? ShowMe;

    /// <summary>The warning card's heading, or nothing while there is no warning.</summary>
    public string WarningText => _warning.Visible ? _heading.Text : "";

    /// <summary>Whether the key to the colours is on screen.</summary>
    public bool KeyShown => _key.Visible;

    /// <summary>Whether the warning card is offering the four crush choices.</summary>
    public bool ChoicesShown => _warning.Visible && _choices.Visible;

    /// <summary>Whether the player could stop the music for an announcement from the card.</summary>
    public bool CanPause => ChoicesShown && !_announce.Disabled;

    public void Initialise(FestivalGame game, DensityView view)
    {
        _game = game;
        _view = view;
        Theme = UiStyle.Theme;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _key = new PanelContainer();
        _key.AddThemeStyleboxOverride("panel", UiStyle.Card(margin: 14, radius: 14));
        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", 6);
        _key.AddChild(rows);
        rows.AddChild(UiStyle.Heading("Crowd view", 20));
        foreach (var (status, text) in new[]
        {
            (Crowding.Comfortable, "Comfortable: under 2 people per m²"),
            (Crowding.Busy, "Busy: 2 to 4 people per m²"),
            (Crowding.CrushRisk, "Crush risk: over 4 people per m²"),
        })
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            row.AddChild(new ColorRect { Color = DensityView.ColourOf(status), CustomMinimumSize = new Vector2(18, 18), SizeFlagsVertical = SizeFlags.ShrinkCenter });
            row.AddChild(UiStyle.Body(text, size: 15));
            rows.AddChild(row);
        }

        AddChild(_key);
        _key.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft, LayoutPresetMode.Minsize, 16);
        _key.OffsetTop = UiStyle.TopClear;
        _key.OffsetBottom = UiStyle.TopClear;
        _key.OffsetLeft = _key.OffsetRight = 22;

        _warning = new PanelContainer();
        _warning.AddThemeStyleboxOverride("panel", UiStyle.Card(margin: 14, radius: 14));
        var line = new HBoxContainer();
        line.AddThemeConstantOverride("separation", 12);
        _warning.AddChild(line);
        _stripe = new ColorRect { CustomMinimumSize = new Vector2(8, 0) };
        line.AddChild(_stripe);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 6);
        line.AddChild(column);
        _heading = UiStyle.Heading("", 22);
        column.AddChild(_heading);
        _body = UiStyle.Body("", muted: true, size: 15);
        column.AddChild(_body);
        _choices = new GridContainer { Columns = 2 };
        _choices.AddThemeConstantOverride("h_separation", 10);
        _choices.AddThemeConstantOverride("v_separation", 8);
        column.AddChild(_choices);
        (_announce, _announceNote) = Choice(UiStyle.Button("Stop and announce", () => Choose(CrushChoice.Announce)),
            "Stop the music for a few minutes while the PA asks everyone to take a step back. Fewer people listen each time.");
        (_security, _securityNote) = Choice(UiStyle.SecondaryButton("Send security in", () => Choose(CrushChoice.SendSecurity)),
            "The nearest security walk over, hold the spot for ten minutes and lift people out. Their own posts stand empty meanwhile.");
        (_cancel, _cancelNote) = Choice(UiStyle.SecondaryButton("Cancel the act", () => Choose(CrushChoice.CancelAct)),
            "The act walks off now and the crowd spreads out. Their fans are let down, some go home, and the fee is not given back.");
        (_leave, _leaveNote) = Choice(UiStyle.SecondaryButton("Do nothing", () => Choose(CrushChoice.LeaveIt)),
            "The music plays on and the crowd may sort itself out. If anyone is hurt, the council will hear about it.");
        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 8);
        column.AddChild(buttons);
        _again = UiStyle.Button("Choose again", Reopen);
        buttons.AddChild(_again);
        _show = UiStyle.SecondaryButton("Show me", Look);
        buttons.AddChild(_show);
        AddChild(_warning);
        _warning.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight, LayoutPresetMode.Minsize, 16);
        _warning.GrowHorizontal = GrowDirection.Begin;
        _warning.OffsetTop = UiStyle.TopClear;
        _warning.OffsetBottom = UiStyle.TopClear;
        Refresh();
    }

    public override void _Process(double delta)
    {
        _sinceRefresh += delta;
        if (_sinceRefresh > 0.3)
        {
            Refresh();
        }
    }

    /// <summary>One of the four choices: its button, and a line under it saying what it costs.</summary>
    private (Button, Label) Choice(Button button, string tooltip)
    {
        var cell = new VBoxContainer { CustomMinimumSize = new Vector2(230, 0) };
        cell.AddThemeConstantOverride("separation", 2);
        button.TooltipText = tooltip;
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        cell.AddChild(button);
        var note = UiStyle.Body("", muted: true, size: 13);
        note.CustomMinimumSize = new Vector2(230, 0);
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        cell.AddChild(note);
        _choices.AddChild(cell);
        return (button, note);
    }

    /// <summary>Does what the player chose for the crush, as the buttons on the warning card do.</summary>
    public void Choose(CrushChoice choice)
    {
        _game.Choose(choice);
        _reopened = false;
        Refresh();
    }

    /// <summary>Stops the act for an announcement, as the first button on the warning card does.</summary>
    public void Pause() => Choose(CrushChoice.Announce);

    /// <summary>Brings the four choices back after the player chose to leave a crush alone.</summary>
    private void Reopen()
    {
        _reopened = true;
        Refresh();
    }

    private void Look()
    {
        var (x, y) = _game.Density.WorstAt;
        _view.Showing = true;
        ShowMe?.Invoke(x, y);
    }

    /// <summary>Where on the site a place in metres is, in words.</summary>
    private string Where(double x, double y)
    {
        if (_game.Show.Stage is { } stage)
        {
            var (fx, fy, _, _) = RunningOrder.Front(stage);
            if (Math.Abs(x - fx) < 18 && Math.Abs(y - fy) < 18)
            {
                return _game.SecondShow.Stage is null ? "at the stage" : "at the main stage";
            }
        }

        if (_game.SecondShow.Stage is { } other)
        {
            var (fx, fy, _, _) = RunningOrder.Front(other);
            if (Math.Abs(x - fx) < 14 && Math.Abs(y - fy) < 14)
            {
                return "at the second stage";
            }
        }

        var gap = _game.Field.Gap;
        var (gx, gy) = ((gap.X + (gap.Width / 2.0)) * FieldMap.TileMetres, gap.Y * FieldMap.TileMetres);
        return Math.Abs(x - gx) < 10 && Math.Abs(y - gy) < 10 ? "at the gate" : "in the field";
    }

    /// <summary>Brings both cards up to date with the rules.</summary>
    public void Refresh()
    {
        _sinceRefresh = 0;
        _key.Visible = _view.Showing;
        var day = _game.Phase == Phase.Day;
        var hurt = _game.CrushInjuries;
        var hurtLine = hurt == 0 ? "" : hurt == 1 ? "One person has been hurt." : $"{hurt} people have been hurt.";
        var (x, y) = _game.Density.WorstAt;
        if (day && _game.SeriousIncident)
        {
            Warn(DensityView.CrushRed, "The day has been called off",
                $"{hurt} people were hurt in a crush {Where(x, y)}.\nThe music has stopped and everyone is going home.", choices: false, again: false, show: false);
        }
        else if (day && _game.ActPaused)
        {
            Warn(DensityView.BusyAmber, "The music has stopped",
                $"The PA is asking everyone to take a step back.{(hurtLine.Length > 0 ? " " + hurtLine : "")}\nThe music starts again in a few minutes.", choices: false, again: false, show: true);
        }
        else if (day && _game.CrushBuilding && (!_game.CrushLeftAlone || _reopened))
        {
            (x, y) = _game.CrushSpot;
            Warn(DensityView.CrushRed, $"Crush risk {Where(x, y)}",
                $"{_game.Density.Worst:0.0} people per square metre. {(hurtLine.Length > 0 ? hurtLine : "People will be hurt if it stays like this.")}\nWhat do you do?",
                choices: true, again: false, show: true);
            UpdateChoices();
        }
        else if (day && _game.CrushBuilding)
        {
            (x, y) = _game.CrushSpot;
            Warn(DensityView.CrushRed, $"Crush risk {Where(x, y)}, left alone",
                $"{_game.Density.Worst:0.0} people per square metre. {(hurtLine.Length > 0 ? hurtLine : "Nobody has been hurt yet.")}", choices: false, again: true, show: true);
        }
        else if (day && _game.Staff.Any(s => s.State is StaffState.Sent or StaffState.Holding) && _game.Density.RedSquares > 0)
        {
            var there = _game.Staff.Count(s => s.State == StaffState.Holding);
            Warn(DensityView.BusyAmber, "Security are going in",
                there == 0 ? "They are on their way to the crush." : $"{there} are in the crowd, lifting people out to the back.", choices: false, again: false, show: true);
        }
        else
        {
            _warning.Visible = false;
            _reopened = false;
        }
    }

    /// <summary>How much room to leave on the right for another panel that is open there, in pixels.</summary>
    public float RightInset { get; set; }

    /// <summary>Brings the four choices up to date: what each would cost now, and which can be done.</summary>
    private void UpdateChoices()
    {
        var heard = _game.AnnouncementHeard;
        var canAnnounce = _game.MusicOn || _game.CrowdWaiting;
        _announce.Disabled = !canAnnounce;
        _announceNote.Text = !canAnnounce ? "No music is on to stop." :
            heard >= 1 ? "Free. The crowd gets a little bored." :
            $"Free. Only about {heard * 100:0}% will listen this time.";

        var free = _game.SecurityFree;
        _security.Disabled = free == 0;
        _securityNote.Text = free == 0 ? "No security are free." :
            $"{free} go, £{free * FestivalGame.Overtime} overtime. The music plays on.";

        var act = _game.ActAtCrush;
        _cancel.Disabled = act is null;
        _cancelNote.Text = act is null ? "No act is on." : act.Start > _game.Now ? $"Call off {act.Act.Name}. Fans upset, fee lost." : $"{act.Act.Name} walk off. Fans upset, fee lost.";

        _leave.Disabled = false;
        _leaveNote.Text = "Free, but anyone hurt counts against the licence.";
    }

    private void Warn(Color stripe, string heading, string body, bool choices, bool again, bool show)
    {
        _warning.Visible = true;
        _warning.ResetSize();
        _warning.Position = new Vector2(GetViewportRect().Size.X - 22 - RightInset - _warning.Size.X, UiStyle.TopClear);
        _stripe.Color = stripe;
        _heading.Text = heading;
        _body.Text = body;
        _choices.Visible = choices;
        _again.Visible = again;
        _show.Visible = show;
    }
}
