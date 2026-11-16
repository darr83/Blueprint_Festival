using System.Collections.Generic;
using System.Linq;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.People;
using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// The fires in the camping field at night, with campers sitting round them: flames in the fire
/// pits first, then small fires by the tents when there are more campers up than the pits can
/// seat. Each fire flickers and lights the people round it, and goes out as they go to bed.
/// </summary>
public partial class CampFiresView : Node3D
{
    private FestivalGame _game = null!;
    private StandardMaterial3D _flame = null!;
    private StandardMaterial3D _core = null!;
    private readonly List<OmniLight3D> _lights = new();
    private string _drawn = "";
    private double _since = 1;
    private double _time;

    /// <summary>How many fires are drawn just now, for the smoke test.</summary>
    public int FiresDrawn => GetChildCount();

    public void Initialise(FestivalGame game)
    {
        _game = game;
        _flame = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(1f, 0.52f, 0.12f, 0.9f),
        };
        _core = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(1f, 0.88f, 0.42f),
        };
    }

    public override void _Process(double delta)
    {
        _time += delta;
        _since += delta;
        if (_since >= 0.5)
        {
            _since = 0;
            Refresh();
        }

        // The fires flicker, each on its own beat.
        for (var i = 0; i < _lights.Count; i++)
        {
            var flicker = 0.8f + (0.2f * Mathf.Sin((float)(_time * 9) + (i * 1.7f))) + (0.1f * Mathf.Sin((float)(_time * 23) + i));
            _lights[i].LightEnergy = 1.6f * flicker;
            if (_lights[i].GetParent() is Node3D fire && fire.GetNodeOrNull<Node3D>("Flames") is { } flames)
            {
                flames.Scale = new Vector3(1, 0.85f + (0.25f * flicker), 1);
            }
        }
    }

    /// <summary>Draws the fires again if they have changed.</summary>
    public void Refresh()
    {
        var fires = _game.CampFires;
        var key = string.Join(";", fires.Select(f => $"{f.X:0.#},{f.Y:0.#}"));
        if (key == _drawn)
        {
            return;
        }

        _drawn = key;
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        _lights.Clear();
        for (var i = 0; i < fires.Count; i++)
        {
            AddChild(Fire(fires[i], i));
        }
    }

    private Node3D Fire(CampFire fire, int index)
    {
        var node = new Node3D { Name = $"Fire{index}", Position = new Vector3((float)fire.X, 0, (float)fire.Y) };
        var lift = fire.InPit ? 0.38f : 0.05f;
        if (!fire.InPit)
        {
            // A few stones round a fire on the grass, and a scorch mark.
            var ring = new MeshBuilder();
            ring.Tube(new Vector3(0, 0.01f, 0), new Vector3(0.9f, 0.02f, 0.9f), new Color("#3A3028"));
            for (var k = 0; k < 7; k++)
            {
                var a = k * Mathf.Tau / 7;
                ring.Ball(new Vector3(0.42f * Mathf.Cos(a), 0.06f, 0.42f * Mathf.Sin(a)), new Vector3(0.18f, 0.12f, 0.15f), new Color("#8D8980"));
            }

            node.AddChild(ring.BuildNode("Stones"));
        }

        var flames = new MeshBuilder();
        var size = fire.InPit ? 1f : 0.65f;
        flames.Cone(new Vector3(0, lift + (0.3f * size), 0), new Vector3(0.6f * size, 0.6f * size, 0.6f * size), Colors.White);
        flames.Cone(new Vector3(0.12f * size, lift + (0.22f * size), 0.08f * size), new Vector3(0.35f * size, 0.44f * size, 0.35f * size), Colors.White, new Vector3(0, 40, 8));
        var outer = flames.BuildNode("Flames");
        outer.MaterialOverride = _flame;
        outer.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        node.AddChild(outer);

        var core = new MeshBuilder();
        core.Cone(new Vector3(0, lift + (0.16f * size), 0), new Vector3(0.3f * size, 0.32f * size, 0.3f * size), Colors.White);
        var inner = core.BuildNode("Core");
        inner.MaterialOverride = _core;
        inner.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        node.AddChild(inner);

        var light = new OmniLight3D { Name = "Glow", Position = new Vector3(0, lift + 0.6f, 0), OmniRange = fire.InPit ? 7 : 5, LightEnergy = 1.6f, LightColor = new Color(1f, 0.62f, 0.3f), ShadowEnabled = false };
        node.AddChild(light);
        _lights.Add(light);

        // Campers sit round on the grass, facing the fire.
        var seats = fire.InPit ? 6 : 3;
        var radius = fire.InPit ? 1.45f : 1.0f;
        for (var k = 0; k < seats; k++)
        {
            var seed = (index * 31) + (k * 7) + 4000;
            var look = LookGenerator.FestivalGoer(seed, AgeGroup.Adult) with { Poncho = null, Sunglasses = false };
            var person = PersonView.Build(look);
            person.SeatHeight = 0.12f;
            person.SetPose(Pose.Sitting);
            var a = (k * Mathf.Tau / seats) + (index * 0.7f);
            person.Position = new Vector3(radius * Mathf.Cos(a), 0, radius * Mathf.Sin(a));
            // Face the middle: people are built facing +Z.
            person.Rotation = new Vector3(0, Mathf.Atan2(-person.Position.X, -person.Position.Z), 0);
            node.AddChild(person);
        }

        return node;
    }
}
