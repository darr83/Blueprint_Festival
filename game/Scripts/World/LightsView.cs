using System;
using System.Collections.Generic;
using System.Linq;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// Draws the lights the player has put up: path lights, floodlight towers and fairy lights on
/// poles. By day they are just posts and bulbs; as it gets dark they come on and light the ground.
/// </summary>
public partial class LightsView : Node3D
{
    private const float T = (float)FieldMap.TileMetres;

    /// <summary>The most real lamps lit at once, so a site covered in lights still draws quickly. The rest still glow.</summary>
    public const int MostLamps = 160;

    private static readonly Color Steel = new("#8E979C");
    private static readonly Color DarkSteel = new("#4A5358");
    private static readonly Color Wood = new("#8A6A45");
    private static readonly Color PostGreen = new("#2F4A3A");
    private static readonly Color WarmBulb = new("#FFE2A6");
    private static readonly Color CoolBulb = new("#F2F7FF");
    private static readonly Color[] FairyColours = { new("#E94F7B"), new("#F6B93B"), new("#FFF1C9"), new("#2FD7E8"), new("#8BD46A") };

    private readonly List<(Light3D Light, float Full)> _lamps = new();
    private FestivalGame _game = null!;
    private StandardMaterial3D _glow = null!;
    private Node3D _rig = null!;
    private bool _stale;

    /// <summary>How many path lights and floodlights are drawn.</summary>
    public int LightCount { get; private set; }

    /// <summary>How many strings of fairy lights are drawn.</summary>
    public int StringCount { get; private set; }

    /// <summary>How many fairy light poles are drawn: a pole two strings share is drawn once.</summary>
    public int PoleCount { get; private set; }

    /// <summary>How many real lamps there are to light the ground.</summary>
    public int LampCount => _lamps.Count;

    /// <summary>How brightly the lights are on: 0 by day, 1 at night.</summary>
    public float Strength { get; private set; }

    public void Initialise(FestivalGame game)
    {
        _game = game;
        _glow = GlowMaterial();
        game.Site.Changed += MarkStale;
        Rebuild();
    }

    public override void _ExitTree() => _game.Site.Changed -= MarkStale;

    private void MarkStale() => _stale = true;

