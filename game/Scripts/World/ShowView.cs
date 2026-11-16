using System.Collections.Generic;
using System.Linq;
using BlueprintFestival.Audio;
using BlueprintFestival.Core;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// Draws the show: whoever is on stage right now, with their instruments, and the stage lights.
/// The rules say who is playing and when; this puts them on the first stage, has them play along
/// with the music (see <see cref="BandMoves"/>), and brings the lights up as it gets dark.
/// </summary>
public partial class ShowView : Node3D
{
    private readonly List<BandMember> _band = new();
    private readonly StageBeat _beat = new();
    private readonly List<(Light3D Light, float Full)> _lights = new();
    private readonly List<Light3D> _shadowSpots = new();

    /// <summary>How many stage lights are throwing shadows right now.</summary>
    public int ShadowSpotCount => _shadowSpots.Count(l => l.ShadowEnabled);

    private void FollowSettings()
    {
        foreach (var spot in _shadowSpots)
        {
            spot.ShadowEnabled = Graphics.Shadows;
        }
    }
    private readonly List<(ShaderMaterial Material, Color Colour)> _beams = new();
    private static Shader? _beamShader;

    /// <summary>How a beam of stage light is drawn: light added to the picture, fading along its length and towards its edges.</summary>
    private static Shader BeamShader => _beamShader ??= new Shader
    {
        Code = """
            shader_type spatial;
            render_mode unshaded, blend_add, cull_disabled, depth_draw_never, shadows_disabled;
            uniform vec4 colour : source_color = vec4(1.0);
            uniform float strength = 0.0;
            void fragment() {
                // Down the side of the cone the picture runs from 0 at the lamp to 0.5 at the far end.
                float along = clamp(UV.y * 2.0, 0.0, 1.0);
                float edge = abs(dot(normalize(NORMAL), normalize(VIEW)));
                ALBEDO = colour.rgb * 1.6;
                ALPHA = strength * pow(edge, 1.4) * pow(1.0 - along, 1.6);
            }
            """,
    };

    /// <summary>How bright the beams in the air are drawn at full strength.</summary>
    public const float BeamStrength = 0.55f;
    private FestivalGame _game = null!;
    private Node3D _acts = null!;
    private Node3D _rig = null!;
    private PlacedStage? _stage;
    private int _slot = -2;
    private int _onStage = -1;
    private bool _djLights;
    private double _time;

    /// <summary>The music player, so the band can play to the song that is heard.</summary>
    public MusicPlayer? Music { get; set; }

    /// <summary>What is going on at this stage, and its beat.</summary>
    public StageBeat Beat => _beat;

    /// <summary>The members of the act on stage, as they are drawn.</summary>
    public IReadOnlyList<BandMember> Band => _band;

    /// <summary>How many performers are drawn on the stage.</summary>
    public int PerformerCount => _band.Count;

    /// <summary>How bright the stage lights are, from 0 (off, by day) to 1 (a set at night).</summary>
    public float LightStrength { get; private set; }

    /// <summary>True for the view of an extra stage's acts (the second, third or fourth).</summary>
    public bool Second => Number > 0;

    /// <summary>Which stage's acts this draws: 0 for the main stage, then 1 to 3.</summary>
    public int Number { get; private set; }

    public void Initialise(FestivalGame game, bool second = false) => Initialise(game, second ? 1 : 0);

    public void Initialise(FestivalGame game, int number)
    {
        _game = game;
        Number = number;
        _acts = new Node3D { Name = "Acts" };
        AddChild(_acts);
        _rig = new Node3D { Name = "Lights" };
        AddChild(_rig);
        Graphics.Changed += FollowSettings;
        Sync(0);
    }

    public override void _ExitTree() => Graphics.Changed -= FollowSettings;

    public override void _Process(double delta) => Sync(delta);

    /// <summary>Where each member of an act stands on a stage, in metres from its middle, the stage facing south.</summary>
    public static List<Vector3> Spots(StageKind kind, IReadOnlyList<BandRole> roles)
    {
        // Every style of a size shares a deck, so the band stands in the same places on each.
        kind = Catalogue.DeckOf(kind);
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
        var show = _game.Shows[Number];
        var stage = show.Stage;
        var speed = (int)_game.Clock.Speed;
        _beat.Update(_game, show, Music, _game.ActPaused || speed == 0, delta);
        // While an act is stopped to let the crowd ease, the stage is empty. After a set the act
        // stays on for a few minutes to take the applause.
        var slot = _beat.Slot;
        var roles = slot is null || stage is null ? System.Array.Empty<BandRole>() : slot.Act.On(Catalogue.Of(stage.Kind).Performers);
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

        foreach (var (material, _) in _beams)
        {
            material.SetShaderParameter("strength", BeamStrength * LightStrength);
        }

        // The band plays to the music unless the game is paused.
        var step = speed > 0 ? delta : 0;
        _time += step;
        foreach (var member in _band)
        {
            BandMoves.Animate(member, _beat, _time, step);
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
            var performer = Performers.Build(roles[i], StarLooks.For(slot.Act, i), slot.Act.LookSeed + (i * 13));
            performer.Position = spots[i];
            _acts.AddChild(performer);
            _band.Add(new BandMember(performer, roles[i], spots[i], i));
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
        _shadowSpots.Clear();
        _beams.Clear();
        if (stage is null)
        {
            return;
        }

        _rig.Position = SiteView.Middle(stage.Rect);
        _rig.RotationDegrees = new Vector3(0, SiteView.Turn(stage.Facing), 0);
        var deck = Catalogue.DeckOf(stage.Kind);
        var k = deck == StageKind.MainStage ? SiteProps.MainStageScale : 1f;
        if (deck is not (StageKind.HiredStage or StageKind.MainStage))
        {
            // Small stages get a string of warm bulbs' worth of light, no more.
            var high = deck == StageKind.Bandstand ? 3.2f : 3.6f;
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
            var spot = new SpotLight3D { LightColor = colours[i], SpotRange = 14f * k, SpotAngle = 32f, ShadowEnabled = Graphics.Shadows, ShadowBias = 0.1f, ShadowBlur = 1.5f, Position = from };
            _shadowSpots.Add(spot);
            Add(spot, 9f * k);
            spot.LookAtFromPosition(from, new Vector3(x * 0.8f, 1.3f * k, 0.2f), Vector3.Up);

            // A beam through the haze, pointing out over the crowd: bright at the lamp, fading to
            // nothing at the far end, and soft at the edges.
            var material = new ShaderMaterial { Shader = BeamShader };
            material.SetShaderParameter("colour", colours[i]);
            material.SetShaderParameter("strength", 0f);
            var tilt = new Vector3(-52, (i - 2) * 16, 0);
            _rig.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.08f * k, BottomRadius = 1.5f * k, Height = 12f * k, RadialSegments = 20, Rings = 1, CapTop = false, CapBottom = false },
                MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                RotationDegrees = tilt,
                Position = from + (Basis.FromEuler(tilt * (Mathf.Pi / 180f)) * new Vector3(0, -6f * k, 0)) + new Vector3(0, 0.2f, 0.5f),
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
