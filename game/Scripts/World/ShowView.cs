using System.Collections.Generic;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// Draws the show: whoever is on stage right now, with their instruments, and the stage lights.
/// The rules say who is playing and when; this puts them on the first stage, moves them a little
/// in time with the music, and brings the lights up as it gets dark.
/// </summary>
public partial class ShowView : Node3D
{
    private readonly List<(Node3D Node, BandRole Role, Vector3 At, float Phase)> _band = new();
    private readonly List<(Light3D Light, float Full)> _lights = new();
    private readonly List<(StandardMaterial3D Material, Color Colour)> _beams = new();
    private FestivalGame _game = null!;
    private Node3D _acts = null!;
    private Node3D _rig = null!;
    private PlacedStage? _stage;
    private int _slot = -2;
    private int _onStage = -1;
    private bool _djLights;
    private double _time;

    /// <summary>How many performers are drawn on the stage.</summary>
    public int PerformerCount => _band.Count;

    /// <summary>How bright the stage lights are, from 0 (off, by day) to 1 (a set at night).</summary>
    public float LightStrength { get; private set; }

    public void Initialise(FestivalGame game)
    {
        _game = game;
        _acts = new Node3D { Name = "Acts" };
        AddChild(_acts);
        _rig = new Node3D { Name = "Lights" };
        AddChild(_rig);
        Sync(0);
    }

    public override void _Process(double delta) => Sync(delta);

    /// <summary>Where each member of an act stands on a stage, in metres from its middle, the stage facing south.</summary>
    public static List<Vector3> Spots(StageKind kind, IReadOnlyList<BandRole> roles)
    {
        var (deck, shift) = kind switch { StageKind.FarmTrailer => (1.1f, -0.25f), StageKind.Bandstand => (0.6f, -0.5f), _ => (1.3f, 0f) };
        Vector2[] front = kind switch
        {
            StageKind.FarmTrailer => new[] { new Vector2(-1.3f, 0.3f), new Vector2(1.2f, 0.2f) },
            StageKind.Bandstand => new[] { new Vector2(0, 1.2f), new Vector2(-1.9f, 0.8f), new Vector2(1.9f, 0.8f) },
            _ => new[] { new Vector2(0, 1.7f), new Vector2(-2.4f, 1.2f), new Vector2(2.4f, 1.2f) },
        };
        var big = kind is StageKind.HiredStage or StageKind.MainStage;
        var spots = new List<Vector3>();
        var used = 0;
        foreach (var role in roles)
        {
            Vector3 at;
            if (roles.Count == 1)
            {
                at = new Vector3(0, deck, (big ? 0.6f : 0.3f) + shift);
            }
            else if (role == BandRole.Drummer && kind != StageKind.FarmTrailer)
            {
                // The hired stage has a drum riser at the back.
                at = big ? new Vector3(-0.2f, deck + 0.5f, -1.9f) : new Vector3(0, deck, -1.3f + shift);
            }
            else if (role == BandRole.Keys && big)
            {
                at = new Vector3(3.0f, deck, -0.6f);
            }
            else
            {
                var spot = front[used % front.Length];
                used++;
                at = new Vector3(spot.X, deck, spot.Y + shift);
            }

            // The main stage is the hired stage made bigger, so everyone stands further apart and higher up.
            spots.Add(kind == StageKind.MainStage ? at * SiteProps.MainStageScale : at);
        }

        return spots;
    }

    private void Sync(double delta)
    {
        var now = _game.Now;
        var show = _game.Show;
        var stage = show.Stage;
        // While an act is stopped to let the crowd ease, the stage is empty.
        var slot = _game.ActPaused ? null : show.Playing(now);
        var roles = _game.ActPaused ? System.Array.Empty<BandRole>() : show.OnStage(now);
        if (stage != _stage || (slot?.Index ?? -1) != _slot || roles.Count != _onStage)
        {
            var dj = slot?.Act.Kind == ActKind.Dj;
            if (stage != _stage || dj != _djLights)
            {
                BuildLights(stage, dj);
            }

            _stage = stage;
            _slot = slot?.Index ?? -1;
            _onStage = roles.Count;
            _djLights = dj;
            BuildBand(stage, slot, roles);
        }

        // Lights come up with the dark: full for a set, low while the stage is being changed over.
        var night = 1f - (float)_game.Light;
        var want = stage is null ? 0f : night * (slot is null ? 0.3f : 1f);
        LightStrength = delta <= 0 ? want : Mathf.MoveToward(LightStrength, want, (float)delta * 0.8f);
        _rig.Visible = LightStrength > 0.01f;
        foreach (var (light, full) in _lights)
        {
            light.LightEnergy = full * LightStrength;
        }

        foreach (var (material, colour) in _beams)
        {
            material.AlbedoColor = new Color(colour, 0.4f * LightStrength);
        }

        // The band moves to the music unless the game is paused.
        var speed = (int)_game.Clock.Speed;
        if (speed > 0)
        {
            _time += delta;
        }

        foreach (var (node, role, at, phase) in _band)
        {
            var beat = (float)(_time * 4.2) + phase;
            var still = role is BandRole.Drummer or BandRole.Keys or BandRole.Dj;
            node.Position = at + new Vector3(still ? 0 : Mathf.Sin(beat * 0.25f) * 0.12f, 0, 0);
            var person = node.GetNode<PersonView>("Person");
            person.Position = new Vector3(0, Mathf.Abs(Mathf.Sin(beat)) * (role == BandRole.Dj ? 0.07f : 0.035f), 0);
            person.RotationDegrees = new Vector3(0, Mathf.Sin(beat * 0.5f) * (still ? 5f : 14f), 0);
            if (role is BandRole.Singer or BandRole.Dj)
            {
                // Arms up for the big moments, down in between.
                person.Pose = Mathf.Sin(beat * 0.11f) > (role == BandRole.Dj ? 0.3f : -0.2f) ? Pose.Cheering : Pose.Standing;
                person.Animate(speed > 0 ? delta : 0);
            }
        }
    }

