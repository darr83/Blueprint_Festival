using System.Linq;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.World;
using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// Draws how people get there: the white lines of the parking bays, a car in every bay that is
/// taken, the bus stop on the lane, and the shuttle bus that pulls in every quarter of an hour.
/// </summary>
public partial class ParkingView : Node3D
{
    private const float T = (float)FieldMap.TileMetres;

    // Car colours: none of the red, amber and green kept for safety.
    private static readonly Color[] Colours =
    {
        new("#F2F0E6"), new("#D5DADF"), new("#9AA7B4"), new("#56657A"), new("#4A8FCB"), new("#35629A"), new("#5CC3CF"), new("#8A76A8"), new("#E3D2AE"), new("#F2F0E6"), new("#3A4654"),
    };

    private FestivalGame _game = null!;
    private MultiMeshInstance3D _cars = null!;
    private Node3D _bus = null!;
    private float _stopX;
    private float _busZ;
    private double _since = 1;
    private int _drawn = -1;

    /// <summary>How many cars are drawn.</summary>
    public int CarCount => Mathf.Max(0, _drawn);

    /// <summary>True if the map has a bus stop drawn on the lane.</summary>
    public bool HasBusStop { get; private set; }

    /// <summary>True while the shuttle bus is in sight.</summary>
    public bool BusInSight => _bus.Visible;

