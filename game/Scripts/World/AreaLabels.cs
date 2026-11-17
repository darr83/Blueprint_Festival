using System.Collections.Generic;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// A label over each crowd area saying what it is and how full it is: "Front pen · 250 of 250 ·
/// Full". The labels show while areas are being drawn and while the crowd view is on.
/// </summary>
public partial class AreaLabels : Node3D
{
    private FestivalGame _game = null!;
    private BuildTool _tool = null!;
    private DensityView _density = null!;
    private readonly List<Label3D> _labels = new();
    private readonly List<Label3D> _standLabels = new();
    private double _since = 1;

    public void Initialise(FestivalGame game, BuildTool tool, DensityView density)
    {
        _game = game;
        _tool = tool;
        _density = density;
    }

    /// <summary>What each label says just now, for the launch test.</summary>
    public IEnumerable<string> Texts()
    {
        foreach (var label in System.Linq.Enumerable.Concat(_labels, _standLabels))
        {
            if (label.Visible)
            {
                yield return label.Text;
            }
        }
    }

    /// <summary>What a label over an area says: its name, and how many are in it out of how many it lets in.</summary>
    public static string TextFor(FestivalGame game, int area)
    {
        var a = game.Site.Areas[area];
        var name = CrowdAreas.Name(a.Kind);
        if (game.Phase != Phase.Day)
        {
            return $"{name} · lets in {a.Limit:N0}";
        }

        var crowd = game.Crowd;
        var text = $"{name} · {crowd.InArea(area):N0} of {a.Limit:N0}";
        if (!crowd.Kept(area))
        {
            return text + " · nobody on the gaps";
        }

        if (crowd.IsFull(area))
        {
            text += " · Full";
        }

        return crowd.WaitingAt(area) > 0 ? $"{text} · {crowd.WaitingAt(area):N0} waiting" : text;
    }

    /// <summary>What a label over a stand says: its name, and how many are on it out of its places.</summary>
    public static string StandText(FestivalGame game, int stand)
    {
        var s = game.Site.Stands[stand];
        var name = Stands.Name(s.Kind);
        var places = game.Crowd.Places(stand);
        if (game.Phase != Phase.Day)
        {
            return $"{name} · {places:N0} {(s.Kind == StandKind.Seated ? "seats" : "places")}";
        }

        var crowd = game.Crowd;
        var text = $"{name} · {crowd.OnStand(stand):N0} of {places:N0}";
        return !crowd.StandKept(stand) ? text + " · nobody on the steps" : crowd.StandFull(stand) ? text + " · Full" : text;
    }

    private Label3D NewLabel()
    {
        var label = new Label3D
        {
            FontSize = 150,
            PixelSize = 0.00026f,
            FixedSize = true,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            RenderPriority = 10,
            OutlineRenderPriority = 9,
            OutlineModulate = new Color("#26232B"),
            OutlineSize = 30,
            Visible = false,
        };
        AddChild(label);
        return label;
    }

    private void StandLabels(bool show)
    {
        var stands = _game.Site.Stands;
        while (_standLabels.Count < stands.Count)
        {
            _standLabels.Add(NewLabel());
        }

        for (var i = 0; i < _standLabels.Count; i++)
        {
            var label = _standLabels[i];
            label.Visible = show && i < stands.Count;
            if (!label.Visible)
            {
                continue;
            }

            var (x, y) = stands[i].Middle;
            label.Position = new Vector3((float)x, (float)Stands.TopOf(stands[i]) + 2.2f, (float)y);
            label.Text = StandText(_game, i);
            label.Modulate = _game.Phase == Phase.Day && _game.Crowd.StandFull(i) ? new Color("#FFD6E2") : Colors.White;
        }
    }

    public override void _Process(double delta)
    {
        _since += delta;
        if (_since < 0.4)
        {
            return;
        }

        _since = 0;
        var areas = _game.Site.Areas;
        var show = _tool.Mode == BuildMode.Area || _density.Showing;
        StandLabels(_tool.Mode is BuildMode.Area or BuildMode.Stand || _density.Showing);
        while (_labels.Count < areas.Count)
        {
            var label = new Label3D
            {
                FontSize = 150,
                PixelSize = 0.00026f,
                FixedSize = true,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                NoDepthTest = true,
                RenderPriority = 10,
                OutlineRenderPriority = 9,
                OutlineModulate = new Color("#26232B"),
                OutlineSize = 30,
                Visible = false,
            };
            AddChild(label);
            _labels.Add(label);
        }

        for (var i = 0; i < _labels.Count; i++)
        {
            var label = _labels[i];
            label.Visible = show && i < areas.Count;
            if (!label.Visible)
            {
                continue;
            }

            var r = areas[i].Rect;
            label.Position = new Vector3((float)((r.X + (r.Width / 2.0)) * FieldMap.TileMetres), 2.4f, (float)((r.Y + (r.Height / 2.0)) * FieldMap.TileMetres));
            label.Text = TextFor(_game, i);
            label.Modulate = _game.Phase == Phase.Day && _game.Crowd.IsFull(i) ? new Color("#FFD6E2") : Colors.White;
        }
    }
}