    /// <summary>The material of bulbs and lamps: it shows its own colour whatever the light, dimmer by day.</summary>
    public static StandardMaterial3D GlowMaterial() => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        VertexColorUseAsAlbedo = true,
        VertexColorIsSrgb = true,
        AlbedoColor = new Color(0.72f, 0.72f, 0.72f),
    };

    public override void _Process(double delta)
    {
        if (_stale)
        {
            Rebuild();
        }

        Sync();
    }

    /// <summary>Brings the lights up as the daylight goes.</summary>
    public void Sync()
    {
        // They are switched on a little before it is fully dark.
        Strength = Mathf.Clamp((1f - (float)_game.Light) * 1.4f, 0f, 1f);
        var grey = Mathf.Lerp(0.72f, 1f, Strength);
        _glow.AlbedoColor = new Color(grey, grey, grey);
        _rig.Visible = Strength > 0.01f;
        foreach (var (light, full) in _lamps)
        {
            light.LightEnergy = full * Strength;
        }
    }

    public static Vector3 Middle(TilePoint p) => new((p.X + 0.5f) * T, 0, (p.Y + 0.5f) * T);

    /// <summary>A lamp on a short post.</summary>
    public static void AddPathLight(MeshBuilder frame, MeshBuilder glow, Vector3 at)
    {
        frame.Tube(at + new Vector3(0, 0.03f, 0), new Vector3(0.22f, 0.06f, 0.22f), DarkSteel);
        frame.Tube(at + new Vector3(0, 0.55f, 0), new Vector3(0.07f, 1.1f, 0.07f), PostGreen);
        frame.Cone(at + new Vector3(0, 1.36f, 0), new Vector3(0.34f, 0.08f, 0.34f), PostGreen);
        glow.Ball(at + new Vector3(0, 1.2f, 0), new Vector3(0.24f, 0.26f, 0.24f), WarmBulb);
    }

    /// <summary>How high a floodlight's lamps are, in metres.</summary>
    public const float TowerHeight = 7.5f;

    /// <summary>A steel tower with a bank of lamps on top, shining out all round.</summary>
    public static void AddFloodlight(MeshBuilder frame, MeshBuilder glow, Vector3 at)
    {
        // A weighted base, four legs with braces, and a head with two lamps on each side.
        frame.Box(at + new Vector3(0, 0.12f, 0), new Vector3(1.1f, 0.24f, 1.1f), DarkSteel);
        const float half = 0.22f;
        foreach (var (sx, sz) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
        {
            frame.Box(at + new Vector3(sx * half, TowerHeight / 2f, sz * half), new Vector3(0.06f, TowerHeight, 0.06f), Steel);
        }

        for (var y = 0.9f; y < TowerHeight; y += 1.1f)
        {
            frame.Box(at + new Vector3(0, y, half), new Vector3((half * 2) + 0.06f, 0.04f, 0.04f), Steel);
            frame.Box(at + new Vector3(0, y, -half), new Vector3((half * 2) + 0.06f, 0.04f, 0.04f), Steel);
            frame.Box(at + new Vector3(half, y, 0), new Vector3(0.04f, 0.04f, (half * 2) + 0.06f), Steel);
            frame.Box(at + new Vector3(-half, y, 0), new Vector3(0.04f, 0.04f, (half * 2) + 0.06f), Steel);
        }

        var top = at + new Vector3(0, TowerHeight + 0.25f, 0);
        frame.Box(top, new Vector3(1.0f, 0.5f, 1.0f), DarkSteel);
        foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            foreach (var side in new[] { -0.24f, 0.24f })
            {
                var face = top + new Vector3((dx * 0.51f) + (dz * side), 0, (dz * 0.51f) + (dx * side));
                glow.Box(face, new Vector3(dx != 0 ? 0.04f : 0.38f, 0.36f, dz != 0 ? 0.04f : 0.38f), CoolBulb);
            }
        }
    }

    /// <summary>How high a fairy light pole is, in metres.</summary>
    public const float PoleHeight = 3f;

    public static void AddPole(MeshBuilder frame, Vector3 at)
    {
        frame.Tube(at + new Vector3(0, PoleHeight / 2f, 0), new Vector3(0.09f, PoleHeight, 0.09f), Wood);
        frame.Ball(at + new Vector3(0, PoleHeight, 0), 0.13f, Wood);
    }

    /// <summary>Where a string hangs between two pole tops, part of the way along: it dips in the middle.</summary>
    public static Vector3 Hang(Vector3 from, Vector3 to, float along)
    {
        var dip = Mathf.Min(0.5f, from.DistanceTo(to) * 0.09f);
        return from.Lerp(to, along) + new Vector3(0, (PoleHeight - 0.08f) - (dip * 4f * along * (1f - along)), 0);
    }

    /// <summary>The string between two poles: a thin wire with a coloured bulb every 40 cm or so.</summary>
    public static void AddString(MeshBuilder frame, MeshBuilder glow, Vector3 from, Vector3 to, int seed)
    {
        var length = from.DistanceTo(to);
        var bulbs = Math.Max(2, (int)Mathf.Round(length / 0.4f));
        var last = Hang(from, to, 0);
        for (var i = 1; i <= bulbs; i++)
        {
            var next = Hang(from, to, (float)i / bulbs);
            var d = next - last;
            var yaw = Mathf.RadToDeg(Mathf.Atan2(d.X, d.Z));
            var pitch = -Mathf.RadToDeg(Mathf.Asin(d.Y / Mathf.Max(0.001f, d.Length())));
            frame.Box((last + next) / 2f, new Vector3(0.018f, 0.018f, d.Length()), DarkSteel, new Vector3(pitch, yaw, 0));
            if (i < bulbs)
            {
                glow.Ball(next + new Vector3(0, -0.07f, 0), new Vector3(0.1f, 0.13f, 0.1f), FairyColours[(i + seed) % FairyColours.Length]);
            }

            last = next;
        }
    }

    /// <summary>A whole string of fairy lights with its poles, for the see-through copy that follows the mouse.</summary>
    public static Node3D Fairy(FairyString fairy)
    {
        var (frame, glow) = (new MeshBuilder(), new MeshBuilder());
        var poles = fairy.Poles();
        foreach (var p in poles)
        {
            AddPole(frame, Middle(p));
        }

        for (var i = 1; i < poles.Count; i++)
        {
            AddString(frame, glow, Middle(poles[i - 1]), Middle(poles[i]), i);
        }

        return Pair(frame, glow, GlowMaterial(), "FairyLights");
    }

    /// <summary>One path light or floodlight, for the see-through copy that follows the mouse.</summary>
    public static Node3D Single(LightKind kind)
    {
        var (frame, glow) = (new MeshBuilder(), new MeshBuilder());
        if (kind == LightKind.Floodlight)
        {
            AddFloodlight(frame, glow, Vector3.Zero);
        }
        else
        {
            AddPathLight(frame, glow, Vector3.Zero);
        }

        return Pair(frame, glow, GlowMaterial(), kind.ToString());
    }

    private static Node3D Pair(MeshBuilder frame, MeshBuilder glow, StandardMaterial3D glowMaterial, string name)
    {
        var node = new Node3D { Name = name };
        if (!frame.IsEmpty)
        {
            node.AddChild(frame.BuildNode("Frames"));
        }

        if (!glow.IsEmpty)
        {
            var bulbs = glow.BuildNode("Bulbs");
            bulbs.MaterialOverride = glowMaterial;
            bulbs.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            node.AddChild(bulbs);
        }

        return node;
    }

    public void Rebuild()
    {
        _stale = false;
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        _lamps.Clear();
        _rig = new Node3D { Name = "Lamps" };
        var site = _game.Site;
        var (frame, glow) = (new MeshBuilder(), new MeshBuilder());

        // Floodlights first, so that if there are too many lamps it is the small ones that go without.
        foreach (var light in site.Lights.OrderByDescending(l => l.Kind == LightKind.Floodlight))
        {
            var at = Middle(light.Tile);
            if (light.Kind == LightKind.Floodlight)
            {
                AddFloodlight(frame, glow, at);
                Lamp(new OmniLight3D { LightColor = new Color("#EEF4FF"), OmniRange = 25f, OmniAttenuation = 0.7f, Position = at + new Vector3(0, TowerHeight, 0) }, 5f);
            }
            else
            {
                AddPathLight(frame, glow, at);
                Lamp(new OmniLight3D { LightColor = new Color("#FFD9A0"), OmniRange = 6.5f, Position = at + new Vector3(0, 1.5f, 0) }, 1.6f);
            }
        }

        var poles = new HashSet<TilePoint>();
        var strings = 0;
        foreach (var fairy in site.FairyLights)
        {
            var own = fairy.Poles();
            foreach (var p in own)
            {
                if (poles.Add(p))
                {
                    AddPole(frame, Middle(p));
                }
            }

            for (var i = 1; i < own.Count; i++)
            {
                var (a, b) = (Middle(own[i - 1]), Middle(own[i]));
                AddString(frame, glow, a, b, strings + i);
                Lamp(new OmniLight3D { LightColor = new Color("#FFD2A8"), OmniRange = 7f, Position = Hang(a, b, 0.5f) }, 1.3f);
            }

            strings++;
        }

        AddChild(Pair(frame, glow, _glow, "Lights"));
        AddChild(_rig);
        LightCount = site.Lights.Count;
        StringCount = strings;
        PoleCount = poles.Count;
        Sync();
    }

    private void Lamp(OmniLight3D light, float full)
    {
        if (_lamps.Count >= MostLamps)
        {
            return;
        }

        light.ShadowEnabled = false;
        light.LightEnergy = 0;
        _rig.AddChild(light);
        _lamps.Add((light, full));
    }
}
