using System.Linq;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// Draws the camping field next to the showground: a farm gate across the gateway while the
/// field is not in use, and a tent for every party of campers once they are through the gate.
/// </summary>
public partial class CampView : Node3D
{
    private const float T = (float)FieldMap.TileMetres;

    // Tent colours: bright, but none of the red, amber and green kept for safety.
    private static readonly Color[] Colours =
    {
        new("#3E7CB1"), new("#E8C547"), new("#8E6BBF"), new("#E66A9A"), new("#4FB3BF"), new("#F2F0E6"), new("#5B6C8F"), new("#F08A4B"),
    };

    private FestivalGame _game = null!;
    private MultiMeshInstance3D _tents = null!;
    private Node3D _gate = null!;
    private double _since = 1;
    private int _drawn = -1;

    /// <summary>How many tents are drawn.</summary>
    public int TentCount => Mathf.Max(0, _drawn);

    /// <summary>True if the farm gate is shut across the gateway.</summary>
    public bool GateShut => _gate.Visible;

    public void Initialise(FestivalGame game)
    {
        _game = game;
        var map = game.Field;
        _gate = new Node3D { Name = "FarmGate", Visible = false };
        AddChild(_gate);
        _tents = new MultiMeshInstance3D
        {
            Name = "Tents",
            Multimesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseColors = true,
                Mesh = TentMesh(),
            },
            MaterialOverride = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 0.9f, CullMode = BaseMaterial3D.CullModeEnum.Disabled },
        };
        AddChild(_tents);
        game.Site.CampZonesChanged += DrawFireLanes;
        DrawFireLanes();
        if (!map.HasCamp)
        {
            return;
        }

        // A five-bar gate: two posts and five rails across the gateway in the hedge.
        var gap = map.CampGap;
        var x = (gap.X + (gap.Width / 2f)) * T;
        var z0 = gap.Y * T;
        var length = gap.Height * T;
        var wood = new StandardMaterial3D { AlbedoColor = new Color("#8A6B45"), Roughness = 0.95f };
        void Bar(Vector3 centre, Vector3 size) =>
            _gate.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, Position = centre, MaterialOverride = wood });
        Bar(new Vector3(x, 0.7f, z0 + 0.1f), new Vector3(0.2f, 1.4f, 0.2f));
        Bar(new Vector3(x, 0.7f, z0 + length - 0.1f), new Vector3(0.2f, 1.4f, 0.2f));
        Bar(new Vector3(x, 0.7f, z0 + (length / 2)), new Vector3(0.2f, 1.4f, 0.2f));
        for (var rail = 0; rail < 5; rail++)
        {
            Bar(new Vector3(x, 0.25f + (rail * 0.24f), z0 + (length / 2)), new Vector3(0.08f, 0.1f, length));
        }

        Refresh();
    }

    private MeshInstance3D? _fireLanes;

    /// <summary>How many straight runs of fire lane are mown.</summary>
    public int FireLaneRuns { get; private set; }

    public override void _ExitTree() => _game.Site.CampZonesChanged -= DrawFireLanes;

    /// <summary>Fire lanes are mown short, a paler stripe through the camping field, so they show whatever tool is in hand.</summary>
    private void DrawFireLanes()
    {
        _fireLanes?.QueueFree();
        var tiles = new System.Collections.Generic.HashSet<TilePoint>(_game.Site.CampZones.Where(z => z.Value == CampZone.FireLane).Select(z => z.Key));
        var runs = SiteLayout.RunsOf(tiles);
        FireLaneRuns = runs.Count;
        var b = new MeshBuilder();
        foreach (var (x, y, length, vertical) in runs)
        {
            var rect = vertical ? new TileRect(x, y, 1, length) : new TileRect(x, y, length, 1);
            b.Box(SiteView.Middle(rect) + new Vector3(0, 0.012f, 0), new Vector3(rect.Width * T, 0.01f, rect.Height * T), new Color("#A8D47E"));
        }

        _fireLanes = b.IsEmpty ? new MeshInstance3D { Name = "FireLanes" } : b.BuildNode("FireLanes");
        _fireLanes.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        AddChild(_fireLanes);
    }

    /// <summary>
    /// A ridge tent: two sloping sides in the tent's colour, a shaded end at the back, and a
    /// shaded front with a dark doorway, so tents read as tents from far off.
    /// </summary>
    private static ArrayMesh TentMesh()
    {
        const float w = 1.1f;    // half the width
        const float h = 1.3f;    // height of the ridge
        const float l = 1.3f;    // half the length
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        void Face(Color shade, params Vector3[] corners)
        {
            var normal = (corners[2] - corners[0]).Cross(corners[1] - corners[0]).Normalized();
            for (var i = 1; i + 1 < corners.Length; i++)
            {
                foreach (var corner in new[] { corners[0], corners[i], corners[i + 1] })
                {
                    tool.SetColor(shade);
                    tool.SetNormal(normal);
                    tool.AddVertex(corner);
                }
            }
        }

        var (ridgeBack, ridgeFront) = (new Vector3(0, h, -l), new Vector3(0, h, l));
        var (leftBack, leftFront) = (new Vector3(-w, 0, -l), new Vector3(-w, 0, l));
        var (rightBack, rightFront) = (new Vector3(w, 0, -l), new Vector3(w, 0, l));
        var light = new Color(1, 1, 1);
        var shaded = new Color(0.72f, 0.72f, 0.72f);
        var dark = new Color(0.2f, 0.2f, 0.22f);
        Face(light, leftBack, leftFront, ridgeFront, ridgeBack);
        Face(light.Darkened(0.12f), rightFront, rightBack, ridgeBack, ridgeFront);
        Face(shaded, rightBack, leftBack, ridgeBack);
        Face(shaded, leftFront, rightFront, ridgeFront);

        // The doorway, a hair in front of the front end.
        const float front = l + 0.01f;
        Face(dark, new Vector3(-0.42f, 0, front), new Vector3(0.42f, 0, front), new Vector3(0, 0.95f, front));
        return tool.Commit();
    }

    public override void _Process(double delta)
    {
        _since += delta;
        if (_since >= 0.5)
        {
            _since = 0;
            Refresh();
        }
    }

    /// <summary>Redraws the tents and the gate now.</summary>
    public void Refresh()
    {
        _gate.Visible = _game.Field.HasCamp && !_game.CampingBooked;
        var tents = _game.Crowd.Tents.ToList();
        if (tents.Count == _drawn)
        {
            return;
        }

        _drawn = tents.Count;
        var mesh = _tents.Multimesh;
        mesh.InstanceCount = tents.Count;
        for (var i = 0; i < tents.Count; i++)
        {
            var (pitch, party) = tents[i];
            var (x, z) = FieldMap.CentreOf(pitch);
            // Each tent faces whichever way its campers liked and is a little bigger or smaller, the
            // same way every time, so the field looks pitched by hand and not laid out in rows.
            var turn = (((pitch.X * 7919) + (pitch.Y * 104729) + (party * 37)) % 360) * Mathf.Pi / 180f;
            var size = 0.7f + (((party * 13) % 25) * 0.008f);
            var basis = new Basis(Vector3.Up, turn).Scaled(new Vector3(size, size, size));
            mesh.SetInstanceTransform(i, new Transform3D(basis, new Vector3((float)x, 0.02f, (float)z)));
            mesh.SetInstanceColor(i, Colours[(party * 7) % Colours.Length].SrgbToLinear());
        }
    }
}
