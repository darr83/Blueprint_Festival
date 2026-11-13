using System;
using System.Collections.Generic;
using BlueprintFestival.Simulation.World;
using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// Draws the site before anything is built on it: the mown festival field, the hedge round it, the
/// lane and the track in, and the farmland beyond. One metre is one unit; tile X runs east along
/// world X and tile Y runs south along world Z.
/// </summary>
public partial class FieldView : Node3D
{
    private const float T = (float)FieldMap.TileMetres;
    private const float HedgeHeight = 1.5f;

    private static readonly Color Meadow = new("#5E9C4E");
    private static readonly Color MeadowLight = new("#69A758");
    private static readonly Color Verge = new("#578F49");
    private static readonly Color Countryside = new("#6FA060");
    private static readonly Color HedgeGreen = new("#3C7438");
    private static readonly Color Tarmac = new("#8B8D88");
    private static readonly Color Dirt = new("#B39A6C");
    private static readonly Color Wood = new("#8A6A45");
    private static readonly Color Trunk = new("#6B5137");

    /// <summary>Wheat, pasture, oilseed rape, ploughed earth, barley and darker pasture.</summary>
    private static readonly Color[] Crops =
    {
        new("#CDBB82"), new("#7CAB62"), new("#D3C162"), new("#A08768"), new("#BDB27E"), new("#6A9C57"),
    };

    private readonly Dictionary<Color, StandardMaterial3D> _materials = new();
    private FieldMap _map = null!;

    /// <summary>The name on the sign at the way in.</summary>
    public string SignText { get; private set; } = "";

    public int TreeCount { get; private set; }

    public void Initialise(FieldMap map, string festivalName)
    {
        _map = map;
        SignText = festivalName;
        var random = new Random(1707);

        BuildGround();
        BuildFieldGrass();
        BuildLane();
        BuildHedge(random);
        BuildTrees(random);
        BuildWayIn(festivalName);
    }

    private StandardMaterial3D Material(Color colour)
    {
        if (!_materials.TryGetValue(colour, out var material))
        {
            material = new StandardMaterial3D { AlbedoColor = colour, Roughness = 0.95f, MetallicSpecular = 0.1f };
            _materials[colour] = material;
        }

        return material;
    }

