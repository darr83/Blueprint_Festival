using System.Collections.Generic;
using System.Linq;
using BlueprintFestival.Audio;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.Sound;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.UI;

/// <summary>
/// The box for writing on a sign. It opens when a sign goes up, and when one is clicked with
/// nothing picked on the build bar: a line to type in for each line or finger, which way each
/// finger points, and the colour. The sign changes as you type.
/// </summary>
public partial class SignPanel : PanelContainer
{
    private FestivalGame _game = null!;
    private BuildTool _tool = null!;
    private VBoxContainer _box = null!;
    private readonly List<LineEdit> _lines = new();
    private readonly List<Button> _left = new();
    private readonly List<Button> _right = new();
    private readonly List<Button> _swatches = new();
    private int _colour;
    private int _points;

    /// <summary>A tile of the sign being written on, or null when the box is shut.</summary>
    public TilePoint? Sign { get; private set; }

    /// <summary>How many lines can be typed on the sign showing.</summary>
    public int Rows => _lines.Count;

    public void Initialise(FestivalGame game, BuildTool tool)
    {
        _game = game;
        _tool = tool;
        Theme = UiStyle.Theme;
        CustomMinimumSize = new Vector2(430, 0);
        AddThemeStyleboxOverride("panel", UiStyle.Card(margin: 22, radius: 12));
        _box = new VBoxContainer();
        _box.AddThemeConstantOverride("separation", 10);
        AddChild(_box);
        Visible = false;
        SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft, LayoutPresetMode.Minsize, 22);
        Position = new Vector2(22, UiStyle.TopClear + 8);
        game.Site.Changed += OnSiteChanged;
        tool.SignPlaced += OnSignPlaced;
    }

    public override void _ExitTree()
    {
        _game.Site.Changed -= OnSiteChanged;
        _tool.SignPlaced -= OnSignPlaced;
    }

    private void OnSignPlaced(TilePoint at) => Open(at);

    private void OnSiteChanged()
    {
        // Taken down while the box was open.
        if (Visible && (Sign is not { } at || _game.Site.SignAt(at) is null))
        {
            Close();
        }
    }

    /// <summary>Opens the box on the sign standing on a tile. True if there is one.</summary>
    public bool Open(TilePoint at)
    {
        if (_game.Site.SignAt(at) is not { } sign)
        {
            return false;
        }

        Sign = new TilePoint(sign.X, sign.Y);
        _colour = sign.Colour;
        _points = sign.Points;
        Build(sign);
        Visible = true;
        ResetSize();
        if (_lines.Count > 0)
        {
            _lines[0].GrabFocus();
            _lines[0].SelectAll();
        }

        return true;
    }

    public void Close()
    {
        Visible = false;
        Sign = null;
        foreach (var line in _lines)
        {
            line.ReleaseFocus();
        }
    }

    /// <summary>Types words onto a line of the sign showing, as the player would.</summary>
    public void Type(int line, string words)
    {
        if (line >= 0 && line < _lines.Count)
        {
            _lines[line].Text = words;
            Write();
        }
    }

    /// <summary>Paints the sign showing one of the sign colours.</summary>
    public void Paint(int colour)
    {
        _colour = colour;
        Write();
    }

    /// <summary>Points a finger of the post showing to the right, or to the left.</summary>
    public void Point(int finger, bool right)
    {
        _points = right ? _points | (1 << finger) : _points & ~(1 << finger);
        Write();
    }

    private void Build(PlacedSign sign)
    {
        foreach (var child in _box.GetChildren())
        {
            _box.RemoveChild(child);
            child.QueueFree();
        }

        _lines.Clear();
        _left.Clear();
        _right.Clear();
        _swatches.Clear();
        var info = Signs.Of(sign.Kind);
        _box.AddChild(UiStyle.Caps($"{info.Name} · £{info.Cost}", pink: true));
        _box.AddChild(UiStyle.Heading("What should it say?", 26));
        _box.AddChild(UiStyle.Body(sign.Kind switch
        {
            SignKind.FingerPost => "Up to three fingers. Leave one empty to leave it off.",
            SignKind.SandwichBoard => "Up to three lines. The top one is the big one.",
            SignKind.MapBoard => "A title for the top. The plan draws itself from what you build.",
            _ => "One line, to hang over the way in or anywhere you like.",
        }, muted: true, size: 14));
        for (var i = 0; i < info.Lines; i++)
        {
            var finger = i;
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            var line = new LineEdit { Text = sign.Line(i), MaxLength = info.LineLength, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 42), Name = $"Line{i}" };
            line.TextChanged += _ => Write();
            line.TextSubmitted += _ => Close();
            row.AddChild(line);
            _lines.Add(line);
            if (sign.Kind == SignKind.FingerPost)
            {
                var left = UiStyle.ChipButton("◀ Left", () => Point(finger, false), Sounds.Tab);
                var right = UiStyle.ChipButton("Right ▶", () => Point(finger, true), Sounds.Tab);
                left.ToggleMode = true;
                right.ToggleMode = true;
                row.AddChild(left);
                row.AddChild(right);
                _left.Add(left);
                _right.Add(right);
            }

            _box.AddChild(row);
        }

        if (sign.Kind != SignKind.MapBoard)
        {
            _box.AddChild(UiStyle.Caps("Colour"));
            var colours = new HBoxContainer();
            colours.AddThemeConstantOverride("separation", 8);
            for (var i = 0; i < SiteProps.SignColours.Length; i++)
            {
                var colour = i;
                var swatch = new Button { CustomMinimumSize = new Vector2(38, 38), ToggleMode = true, FocusMode = FocusModeEnum.None };
                foreach (var state in new[] { "normal", "hover", "pressed", "focus" })
                {
                    swatch.AddThemeStyleboxOverride(state, UiStyle.Box(SiteProps.SignColours[i], 19, UiStyle.NightInk, 0, state == "pressed" ? 4 : 1));
                }

                swatch.Pressed += () =>
                {
                    SoundBoard.Cue(Sounds.Tab);
                    Paint(colour);
                };
                colours.AddChild(swatch);
                _swatches.Add(swatch);
            }

            _box.AddChild(colours);
        }

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        buttons.AddThemeConstantOverride("separation", 8);
        buttons.AddChild(UiStyle.SecondaryButton("Take it down", TakeDown, Sounds.TakeDown));
        buttons.AddChild(UiStyle.Button("Done", Close));
        _box.AddChild(buttons);
        Show(sign);
    }

    private void Show(PlacedSign sign)
    {
        for (var i = 0; i < _left.Count; i++)
        {
            _left[i].SetPressedNoSignal(!sign.PointsRight(i));
            _right[i].SetPressedNoSignal(sign.PointsRight(i));
        }

        for (var i = 0; i < _swatches.Count; i++)
        {
            _swatches[i].SetPressedNoSignal(i == sign.Colour);
        }
    }

    private void Write()
    {
        if (Sign is not { } at)
        {
            return;
        }

        _game.WriteSign(at, _lines.Select(l => l.Text), _colour, _points);
        if (_game.Site.SignAt(at) is { } sign)
        {
            Show(sign);
        }
    }

    private void TakeDown()
    {
        if (Sign is { } at)
        {
            _game.Remove(at);
        }

        Close();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } && Visible)
        {
            Close();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } || _tool.Mode != BuildMode.None
            || _game.Phase is not (Phase.Build or Phase.Day))
        {
            return;
        }

        if (_tool.TileUnderMouse() is { } tile)
        {
            if (Open(tile))
            {
                SoundBoard.Cue(Sounds.Tab);
                GetViewport().SetInputAsHandled();
            }
            else if (Visible)
            {
                Close();
            }
        }
    }
}
