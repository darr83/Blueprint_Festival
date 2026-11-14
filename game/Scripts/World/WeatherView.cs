using System;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.World;
using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// Draws the rain coming down, and the straw and trackway laid on the grass. The mud itself is
/// drawn with the worn grass, by <see cref="LitterView"/>.
/// </summary>
public partial class WeatherView : Node3D
{
    private int _streaks = 2600;
    private float[] _rainBuffer = Array.Empty<float>();
    private const float Top = 16f;

    private static readonly Color Straw = new("#E0C46C");
    private static readonly Color StrawDark = new("#C9A94F");
    private static readonly Color Trackway = new("#AEB5BB");
    private static readonly Color TrackwayDark = new("#8C949B");

    private FestivalGame _game = null!;
    private MultiMeshInstance3D _rain = null!;
    private MeshInstance3D? _cover;
    private Vector3[] _starts = Array.Empty<Vector3>();
    private float[] _speeds = Array.Empty<float>();
    private double _time;
    private int _coverDrawn = -1;
    private int _coverSum = -1;

    /// <summary>How hard the rain is drawn falling just now, from 0 to 1. It eases in and out.</summary>
    public float Falling { get; private set; }

    /// <summary>How many streaks of rain are drawn.</summary>
    public int StreaksShown => _rain.Multimesh.VisibleInstanceCount;

    /// <summary>How many patches of straw or trackway are drawn.</summary>
    public int CoverShown => Math.Max(0, _coverDrawn);

    public void Initialise(FestivalGame game)
    {
        _game = game;
        _rain = new MultiMeshInstance3D
        {
            Name = "Rain",
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                AlbedoColor = new Color(0.86f, 0.92f, 1f, 0.5f),
            },
            Multimesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                Mesh = new BoxMesh { Size = new Vector3(0.035f, 0.75f, 0.035f) },
                InstanceCount = 0,
                VisibleInstanceCount = 0,
            },
        };
        AddChild(_rain);

        // Every streak has its own place and speed, made up once from its number, so the rain looks the same on every computer.
        var width = game.Field.Width * (float)FieldMap.TileMetres;
        var depth = game.Field.Height * (float)FieldMap.TileMetres;

        // A bigger field needs more rain to look as wet.
        _streaks = Math.Clamp((int)(2600 * width * depth / 6000f), 2600, 12000);
        _rain.Multimesh.InstanceCount = _streaks;
        _rainBuffer = new float[_streaks * 12];
        _starts = new Vector3[_streaks];
        _speeds = new float[_streaks];
        var random = new Random(77);
        for (var i = 0; i < _streaks; i++)
        {
            _starts[i] = new Vector3((float)random.NextDouble() * width, (float)random.NextDouble() * Top, (float)random.NextDouble() * depth);
            _speeds[i] = 13f + ((float)random.NextDouble() * 5f);
        }

        Refresh();
    }

    public override void _Process(double delta)
    {
        _time += delta;
        var target = (float)_game.RainNow;
        Falling = Mathf.MoveToward(Falling, target, (float)delta * 0.5f);
        DrawRain();
        Refresh();
    }

    /// <summary>Jumps the rain straight to how hard it is falling now, for pictures.</summary>
    public void Settle()
    {
        Falling = (float)_game.RainNow;
        DrawRain();
        Refresh();
    }

    private void DrawRain()
    {
        var shown = Falling < 0.03f ? 0 : (int)(_streaks * Mathf.Clamp(0.25f + (Falling * 0.75f), 0, 1));
        var mesh = _rain.Multimesh;
        mesh.VisibleInstanceCount = shown;
        if (shown == 0)
        {
            return;
        }

        // The rain comes down on a slant, as if on a breeze.
        var slant = new Basis(new Vector3(0, 0, 1), Mathf.DegToRad(9));
        for (var i = 0; i < shown; i++)
        {
            var start = _starts[i];
            var y = Mathf.PosMod(start.Y - ((float)_time * _speeds[i]), Top);
            var o = i * 12;
            (_rainBuffer[o], _rainBuffer[o + 1], _rainBuffer[o + 2], _rainBuffer[o + 3]) = (slant.X.X, slant.Y.X, slant.Z.X, start.X + (y * 0.16f));
            (_rainBuffer[o + 4], _rainBuffer[o + 5], _rainBuffer[o + 6], _rainBuffer[o + 7]) = (slant.X.Y, slant.Y.Y, slant.Z.Y, y);
            (_rainBuffer[o + 8], _rainBuffer[o + 9], _rainBuffer[o + 10], _rainBuffer[o + 11]) = (slant.X.Z, slant.Y.Z, slant.Z.Z, start.Z);
        }

        mesh.Buffer = _rainBuffer;
    }

    /// <summary>Draws the straw and trackway again if any has been laid or lifted.</summary>
    public void Refresh()
    {
        var count = _game.GroundCovered;
        var sum = 0;
        if (count > 0)
        {
            for (var row = 0; row < _game.WearRows; row++)
            {
                for (var column = 0; column < _game.WearColumns; column++)
                {
                    sum += _game.GroundAt(column, row) is { } kind ? ((int)kind + 1) * ((row * 131) + column + 7) : 0;
                }
            }
        }

        if (count == _coverDrawn && sum == _coverSum)
        {
            return;
        }

        _coverDrawn = count;
        _coverSum = sum;
        _cover?.QueueFree();
        _cover = null;
        if (count == 0)
        {
            return;
        }

        var f = _game.Field.Field;
        var size = FestivalGame.WearTiles * (float)FieldMap.TileMetres;
        var b = new MeshBuilder();
        for (var row = 0; row < _game.WearRows; row++)
        {
            for (var column = 0; column < _game.WearColumns; column++)
            {
                if (_game.GroundAt(column, row) is not { } kind)
                {
                    continue;
                }

                var x = (f.X * (float)FieldMap.TileMetres) + ((column + 0.5f) * size);
                var z = (f.Y * (float)FieldMap.TileMetres) + ((row + 0.5f) * size);
                if (kind == GroundKind.Trackway)
                {
                    // Two metal panels side by side, with ribs across them for grip.
                    b.Box(new Vector3(x, 0.03f, z), new Vector3(size - 0.04f, 0.06f, size - 0.04f), Trackway);
                    for (var rib = 0; rib < 5; rib++)
                    {
                        b.Box(new Vector3(x, 0.065f, z - (size / 2) + 0.2f + (rib * (size - 0.4f) / 4)), new Vector3(size - 0.2f, 0.02f, 0.07f), TrackwayDark);
                    }
                }
                else
                {
                    // A scatter of straw: a pale mat with darker wisps lying every way on top.
                    b.Box(new Vector3(x, 0.025f, z), new Vector3(size, 0.05f, size), Straw);
                    for (var wisp = 0; wisp < 7; wisp++)
                    {
                        var pick = (column * 31) + (row * 17) + (wisp * 53);
                        var dx = (((pick * 37) % 100) / 100f - 0.5f) * (size - 0.5f);
                        var dz = (((pick * 91) % 100) / 100f - 0.5f) * (size - 0.5f);
                        b.Box(new Vector3(x + dx, 0.055f, z + dz), new Vector3(0.55f, 0.02f, 0.06f), StrawDark, new Vector3(0, pick * 29 % 180, 0));
                    }
                }
            }
        }

        _cover = b.BuildNode("GroundCover");
        _cover.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        AddChild(_cover);
    }
}
