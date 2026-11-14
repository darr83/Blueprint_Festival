using System;
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
/// stopped, or the day has been called off.
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
    private Button _pause = null!;
    private Button _show = null!;
    private double _sinceRefresh = 1;

    /// <summary>Raised when the player asks to be shown where the trouble is, with the place in metres.</summary>
    public event Action<double, double>? ShowMe;

    /// <summary>The warning card's heading, or nothing while there is no warning.</summary>
    public string WarningText => _warning.Visible ? _heading.Text : "";

    /// <summary>Whether the key to the colours is on screen.</summary>
    public bool KeyShown => _key.Visible;

    /// <summary>Whether the warning card is offering to pause the act.</summary>
    public bool CanPause => _warning.Visible && _pause.Visible;

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
        rows.AddChild(UiStyle.Heading("Crowd view", 18));
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
        _key.OffsetTop = 66;
        _key.OffsetBottom = 66;

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
        _heading = UiStyle.Heading("", 20);
        column.AddChild(_heading);
        _body = UiStyle.Body("", muted: true, size: 15);
        column.AddChild(_body);
        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 8);
        column.AddChild(buttons);
        _pause = UiStyle.Button("Pause the act", Pause);
        _pause.TooltipText = "Stop the music for a few minutes so the crowd moves back. The audience will not thank you.";
        buttons.AddChild(_pause);
        _show = UiStyle.SecondaryButton("Show me", Look);
        buttons.AddChild(_show);
        AddChild(_warning);
        _warning.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight, LayoutPresetMode.Minsize, 16);
        _warning.GrowHorizontal = GrowDirection.Begin;
        _warning.OffsetTop = 66;
        _warning.OffsetBottom = 66;
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

    /// <summary>Stops the act, as the button on the warning card does.</summary>
    public void Pause()
    {
        _game.PauseAct();
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
                return "at the stage";
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
                $"{hurt} people were hurt in a crush {Where(x, y)}.\nThe music has stopped and everyone is going home.", pause: false, show: false);
        }
        else if (day && _game.ActPaused)
        {
            Warn(DensityView.BusyAmber, "The act has stopped",
                $"The crowd is moving back from the front.{(hurtLine.Length > 0 ? " " + hurtLine : "")}\nThe music starts again in a few minutes.", pause: false, show: true);
        }
        else if (day && _game.CrowdStatus == Crowding.CrushRisk)
        {
            Warn(DensityView.CrushRed, $"Crush risk {Where(x, y)}",
                $"{_game.Density.Worst:0.0} people per square metre. {(hurtLine.Length > 0 ? hurtLine : "People will be hurt if it stays like this.")}\nSecurity posted there turn people away before it gets this bad.",
                pause: _game.Show.Playing(_game.Now) is not null, show: true);
        }
        else
        {
            _warning.Visible = false;
        }
    }

    /// <summary>How much room to leave on the right for another panel that is open there, in pixels.</summary>
    public float RightInset { get; set; }

    private void Warn(Color stripe, string heading, string body, bool pause, bool show)
    {
        _warning.Visible = true;
        _warning.ResetSize();
        _warning.Position = new Vector2(GetViewportRect().Size.X - 16 - RightInset - _warning.Size.X, 66);
        _stripe.Color = stripe;
        _heading.Text = heading;
        _body.Text = body;
        _pause.Visible = pause;
        _show.Visible = show;
    }
}