    public void Initialise(FestivalGame game)
    {
        _game = game;
        var map = game.Field;
        _cars = new MultiMeshInstance3D
        {
            Name = "Cars",
            Multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = CarMesh() },
            MaterialOverride = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 0.45f, Metallic = 0.2f },
        };
        AddChild(_cars);
        _bus = BusNode();
        _bus.Visible = false;
        AddChild(_bus);

        var bays = map.ParkingBays();
        if (bays.Count > 0)
        {
            // One white line down the side of every bay.
            var lines = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                Mesh = new PlaneMesh { Size = new Vector2(0.12f, FieldMap.BayLong * T) },
                InstanceCount = bays.Count * 2,
            };
            for (var i = 0; i < bays.Count; i++)
            {
                var (x, z) = FieldMap.CentreOf(bays[i]);
                var half = FieldMap.BayWide * T / 2;
                lines.SetInstanceTransform(i * 2, new Transform3D(Basis.Identity, new Vector3((float)x - half, 0.03f, (float)z)));
                lines.SetInstanceTransform((i * 2) + 1, new Transform3D(Basis.Identity, new Vector3((float)x + half, 0.03f, (float)z)));
            }

            AddChild(new MultiMeshInstance3D
            {
                Name = "Bay lines",
                Multimesh = lines,
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#F4F1E4"), Roughness = 1 },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }

        if (map.BusStop is { } stop)
        {
            HasBusStop = true;
            _stopX = (stop.X + 0.5f) * T;
            var laneZ = map.Lane.Y * T;
            _busZ = laneZ + 1.7f;
            BuildStop(_stopX, laneZ - 1.3f);
        }

        Refresh();
    }

    private void Part(Node3D on, Vector3 centre, Vector3 size, Color colour)
    {
        on.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            Position = centre,
            MaterialOverride = new StandardMaterial3D { AlbedoColor = colour, Roughness = 0.8f },
        });
    }

    /// <summary>A shelter with a bench and a flag on a pole, on the verge north of the lane.</summary>
    private void BuildStop(float x, float z)
    {
        var stop = new Node3D { Name = "Bus stop", Position = new Vector3(x, 0, z) };
        AddChild(stop);
        var frame = new Color("#3E4A59");
        var pink = new Color("#E66A9A");
        Part(stop, new Vector3(0, 0.02f, 0.9f), new Vector3(9, 0.04f, 1.2f), new Color("#C9C3B5"));
        foreach (var px in new[] { -2f, 2f })
        {
            Part(stop, new Vector3(px, 1.2f, -0.6f), new Vector3(0.12f, 2.4f, 0.12f), frame);
            Part(stop, new Vector3(px, 1.2f, 0.5f), new Vector3(0.12f, 2.4f, 0.12f), frame);
        }

        Part(stop, new Vector3(0, 1.3f, -0.62f), new Vector3(4, 1.9f, 0.06f), new Color("#BFD9E6"));
        Part(stop, new Vector3(0, 2.45f, 0), new Vector3(4.5f, 0.12f, 1.7f), pink);
        Part(stop, new Vector3(0, 0.45f, -0.3f), new Vector3(3, 0.1f, 0.45f), new Color("#8A6B45"));
        Part(stop, new Vector3(3.6f, 1.5f, 0.4f), new Vector3(0.1f, 3, 0.1f), frame);
        Part(stop, new Vector3(4.15f, 2.7f, 0.4f), new Vector3(1.0f, 0.6f, 0.08f), new Color("#F2C230"));
        stop.AddChild(new Label3D
        {
            Text = "BUS",
            Position = new Vector3(4.15f, 2.7f, 0.46f),
            PixelSize = 0.012f,
            FontSize = 32,
            Modulate = new Color("#1F2933"),
            OutlineSize = 0,
        });
    }

    /// <summary>A single-decker in cream and pink. It is built pointing west and turned to drive east, on the left of the lane.</summary>
    private Node3D BusNode()
    {
        var bus = new Node3D { Name = "Bus", RotationDegrees = new Vector3(0, 180, 0) };
        Part(bus, new Vector3(0, 1.0f, 0), new Vector3(11, 1.1f, 2.5f), new Color("#E66A9A"));
        Part(bus, new Vector3(0, 2.1f, 0), new Vector3(11, 1.1f, 2.5f), new Color("#F2F0E6"));
        Part(bus, new Vector3(0, 2.72f, 0), new Vector3(10.6f, 0.14f, 2.3f), new Color("#F2F0E6"));
        Part(bus, new Vector3(0, 2.1f, 0), new Vector3(10.2f, 0.75f, 2.54f), new Color("#27333F"));
        Part(bus, new Vector3(-5.5f, 2.0f, 0), new Vector3(0.06f, 0.95f, 2.2f), new Color("#27333F"));
        Part(bus, new Vector3(-5.52f, 2.62f, 0), new Vector3(0.06f, 0.26f, 1.5f), new Color("#F2C230"));
        foreach (var wx in new[] { -3.6f, 3.4f })
        {
            foreach (var wz in new[] { -1.2f, 1.2f })
            {
                bus.AddChild(new MeshInstance3D
                {
                    Mesh = new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 0.3f },
                    Position = new Vector3(wx, 0.5f, wz),
                    RotationDegrees = new Vector3(90, 0, 0),
                    MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#1F2933"), Roughness = 0.9f },
                });
            }
        }

        return bus;
    }

    /// <summary>A small car pointing north: a body in the car's colour and a darker cabin with windows.</summary>
    private static ArrayMesh CarMesh()
    {
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        void Block(Vector3 centre, Vector3 size, Color shade)
        {
            var h = size / 2;
            var corners = new Vector3[8];
            for (var i = 0; i < 8; i++)
            {
                corners[i] = centre + new Vector3((i & 1) == 0 ? -h.X : h.X, (i & 2) == 0 ? -h.Y : h.Y, (i & 4) == 0 ? -h.Z : h.Z);
            }

            foreach (var (face, normal) in new[]
            {
                (new[] { 0, 2, 3, 1 }, Vector3.Forward), (new[] { 4, 5, 7, 6 }, Vector3.Back), (new[] { 0, 4, 6, 2 }, Vector3.Left),
                (new[] { 1, 3, 7, 5 }, Vector3.Right), (new[] { 2, 6, 7, 3 }, Vector3.Up), (new[] { 0, 1, 5, 4 }, Vector3.Down),
            })
            {
                foreach (var corner in new[] { face[0], face[2], face[1], face[0], face[3], face[2] })
                {
                    tool.SetColor(shade);
                    tool.SetNormal(normal);
                    tool.AddVertex(corners[corner]);
                }
            }
        }

        var white = new Color(1, 1, 1);
        Block(new Vector3(0, 0.62f, 0), new Vector3(1.8f, 0.62f, 4.2f), white);
        Block(new Vector3(0, 1.18f, 0.25f), new Vector3(1.62f, 0.5f, 2.2f), new Color(0.82f, 0.82f, 0.82f));
        Block(new Vector3(0, 1.2f, 0.25f), new Vector3(1.66f, 0.34f, 1.9f), new Color(0.16f, 0.2f, 0.24f));
        Block(new Vector3(0, 1.2f, 0.25f), new Vector3(1.4f, 0.34f, 2.24f), new Color(0.16f, 0.2f, 0.24f));
        foreach (var wz in new[] { -1.35f, 1.35f })
        {
            Block(new Vector3(0, 0.33f, wz), new Vector3(1.86f, 0.66f, 0.66f), new Color(0.1f, 0.1f, 0.11f));
        }

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

        MoveBus();
    }

    /// <summary>
    /// The bus waits at the stop for the first two minutes of each quarter hour, pulls away east,
    /// and the next one comes in from the west in the last minute and a half.
    /// </summary>
    private void MoveBus()
    {
        if (!HasBusStop || _game.Phase != Phase.Day || _game.BusesHired == 0 || _game.Crowd.ComingBy(Travel.Bus) == 0)
        {
            _bus.Visible = false;
            return;
        }

        var minutes = _game.Now.TimeOfDay.TotalMinutes % Crowd.BusEvery.TotalMinutes;
        const float run = 90;
        float? along = minutes < 2 ? 0 : minutes < 3.5 ? (float)((minutes - 2) / 1.5) : minutes >= 13.5 ? -(float)((15 - minutes) / 1.5) : null;
        _bus.Visible = along is not null;
        if (along is { } a)
        {
            // Ease in to the stop and out again.
            _bus.Position = new Vector3(_stopX + (Mathf.Sign(a) * a * a * run), 0, _busZ);
        }
    }

    /// <summary>Redraws the parked cars now.</summary>
    public void Refresh()
    {
        var cars = _game.Crowd.CarsParked.ToList();
        if (cars.Count == _drawn)
        {
            return;
        }

        _drawn = cars.Count;
        var mesh = _cars.Multimesh;
        mesh.InstanceCount = cars.Count;
        for (var i = 0; i < cars.Count; i++)
        {
            var bay = cars[i];
            var (x, z) = FieldMap.CentreOf(bay);
            var mix = (bay.X * 7919) + (bay.Y * 104729);
            // Parked nose in or backed in, and never quite straight.
            var turn = ((mix / 7) % 2 == 0 ? 0 : Mathf.Pi) + ((((mix / 3) % 9) - 4) * 0.012f);
            var size = 0.92f + ((mix % 5) * 0.03f);
            var basis = new Basis(Vector3.Up, turn).Scaled(new Vector3(size, size, size));
            mesh.SetInstanceTransform(i, new Transform3D(basis, new Vector3((float)x, 0.02f, (float)z)));
            mesh.SetInstanceColor(i, Colours[(mix / 11) % Colours.Length].SrgbToLinear());
        }
    }
}
