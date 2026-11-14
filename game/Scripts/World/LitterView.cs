using System;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.World;
using Godot;

namespace BlueprintFestival.World;

/// <summary>Draws the litter people have dropped and the grass their feet have worn brown.</summary>
public partial class LitterView : Node3D
{
    private static readonly Color[] Colours =
    {
        new("#F5F5F5"), new("#D9483B"), new("#4FA8D8"), new("#F6D84A"), new("#C9C2B0"), new("#7A4A2A"),
    };

    private static readonly Color DryEarth = new(0.48f, 0.4f, 0.27f);
    private static readonly Color Mud = new(0.2f, 0.14f, 0.08f);

    private FestivalGame _game = null!;
    private readonly MultiMeshInstance3D[] _pieces = new MultiMeshInstance3D[3];
    private readonly float[][] _buffers = { Array.Empty<float>(), Array.Empty<float>(), Array.Empty<float>() };
    private MultiMeshInstance3D _worn = null!;
    private int _drawn = -1;
    private double _sinceLitter = 1;
    private double _sinceWear = 10;

    /// <summary>How many pieces of litter are drawn.</summary>
    public int Count => Math.Max(0, _drawn);

    /// <summary>How many patches of grass are drawn worn.</summary>
    public int WornPatches => _worn.Multimesh.VisibleInstanceCount;

    public void Initialise(FestivalGame game)
    {
        _game = game;
        var patch = FestivalGame.WearTiles * (float)FieldMap.TileMetres;
        _worn = new MultiMeshInstance3D
        {
            Name = "WornGrass",
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                VertexColorUseAsAlbedo = true,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            },
            Multimesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseColors = true,
                Mesh = new PlaneMesh { Size = new Vector2(patch, patch) },
                InstanceCount = game.WearColumns * game.WearRows,
                VisibleInstanceCount = 0,
            },
        };
        AddChild(_worn);

        // Litter is cans, wrappers and crumpled cups: one shape of each, drawn as many times as there are pieces.
        var shapes = new Mesh[]
        {
            new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 1f, RadialSegments = 8, Rings = 1 },
            new BoxMesh { Size = Vector3.One },
            new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 8, Rings = 4 },
        };
        for (var kind = 0; kind < 3; kind++)
        {
            _pieces[kind] = new MultiMeshInstance3D
            {
                Name = $"Litter {kind}",
                Visible = false,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = shapes[kind] },
                MaterialOverride = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 1f, MetallicSpecular = 0.05f },
            };
            AddChild(_pieces[kind]);
        }

        Refresh();
    }

    public override void _Process(double delta)
    {
        _sinceLitter += delta;
        _sinceWear += delta;
        if ((_game.Litter.Count != _drawn && _sinceLitter > 0.25) || _sinceWear > 4)
        {
            Refresh();
        }
    }

    /// <summary>Draws the litter and the worn grass again as they are right now.</summary>
    public void Refresh()
    {
        if (_game.Litter.Count != _drawn)
        {
            _sinceLitter = 0;
            _drawn = _game.Litter.Count;
            var counts = new int[3];
            var needed = Mathf.NearestPo2(Math.Max(64, _drawn)) * 16;
            for (var kind = 0; kind < 3; kind++)
            {
                if (_buffers[kind].Length < needed)
                {
                    _buffers[kind] = new float[needed];
                }
            }

            foreach (var piece in _game.Litter)
            {
                // The look of a piece comes from where it lies, so it does not change as others are picked up.
                var pick = (int)(Math.Abs((piece.X * 37.1) + (piece.Y * 91.7)) * 10);
                var colour = Colours[pick % Colours.Length].SrgbToLinear();
                var kind = pick / 7 % 3;
                var (lift, shape, lean) = kind switch
                {
                    0 => (0.08f, new Vector3(0.12f, 0.2f, 0.12f), 90f),
                    1 => (0.05f, new Vector3(0.3f, 0.04f, 0.22f), 0f),
                    _ => (0.09f, new Vector3(0.2f, 0.14f, 0.18f), 0f),
                };
                var basis = Basis.FromEuler(new Vector3(Mathf.DegToRad(lean), Mathf.DegToRad(pick * 47 % 180), 0)).Scaled(shape);
                var o = counts[kind]++ * 16;
                var buffer = _buffers[kind];
                (buffer[o], buffer[o + 1], buffer[o + 2], buffer[o + 3]) = (basis.X.X, basis.Y.X, basis.Z.X, (float)piece.X);
                (buffer[o + 4], buffer[o + 5], buffer[o + 6], buffer[o + 7]) = (basis.X.Y, basis.Y.Y, basis.Z.Y, lift);
                (buffer[o + 8], buffer[o + 9], buffer[o + 10], buffer[o + 11]) = (basis.X.Z, basis.Y.Z, basis.Z.Z, (float)piece.Y);
                (buffer[o + 12], buffer[o + 13], buffer[o + 14], buffer[o + 15]) = (colour.R, colour.G, colour.B, 1f);
            }

            for (var kind = 0; kind < 3; kind++)
            {
                var mesh = _pieces[kind].Multimesh;
                _pieces[kind].Visible = counts[kind] > 0;
                if (counts[kind] > 0)
                {
                    if (mesh.InstanceCount * 16 != _buffers[kind].Length)
                    {
                        mesh.InstanceCount = _buffers[kind].Length / 16;
                    }

                    mesh.Buffer = _buffers[kind];
                    mesh.VisibleInstanceCount = counts[kind];
                }
            }
        }

        _sinceWear = 0;
        var f = _game.Field.Field;
        var size = FestivalGame.WearTiles * (float)FieldMap.TileMetres;
        var shown = 0;
        for (var row = 0; row < _game.WearRows; row++)
        {
            for (var column = 0; column < _game.WearColumns; column++)
            {
                var wear = (float)_game.WearAt(column, row);
                var mud = (float)_game.MudAt(column, row);
                if ((wear < 0.25f && mud < 0.08f) || _game.GroundAt(column, row) is not null)
                {
                    continue;
                }

                var x = (f.X * (float)FieldMap.TileMetres) + ((column + 0.5f) * size);
                var z = (f.Y * (float)FieldMap.TileMetres) + ((row + 0.5f) * size);
                _worn.Multimesh.SetInstanceTransform(shown, new Transform3D(Basis.Identity, new Vector3(x, 0.02f, z)));
                // Dry, worn grass goes pale brown; wet, it turns to dark mud.
                var colour = DryEarth.Lerp(Mud, Mathf.Clamp(mud * 1.4f, 0, 1));
                colour.A = Mathf.Clamp(Mathf.Max((wear - 0.2f) * 0.75f, mud * 1.5f), 0, 0.92f);
                _worn.Multimesh.SetInstanceColor(shown, colour);
                shown++;
            }
        }

        _worn.Multimesh.VisibleInstanceCount = shown;
    }
}
