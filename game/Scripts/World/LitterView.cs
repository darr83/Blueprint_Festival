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

    private FestivalGame _game = null!;
    private MeshInstance3D? _litter;
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
            _litter?.QueueFree();
            _litter = null;
            if (_drawn > 0)
            {
                var b = new MeshBuilder();
                foreach (var piece in _game.Litter)
                {
                    // The look of a piece comes from where it lies, so it does not change as others are picked up.
                    var pick = (int)(Math.Abs((piece.X * 37.1) + (piece.Y * 91.7)) * 10);
                    var colour = Colours[pick % Colours.Length];
                    var at = new Vector3((float)piece.X, 0.05f, (float)piece.Y);
                    var turn = pick * 47 % 180;
                    switch (pick / 7 % 3)
                    {
                        case 0:
                            b.Tube(at + new Vector3(0, 0.03f, 0), new Vector3(0.12f, 0.2f, 0.12f), colour, new Vector3(90, turn, 0));
                            break;
                        case 1:
                            b.Box(at, new Vector3(0.3f, 0.04f, 0.22f), colour, new Vector3(0, turn, 0));
                            break;
                        default:
                            b.Ball(at + new Vector3(0, 0.04f, 0), new Vector3(0.2f, 0.14f, 0.18f), colour, new Vector3(0, turn, 0));
                            break;
                    }
                }

                _litter = b.BuildNode("Litter");
                AddChild(_litter);
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
                if (wear < 0.25f)
                {
                    continue;
                }

                var x = (f.X * (float)FieldMap.TileMetres) + ((column + 0.5f) * size);
                var z = (f.Y * (float)FieldMap.TileMetres) + ((row + 0.5f) * size);
                _worn.Multimesh.SetInstanceTransform(shown, new Transform3D(Basis.Identity, new Vector3(x, 0.02f, z)));
                _worn.Multimesh.SetInstanceColor(shown, new Color(0.48f, 0.4f, 0.27f, (wear - 0.2f) * 0.75f));
                shown++;
            }
        }

        _worn.Multimesh.VisibleInstanceCount = shown;
    }
}
