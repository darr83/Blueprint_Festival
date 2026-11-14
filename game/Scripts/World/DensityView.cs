using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.World;
using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// The crowd view: the field cut into two-metre squares, each tinted by how many people stand in
/// every square metre of it. Green is comfortable, amber is busy and red is a crush risk; these
/// three colours are kept for safety and used nowhere else. The squares come from the rules,
/// which measure the crowd again every minute.
/// </summary>
public partial class DensityView : Node3D
{
    public static readonly Color Comfortable = new("#3FA657");
    public static readonly Color BusyAmber = new("#F0A81E");
    public static readonly Color CrushRed = new("#DC3B34");

    private FestivalGame _game = null!;
    private MeshInstance3D? _squares;
    private Label3D _label = null!;
    private StandardMaterial3D _material = null!;
    private double _sinceDrawn = 1;
    private bool _showing;

    /// <summary>Whether the crowd view is switched on.</summary>
    public bool Showing
    {
        get => _showing;
        set
        {
            _showing = value;
            Visible = value;
            if (value)
            {
                Redraw();
            }
        }
    }

    /// <summary>How many squares are tinted amber or red right now.</summary>
    public int Tinted { get; private set; }

    /// <summary>What the label over the busiest square says, or nothing if nowhere is busy.</summary>
    public string LabelText => _label.Visible ? _label.Text : "";

    public static Color ColourOf(Crowding status) => status switch
    {
        Crowding.CrushRisk => CrushRed,
        Crowding.Busy => BusyAmber,
        _ => Comfortable,
    };

    /// <summary>The word for how crowded somewhere is.</summary>
    public static string Word(Crowding status) => status switch
    {
        Crowding.CrushRisk => "Crush risk",
        Crowding.Busy => "Busy",
        _ => "Comfortable",
    };

    public void Initialise(FestivalGame game)
    {
        _game = game;
        _material = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            NoDepthTest = true,
            RenderPriority = 5,
        };
        _label = new Label3D
        {
            FontSize = 176,
            PixelSize = 0.00028f,
            FixedSize = true,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            RenderPriority = 10,
            OutlineRenderPriority = 9,
            OutlineModulate = new Color("#1D2B36"),
            OutlineSize = 36,
            Visible = false,
        };
        AddChild(_label);
        Visible = false;
    }

    public override void _Process(double delta)
    {
        _sinceDrawn += delta;
        if (_showing && _sinceDrawn > 0.4)
        {
            Redraw();
        }
    }

    /// <summary>Draws the squares again from the rules' latest measure.</summary>
    public void Redraw()
    {
        _sinceDrawn = 0;
        _squares?.QueueFree();
        var density = _game.Density;
        var field = _game.Field.Field;
        var b = new MeshBuilder();
        const float side = (float)DensityMap.SquareMetres;
        Tinted = 0;
        for (var sy = 0; sy < density.Down; sy++)
        {
            for (var sx = 0; sx < density.Across; sx++)
            {
                var perMetre = density.In(sx, sy);
                var status = DensityMap.StatusOf(perMetre);
                var (x, y) = DensityMap.CentreOf(sx, sy);
                if (status == Crowding.Comfortable && !field.Contains(FieldMap.TileAt(x, y)))
                {
                    // Outside the field only the squares that matter are drawn.
                    continue;
                }

                var colour = ColourOf(status);
                colour.A = status switch { Crowding.CrushRisk => 0.5f, Crowding.Busy => 0.38f, _ => 0.14f };
                b.Box(new Vector3((float)x, 0.05f, (float)y), new Vector3(side - 0.08f, 0.02f, side - 0.08f), colour);
                if (status != Crowding.Comfortable)
                {
                    Tinted++;
                }
            }
        }

        _squares = b.BuildNode("Squares");
        _squares.MaterialOverride = _material;
        _squares.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        AddChild(_squares);

        var worst = DensityMap.StatusOf(density.Worst);
        _label.Visible = worst != Crowding.Comfortable;
        if (_label.Visible)
        {
            _label.Text = $"{Word(worst)}  {density.Worst:0.0} per m²";
            _label.Position = new Vector3((float)density.WorstAt.X, 2.8f, (float)density.WorstAt.Y);
        }
    }
}
