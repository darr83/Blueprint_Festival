using System;
using System.Collections.Generic;
using BlueprintFestival.Simulation.World;
using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// The crowd view: the field cut into two-metre squares, each tinted by how many people stand in
/// every square metre of it. Green is comfortable, amber is busy and red is a crush risk; these
/// three colours are kept for safety and used nowhere else.
/// </summary>
public partial class DensityView : Node3D
{
    /// <summary>The side of one measured square, in metres.</summary>
    public const double Square = 2.0;

    /// <summary>People per square metre at which a square turns amber.</summary>
    public const double Busy = 2.0;

    /// <summary>People per square metre at which a square turns red.</summary>
    public const double Crush = 4.0;

    public static readonly Color Comfortable = new("#3FA657");
    public static readonly Color BusyAmber = new("#F0A81E");
    public static readonly Color CrushRed = new("#DC3B34");

    private MeshInstance3D? _squares;
    private readonly List<Label3D> _labels = new();

    /// <summary>The busiest square found the last time the view was drawn, in people per square metre.</summary>
    public double Worst { get; private set; }

    /// <summary>Where the busiest square is, in metres.</summary>
    public Vector2 WorstAt { get; private set; }

    /// <summary>Draws the view for people standing at these places (in metres) on a field.</summary>
    public void Draw(IEnumerable<(double X, double Y)> people, TileRect field)
    {
        _squares?.QueueFree();
        foreach (var label in _labels)
        {
            label.QueueFree();
        }

        _labels.Clear();
        const double t = FieldMap.TileMetres;
        var left = field.X * t;
        var top = field.Y * t;
        var across = (int)Math.Ceiling(field.Width * t / Square);
        var down = (int)Math.Ceiling(field.Height * t / Square);
        var counts = new int[across, down];
        foreach (var (x, y) in people)
        {
            var cx = (int)Math.Floor((x - left) / Square);
            var cy = (int)Math.Floor((y - top) / Square);
            if (cx >= 0 && cy >= 0 && cx < across && cy < down)
            {
                counts[cx, cy]++;
            }
        }

        var b = new MeshBuilder();
        Worst = 0;
        for (var cx = 0; cx < across; cx++)
        {
            for (var cy = 0; cy < down; cy++)
            {
                var perMetre = counts[cx, cy] / (Square * Square);
                var colour = perMetre >= Crush ? CrushRed : perMetre >= Busy ? BusyAmber : Comfortable;
                colour.A = perMetre >= Crush ? 0.5f : perMetre >= Busy ? 0.38f : 0.14f;
                var centre = new Vector3((float)(left + ((cx + 0.5) * Square)), 0.05f, (float)(top + ((cy + 0.5) * Square)));
                b.Box(centre, new Vector3((float)Square - 0.08f, 0.02f, (float)Square - 0.08f), colour);
                if (perMetre > Worst)
                {
                    Worst = perMetre;
                    WorstAt = new Vector2(centre.X, centre.Z);
                }
            }
        }

        _squares = b.BuildNode("Squares");
        _squares.MaterialOverride = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            NoDepthTest = true,
            RenderPriority = 5,
        };
        _squares.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        AddChild(_squares);
    }

    /// <summary>Writes a label over a place on the field, such as "Crush risk 4.8 per m²".</summary>
    public void Label(double x, double y, string text, int size = 44)
    {
        var label = new Label3D
        {
            Text = text,
            FontSize = size * 4,
            PixelSize = 0.0026f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            RenderPriority = 10,
            OutlineRenderPriority = 9,
            Modulate = Colors.White,
            OutlineModulate = new Color("#1D2B36"),
            OutlineSize = 36,
            FixedSize = true,
            Position = new Vector3((float)x, 2.6f, (float)y),
        };
        label.PixelSize = 0.00028f;
        _labels.Add(label);
        AddChild(label);
    }

    /// <summary>The word for a number of people per square metre.</summary>
    public static string Word(double perMetre) => perMetre >= Crush ? "Crush risk" : perMetre >= Busy ? "Busy" : "Comfortable";
}