    private void BuildBand(PlacedStage? stage, Slot? slot, IReadOnlyList<BandRole> roles)
    {
        foreach (var child in _acts.GetChildren())
        {
            _acts.RemoveChild(child);
            child.QueueFree();
        }

        _band.Clear();
        if (stage is null || slot is null)
        {
            return;
        }

        _acts.Position = SiteView.Middle(stage.Rect);
        _acts.RotationDegrees = new Vector3(0, SiteView.Turn(stage.Facing), 0);
        var spots = Spots(stage.Kind, roles);
        for (var i = 0; i < roles.Count; i++)
        {
            var performer = Performers.Build(roles[i], slot.Act.LookSeed + (i * 13));
            performer.Position = spots[i];
            _acts.AddChild(performer);
            _band.Add((performer, roles[i], spots[i], i * 1.7f));
        }
    }

    /// <summary>Coloured spots on the band, beams in the air and a wash over the front of the crowd.</summary>
    private void BuildLights(PlacedStage? stage, bool dj)
    {
        foreach (var child in _rig.GetChildren())
        {
            _rig.RemoveChild(child);
            child.QueueFree();
        }

        _lights.Clear();
        _beams.Clear();
        if (stage is null)
        {
            return;
        }

        _rig.Position = SiteView.Middle(stage.Rect);
        _rig.RotationDegrees = new Vector3(0, SiteView.Turn(stage.Facing), 0);
        var k = stage.Kind == StageKind.MainStage ? SiteProps.MainStageScale : 1f;
        if (stage.Kind is not (StageKind.HiredStage or StageKind.MainStage))
        {
            // Small stages get a string of warm bulbs' worth of light, no more.
            var high = stage.Kind == StageKind.Bandstand ? 3.2f : 3.6f;
            Add(new OmniLight3D { LightColor = new Color("#FFD9A0"), OmniRange = 9f, Position = new Vector3(0, high, 0.6f) }, 3.5f);
            Add(new OmniLight3D { LightColor = new Color("#FFE8C0"), OmniRange = 8f, Position = new Vector3(0, 2.6f, 5f) }, 1.4f);
            return;
        }

        var colours = dj
            ? new[] { new Color("#7B4FE9"), new Color("#2FD7E8"), new Color("#E94F7B"), new Color("#2FD7E8"), new Color("#7B4FE9") }
            : new[] { new Color("#F6B93B"), new Color("#E94F7B"), new Color("#FFF1C9"), new Color("#E94F7B"), new Color("#F6B93B") };
        for (var i = 0; i < colours.Length; i++)
        {
            var x = (i - 2) * 1.9f * k;
            var from = new Vector3(x, 6.3f * k, 2.6f * k);
            var spot = new SpotLight3D { LightColor = colours[i], SpotRange = 14f * k, SpotAngle = 32f, ShadowEnabled = false, Position = from };
            Add(spot, 9f * k);
            spot.LookAtFromPosition(from, new Vector3(x * 0.8f, 1.3f * k, 0.2f), Vector3.Up);

            // A beam through the haze, pointing out over the crowd.
            var material = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                AlbedoColor = new Color(colours[i], 0f),
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            };
            var tilt = new Vector3(52, (i - 2) * 16, 0);
            _rig.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.08f * k, BottomRadius = 1.3f * k, Height = 11f * k, RadialSegments = 12, Rings = 1 },
                MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                RotationDegrees = tilt,
                Position = from + (Basis.FromEuler(tilt * (Mathf.Pi / 180f)) * new Vector3(0, -5.5f * k, 0)) + new Vector3(0, 0.2f, 0.5f),
            });
            _beams.Add((material, colours[i]));
        }

        Add(new OmniLight3D { LightColor = dj ? new Color("#9C7BFF") : new Color("#FFD9A0"), OmniRange = 17f * k, Position = new Vector3(0, 5.5f * k, 8f * k) }, 5f * k);
        Add(new OmniLight3D { LightColor = new Color("#FFE8C0"), OmniRange = 9f * k, Position = new Vector3(0, 4.5f * k, 1f) }, 3f * k);
    }

    private void Add(Light3D light, float full)
    {
        light.LightEnergy = 0;
        _rig.AddChild(light);
        _lights.Add((light, full));
    }
}
