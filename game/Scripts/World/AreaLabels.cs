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
        foreach (var label in _labels)
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