    /// <summary>A flat rectangle on the ground. X and Z are its north-west corner in metres.</summary>
    private void Flat(string name, float x, float z, float width, float depth, Color colour, float lift)
    {
        AddChild(new MeshInstance3D
        {
            Name = name,
            Mesh = new PlaneMesh { Size = new Vector2(width, depth) },
            MaterialOverride = Material(colour),
            Position = new Vector3(x + (width / 2), lift, z + (depth / 2)),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    private void Box(string name, Vector3 centre, Vector3 size, Color colour, float turnDegrees = 0)
    {
        AddChild(new MeshInstance3D
        {
            Name = name,
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = Material(colour),
            Position = centre,
            RotationDegrees = new Vector3(0, turnDegrees, 0),
        });
    }

    private static (float X, float Z, float W, float D) Metres(TileRect r) => (r.X * T, r.Y * T, r.Width * T, r.Height * T);

    private void BuildGround()
    {
        var w = (float)_map.WidthMetres;
        var h = (float)_map.HeightMetres;
        Flat("Countryside", -600, -600, 1200 + w, 1200 + h, Countryside, -0.02f);
        Flat("Verge", 0, 0, w, h, Verge, 0f);

        // A patchwork of neighbouring fields, each with a thin hedge line, so the site sits in farmland.
        var laneZ = _map.Lane.Y * T;
        var laneDepth = _map.Lane.Height * T;
        for (var row = -3; row <= 3; row++)
        {
            for (var column = -3; column <= 3; column++)
            {
                if (row == 0 && column == 0)
                {
                    continue;
                }

                var x = column * (w + 6);
                var z = row * (h + 6);
                var crop = Crops[((((row * 2) + (column * 3)) % Crops.Length) + Crops.Length) % Crops.Length];
                Flat($"Crop {row} {column}", x + 3, z + 3, w - 6, h - 6, crop, -0.01f);
                Box($"Far hedge {row} {column} a", new Vector3(x + (w / 2), 0.5f, z - 3), new Vector3(w + 6, 1.0f, 1.2f), HedgeGreen);
                Box($"Far hedge {row} {column} b", new Vector3(x - 3, 0.5f, z + (h / 2)), new Vector3(1.2f, 1.0f, h + 6), HedgeGreen);
            }
        }

        // The lane carries on across the countryside in both directions.
        Flat("Lane beyond", -600, laneZ, 1200 + w, laneDepth, Tarmac, 0.015f);
    }

    private void BuildFieldGrass()
    {
        // Mown in stripes three metres wide, like a village cricket pitch.
        var (x, z, w, d) = Metres(_map.Field);
        const float stripe = 3f;
        var count = (int)Math.Ceiling(w / stripe);
        for (var i = 0; i < count; i++)
        {
            var width = Math.Min(stripe, w - (i * stripe));
            Flat($"Stripe {i}", x + (i * stripe), z, width, d, i % 2 == 0 ? Meadow : MeadowLight, 0.01f);
        }
    }

    private void BuildLane()
    {
        var (lx, lz, lw, ld) = Metres(_map.Lane);
        Flat("Lane", lx, lz, lw, ld, Tarmac, 0.02f);

        // The track runs from the lane through the gap and fans out a little way onto the grass.
        var (tx, tz, tw, td) = Metres(_map.Track);
        var (_, gz, _, gd) = Metres(_map.Gap);
        Flat("Track", tx, gz - 1.5f, tw, td + gd + 1.5f, Dirt, 0.025f);
    }

    private void BuildHedge(Random random)
    {
        var ring = _map.HedgeLine;
        var gap = _map.Gap;
        var thick = (ring.Width - _map.Field.Width) / 2 * T;
        var (rx, rz, rw, rd) = Metres(ring);
        var gapLeft = gap.X * T;
        var gapRight = gap.Right * T;

        var runs = new List<(float X, float Z, float W, float D)>
        {
            (rx, rz, rw, thick),                                         // north
            (rx, rz, thick, rd),                                         // west
            (rx + rw - thick, rz, thick, rd),                            // east
            (rx, rz + rd - thick, gapLeft - rx, thick),                  // south, west of the gap
            (gapRight, rz + rd - thick, rx + rw - gapRight, thick),      // south, east of the gap
        };

        // Bushy tops: rows of rounded clumps along every run, each a slightly different green.
        var clumps = new List<(Vector3 Position, Vector3 Scale, Color Colour)>();
        var index = 0;
        foreach (var (x, z, w, d) in runs)
        {
            Box($"Hedge {index++}", new Vector3(x + (w / 2), HedgeHeight / 2, z + (d / 2)), new Vector3(w, HedgeHeight, d), HedgeGreen);
            var along = Math.Max(w, d);
            var eastWest = w >= d;
            for (var s = 0.4f; s < along - 0.2f; s += 0.75f)
            {
                var across = (float)((random.NextDouble() - 0.5) * thick * 0.5);
                var size = 0.85f + (float)(random.NextDouble() * 0.45);
                var shade = (float)(random.NextDouble() * 0.12) - 0.04f;
                var position = eastWest
                    ? new Vector3(x + s, HedgeHeight - 0.15f, z + (d / 2) + across)
                    : new Vector3(x + (w / 2) + across, HedgeHeight - 0.15f, z + s);
                clumps.Add((position, new Vector3(size, size * 0.7f, size), HedgeGreen.Lightened(Math.Max(0, shade)).Darkened(Math.Max(0, -shade))));
            }
        }

        var mesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = new SphereMesh { Radius = 0.9f, Height = 1.8f, RadialSegments = 10, Rings = 5 },
        };
        mesh.InstanceCount = clumps.Count;
        for (var i = 0; i < clumps.Count; i++)
        {
            mesh.SetInstanceTransform(i, new Transform3D(Basis.FromScale(clumps[i].Scale), clumps[i].Position));
            mesh.SetInstanceColor(i, clumps[i].Colour.SrgbToLinear());
        }

        AddChild(new MultiMeshInstance3D
        {
            Name = "Hedge tops",
            Multimesh = mesh,
            MaterialOverride = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 1f, MetallicSpecular = 0.05f },
        });
    }

    private void BuildTrees(Random random)
    {
        var ring = _map.HedgeLine;
        var (rx, rz, rw, rd) = Metres(ring);
        var spots = new List<Vector3>
        {
            new(rx - 2.5f, 0, rz - 2.5f),
            new(rx + rw + 2.5f, 0, rz - 2f),
            new(rx + rw + 3f, 0, rz + rd + 1.5f),
            new(rx - 3f, 0, rz + (rd * 0.55f)),
        };

        // A loose row along the north side and a few along the far side of the lane.
        for (var x = rx + 6; x < rx + rw - 4; x += 9 + (float)(random.NextDouble() * 6))
        {
            spots.Add(new Vector3(x, 0, rz - 3f - (float)(random.NextDouble() * 3)));
        }

        var beyondLane = (_map.Lane.Bottom * T) + 3.5f;
        for (var x = 4f; x < (float)_map.WidthMetres; x += 13 + (float)(random.NextDouble() * 9))
        {
            spots.Add(new Vector3(x, 0, beyondLane + (float)(random.NextDouble() * 4)));
        }

        var trunk = new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.3f, Height = 2.4f, RadialSegments = 8 };
        var leaves = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 12, Rings = 6 };
        foreach (var spot in spots)
        {
            var tree = new Node3D { Name = $"Tree {TreeCount++}", Position = spot };
            var grow = 0.85f + (float)(random.NextDouble() * 0.5);
            tree.Scale = Vector3.One * grow;
            tree.RotationDegrees = new Vector3(0, (float)(random.NextDouble() * 360), 0);
            tree.AddChild(new MeshInstance3D { Mesh = trunk, MaterialOverride = Material(Trunk), Position = new Vector3(0, 1.2f, 0) });
            var green = HedgeGreen.Lightened(0.08f + (float)(random.NextDouble() * 0.14));
            tree.AddChild(new MeshInstance3D { Mesh = leaves, MaterialOverride = Material(green), Position = new Vector3(0, 3.4f, 0), Scale = new Vector3(2.1f, 1.8f, 2.1f) });
            tree.AddChild(new MeshInstance3D { Mesh = leaves, MaterialOverride = Material(green.Lightened(0.06f)), Position = new Vector3(0.9f, 4.4f, 0.3f), Scale = new Vector3(1.4f, 1.3f, 1.4f) });
            tree.AddChild(new MeshInstance3D { Mesh = leaves, MaterialOverride = Material(green.Darkened(0.05f)), Position = new Vector3(-0.8f, 4.1f, -0.5f), Scale = new Vector3(1.5f, 1.3f, 1.5f) });
            AddChild(tree);
        }
    }

    /// <summary>Gateposts either side of the gap and a painted sign by the lane with the festival's name.</summary>
    private void BuildWayIn(string festivalName)
    {
        var (gx, gz, gw, gd) = Metres(_map.Gap);
        var frontZ = gz + gd + 0.1f;
        Box("Gatepost west", new Vector3(gx - 0.45f, 1.05f, frontZ + 0.35f), new Vector3(0.34f, 2.1f, 0.34f), Wood);
        Box("Gatepost east", new Vector3(gx + gw + 0.45f, 1.05f, frontZ + 0.35f), new Vector3(0.34f, 2.1f, 0.34f), Wood);

        var signX = gx + gw + 3.2f;
        var signZ = (_map.Lane.Y * T) - 1.6f;
        var sign = new Node3D { Name = "Sign", Position = new Vector3(signX, 0, signZ), RotationDegrees = new Vector3(0, 28f, 0) };
        AddChild(sign);
        var post = new BoxMesh { Size = new Vector3(0.16f, 2.3f, 0.16f) };
        sign.AddChild(new MeshInstance3D { Mesh = post, MaterialOverride = Material(Wood), Position = new Vector3(-1.5f, 1.15f, 0) });
        sign.AddChild(new MeshInstance3D { Mesh = post, MaterialOverride = Material(Wood), Position = new Vector3(1.5f, 1.15f, 0) });
        sign.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(3.6f, 1.2f, 0.1f) },
            MaterialOverride = Material(new Color("#E94F7B")),
            Position = new Vector3(0, 1.85f, 0),
        });
        sign.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(3.6f, 0.14f, 0.12f) },
            MaterialOverride = Material(new Color("#F6B93B")),
            Position = new Vector3(0, 1.3f, 0),
        });
        sign.AddChild(new Label3D
        {
            Name = "Name",
            Text = festivalName,
            FontSize = 64,
            PixelSize = Math.Min(0.0075f, 3.2f / (festivalName.Length * 37f)),
            Modulate = Colors.White,
            OutlineSize = 0,
            Position = new Vector3(0, 1.9f, 0.06f),
            DoubleSided = false,
        });
    }
}
