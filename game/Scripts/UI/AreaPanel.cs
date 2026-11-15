using System;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.UI;

/// <summary>
/// Crowd areas on screen. While building, a card on the right for the area just drawn or
/// clicked: its size, how many it lets in (with the game's suggestion and where it turns busy
/// and then a crush risk), its gaps and the security on them. On the day, a card when an area
/// fills up, with a button to let a few more in.
/// </summary>
public partial class AreaPanel : Control
{
    private FestivalGame _game = null!;
    private BuildTool _tool = null!;
    private PanelContainer _editor = null!;
    private Label _kind = null!;
    private Label _size = null!;
    private Label _limit = null!;
    private Label _feel = null!;
    private Label _advice = null!;
    private Label _gaps = null!;
    private Button _takeDown = null!;
    private PanelContainer _full = null!;
    private Label _fullHeading = null!;
    private Label _fullBody = null!;
    private int _fullArea = -1;
    private double _fullFor;
    private double _since = 1;

    /// <summary>How many more people "Let in more" lets into a full area.</summary>
    public const int MoreAtOnce = 30;

    /// <summary>How long the card about a full area stays up, in seconds.</summary>
    public const double FullCardSeconds = 25;

    /// <summary>Raised when the player asks to see an area, with its middle in metres.</summary>
    public event Action<double, double>? ShowMe;

    /// <summary>True while the card for setting an area's limit is open.</summary>
    public bool EditorShown => _editor.Visible;

    /// <summary>The heading of the card about a full area, or nothing while there is none.</summary>
    public string FullText => _full.Visible ? _fullHeading.Text : "";

    /// <summary>What the limit line says, for the launch test.</summary>
    public string LimitText => _limit.Text;

    /// <summary>How far down the screen the cards may start: below anything else on the right.</summary>
    public float TopInset { get; set; } = UiStyle.TopClear;

    public void Initialise(FestivalGame game, BuildTool tool)
    {
        _game = game;
        _tool = tool;
        Theme = UiStyle.Theme;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _editor = new PanelContainer { CustomMinimumSize = new Vector2(370, 0), Visible = false };
        _editor.AddThemeStyleboxOverride("panel", UiStyle.Card(margin: 20, radius: 14));
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        _editor.AddChild(box);
        box.AddChild(UiStyle.Caps("Areas · click inside to change", pink: true));
        _kind = UiStyle.Heading("", 24);
        box.AddChild(_kind);
        _size = UiStyle.Body("", size: 15, bold: true);
        box.AddChild(_size);
        box.AddChild(UiStyle.Rule(1));

        box.AddChild(UiStyle.Caps("How many to let in"));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        row.AddChild(UiStyle.ChipButton("−10", () => Change(-10)));
        row.AddChild(UiStyle.ChipButton("−1", () => Change(-1)));
        _limit = UiStyle.Heading("", 26);
        _limit.CustomMinimumSize = new Vector2(80, 0);
        _limit.HorizontalAlignment = HorizontalAlignment.Center;
        row.AddChild(_limit);
        row.AddChild(UiStyle.ChipButton("+1", () => Change(1)));
        row.AddChild(UiStyle.ChipButton("+10", () => Change(10)));
        box.AddChild(row);
        _feel = UiStyle.Body("", size: 15, bold: true);
        box.AddChild(_feel);
        _advice = Wrapped("");
        box.AddChild(_advice);
        box.AddChild(UiStyle.SecondaryButton("Back to the suggestion", Suggest));
        box.AddChild(UiStyle.Rule(1));

        box.AddChild(UiStyle.Caps("Ways in"));
        _gaps = Wrapped("");
        box.AddChild(_gaps);
        box.AddChild(UiStyle.Rule(1));
        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 10);
        buttons.AddChild(UiStyle.Button("Done", () => _tool.SelectArea(-1)));
        _takeDown = UiStyle.SecondaryButton("Take it down", TakeDown);
        buttons.AddChild(_takeDown);
        box.AddChild(buttons);
        AddChild(_editor);

        _full = new PanelContainer { CustomMinimumSize = new Vector2(370, 0), Visible = false };
        _full.AddThemeStyleboxOverride("panel", UiStyle.Card(margin: 16, radius: 14));
        var line = new HBoxContainer();
        line.AddThemeConstantOverride("separation", 12);
        _full.AddChild(line);
        line.AddChild(new ColorRect { Color = UiStyle.StagePink, CustomMinimumSize = new Vector2(8, 0) });
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 6);
        line.AddChild(column);
        _fullHeading = UiStyle.Heading("", 21);
        column.AddChild(_fullHeading);
        _fullBody = Wrapped("");
        column.AddChild(_fullBody);
        var choices = new HBoxContainer();
        choices.AddThemeConstantOverride("separation", 8);
        choices.AddChild(UiStyle.Button($"Let in {MoreAtOnce} more", LetInMore));
        choices.AddChild(UiStyle.SecondaryButton("Show me", Look));
        choices.AddChild(UiStyle.SecondaryButton("OK", () => _full.Visible = false));
        column.AddChild(choices);
        AddChild(_full);

        tool.AreaPicked += Refresh;
        game.AreaFull += OnFull;
        game.Site.AreasChanged += Refresh;
        game.Site.Changed += Refresh;
    }

    public override void _ExitTree()
    {
        _tool.AreaPicked -= Refresh;
        _game.AreaFull -= OnFull;
        _game.Site.AreasChanged -= Refresh;
        _game.Site.Changed -= Refresh;
    }

    private static Label Wrapped(string text)
    {
        var label = UiStyle.Body(text, muted: true, size: 14);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(330, 0);
        return label;
    }

    private PlacedArea? Selected => _tool.SelectedArea >= 0 && _tool.SelectedArea < _game.Site.Areas.Count ? _game.Site.Areas[_tool.SelectedArea] : null;

    private void Change(int by)
    {
        if (Selected is { } area)
        {
            _game.SetAreaLimit(_tool.SelectedArea, area.Limit + by);
        }
    }

    private void Suggest()
    {
        if (Selected is { } area)
        {
            _game.SetAreaLimit(_tool.SelectedArea, CrowdAreas.Suggested(area.InsideMetres));
        }
    }

    private void TakeDown()
    {
        if (Selected is { } area)
        {
            var inside = new TilePoint(area.Rect.X + 1, area.Rect.Y + 1);
            _tool.SelectArea(-1);
            _game.Remove(inside);
        }
    }

    /// <summary>Opens the card for setting an area's limit. For pictures and the launch test.</summary>
    public void Open(int area) => _tool.SelectArea(area);

    private void Refresh()
    {
        _since = 0;
        if (Selected is not { } area || !_game.CanBuild)
        {
            _editor.Visible = false;
            return;
        }

        var metres = area.InsideMetres;
        var index = _tool.SelectedArea;
        _editor.Visible = true;
        _kind.Text = CrowdAreas.Name(area.Kind);
        _size.Text = $"{area.Rect.Width * FieldMap.TileMetres:0.#} m × {area.Rect.Height * FieldMap.TileMetres:0.#} m · {metres:0} m² inside";
        _limit.Text = $"{area.Limit:N0}";
        var feel = CrowdAreas.Feel(area.Limit, metres);
        _feel.Text = feel;
        _feel.AddThemeColorOverride("font_color", feel switch
        {
            "Crush risk" => DensityView.CrushRed,
            "Busy" => new Color("#A35F00"),
            _ => UiStyle.FirstAidGreen,
        });
        _advice.Text = $"Under {CrowdAreas.ComfortablePerMetre:0} people a square metre stays comfortable: the game suggests {CrowdAreas.Suggested(metres):N0} for this size. "
            + $"You can let in more, but past {CrowdAreas.CrushAt(metres):N0} it is a crush waiting to happen.";
        var manned = _game.GapsManned(index);
        _gaps.Text = $"{area.Gaps.Count} {(area.Gaps.Count == 1 ? "gap" : "gaps")}, {manned} with security by {(area.Gaps.Count == 1 ? "it" : "them")}. "
            + "Click the barrier to open or close a gap. Post one security by each gap to count people in and close it when the area is full; with nobody there, the limit is not kept."
            + (area.Kind == AreaKind.Vip ? " Only VIP guests go in." : "");
        _takeDown.Text = $"Take it down (£{_game.Site.RefundAt(new TilePoint(area.Rect.X + 1, area.Rect.Y + 1)):N0} back)";
    }

    private void OnFull(int area)
    {
        if (area < 0 || area >= _game.Site.Areas.Count)
        {
            return;
        }

        _fullArea = area;
        _fullFor = FullCardSeconds;
        _full.Visible = true;
        FillFull();
    }

    private void FillFull()
    {
        if (_fullArea < 0 || _fullArea >= _game.Site.Areas.Count)
        {
            _full.Visible = false;
            return;
        }

        var a = _game.Site.Areas[_fullArea];
        var crowd = _game.Crowd;
        var waiting = crowd.WaitingAt(_fullArea);
        _fullHeading.Text = $"{CrowdAreas.Name(a.Kind)} is full";
        _fullBody.Text = $"{crowd.InArea(_fullArea):N0} of {a.Limit:N0} in. Security on the gaps have closed them and let one in as one comes out, so nobody is pushed in from behind."
            + (waiting > 0 ? $" {waiting:N0} waiting by the gaps." : "");
    }

    private void LetInMore()
    {
        if (_fullArea >= 0 && _fullArea < _game.Site.Areas.Count)
        {
            _game.SetAreaLimit(_fullArea, _game.Site.Areas[_fullArea].Limit + MoreAtOnce);
        }

        _full.Visible = false;
    }

    private void Look()
    {
        if (_fullArea >= 0 && _fullArea < _game.Site.Areas.Count)
        {
            var r = _game.Site.Areas[_fullArea].Rect;
            ShowMe?.Invoke((r.X + (r.Width / 2.0)) * FieldMap.TileMetres, (r.Y + (r.Height / 2.0)) * FieldMap.TileMetres);
        }
    }

    public override void _Process(double delta)
    {
        var screen = GetViewportRect().Size;
        _editor.Position = new Vector2(screen.X - 22 - _editor.Size.X, UiStyle.TopClear + 8);
        _full.Position = new Vector2(screen.X - 22 - _full.Size.X, TopInset + 8);
        _since += delta;
        if (_full.Visible)
        {
            _fullFor -= delta;
            if (_fullFor <= 0 || _game.Phase != Phase.Day)
            {
                _full.Visible = false;
            }
        }

        if (_since > 0.5)
        {
            if (_editor.Visible)
            {
                Refresh();
            }

            if (_full.Visible)
            {
                FillFull();
            }

            _since = 0;
        }
    }
}
