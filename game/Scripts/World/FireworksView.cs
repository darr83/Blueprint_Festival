using System;
using System.Collections.Generic;
using System.Linq;
using BlueprintFestival.Audio;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.Sound;
using BlueprintFestival.Simulation.World;
using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// The launch pad with its mortar tubes, the Heras fence round its safe zone, the pink safe zone
/// while the player is planning, and the fireworks themselves: when a show's time comes they go up
/// from the pad, burst over the field and light up the crowd. A show plays over 30 real seconds
/// whatever the game speed, so it can be watched.
/// </summary>
public partial class FireworksView : Node3D
{
    private const float T = (float)FieldMap.TileMetres;
    private const int MostSparks = 4000;

    /// <summary>Sparks are drawn bigger than life, so a show reads from the usual height of the camera.</summary>
    private const float SparkScale = 3f;

    private FestivalGame _game = null!;
    private Node3D _built = null!;
    private MeshInstance3D _zone = null!;
    private StandardMaterial3D _zoneMaterial = null!;
    private MultiMeshInstance3D _sparks = null!;
    private OmniLight3D _flash = null!;
    private readonly List<Spark> _live = new();
    private readonly HashSet<string> _fired = new();
    private readonly Random _random = new(5);
    private FireworkShow? _playing;
    private bool _bigPad;
    private double _time;
    private bool _stale = true;
    private bool _oohed;

    private sealed class Spark
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public Color Colour;
        public float Life;
        public float MaxLife;
        public float Size;
        public float Gravity;
        public float Drag;
        public Action<Spark>? Burst;
    }

    /// <summary>True while a show (or a preview) is going off.</summary>
    public bool Playing => _playing is not null;

    /// <summary>How far into the show going off it has got, in seconds.</summary>
    public double ShowTime => _time;

    /// <summary>How many sparks are in the sky.</summary>
    public int SparkCount => _live.Count;

    /// <summary>Whether the pink safe zone is drawn on the ground: while the mixer is open or the pad is being placed.</summary>
    public bool ZoneShown { get; set; }

    /// <summary>True if the launch pad is drawn.</summary>
    public bool PadDrawn => _built.GetChildCount() > 0;

    public void Initialise(FestivalGame game)
    {
        _game = game;
        _built = new Node3D { Name = "Pad" };
        AddChild(_built);

        _zoneMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(0.925f, 0.31f, 0.5f, 0.22f),
        };
        _zone = new MeshInstance3D { Name = "SafeZone", Mesh = new BoxMesh { Size = Vector3.One }, MaterialOverride = _zoneMaterial, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_zone);

        var spark = new QuadMesh { Size = Vector2.One };
        var glow = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoTexture = Dot(),
            NoDepthTest = false,
            DisableReceiveShadows = true,
        };
        spark.Material = glow;
        _sparks = new MultiMeshInstance3D
        {
            Name = "Sparks",
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = spark, InstanceCount = MostSparks, VisibleInstanceCount = 0 },
            ExtraCullMargin = 400,
        };
        AddChild(_sparks);

        _flash = new OmniLight3D { Name = "Flash", OmniRange = 120, LightEnergy = 0, ShadowEnabled = false, LightColor = new Color(1, 0.85f, 0.9f) };
        AddChild(_flash);

        game.Site.Changed += MarkStale;
        game.FireworksStarted += OnStarted;
        Rebuild();
    }

    public override void _ExitTree()
    {
        _game.Site.Changed -= MarkStale;
        _game.FireworksStarted -= OnStarted;
    }

    private void MarkStale() => _stale = true;

    /// <summary>A soft round dot, white in the middle, for every spark.</summary>
    private static ImageTexture Dot()
    {
        const int size = 32;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var d = new Vector2(x - (size / 2f) + 0.5f, y - (size / 2f) + 0.5f).Length() / (size / 2f);
                var a = Mathf.Clamp(1 - d, 0, 1);
                image.SetPixel(x, y, new Color(1, 1, 1, a * a));
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>A launch pad as the build tool shows it: a deck with rows of mortar tubes, sandbags and a firing box.</summary>
    public static Node3D Model(LaunchPadKind kind)
    {
        var info = LaunchPads.Of(kind);
        var side = info.Size * T;
        var b = new MeshBuilder();
        var deck = new Color("#4A4552");
        var tube = new Color("#2F4A35");
        var sand = new Color("#B7A27A");
        b.Box(new Vector3(0, 0.08f, 0), new Vector3(side, 0.16f, side), deck);
        var rows = kind == LaunchPadKind.Big ? 6 : 4;
        var step = (side - 0.8f) / (rows - 1);
        for (var i = 0; i < rows; i++)
        {
            for (var j = 0; j < rows; j++)
            {
                var tall = 0.35f + (0.1f * ((i + j) % 3));
                var at = new Vector3(-side / 2 + 0.4f + (i * step), 0.16f + (tall / 2), -side / 2 + 0.4f + (j * step));
                b.Tube(at, new Vector3(0.22f, tall, 0.22f), tube);
                b.Tube(at + new Vector3(0, tall / 2, 0), new Vector3(0.16f, 0.02f, 0.16f), new Color("#15161A"));
            }
        }

        foreach (var (x, z) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
        {
            b.Pill(new Vector3(x * (side / 2 + 0.25f), 0.12f, z * (side / 2 + 0.25f)), new Vector3(0.5f, 0.24f, 0.35f), sand, new Vector3(0, 90, 90));
        }

        // The firing box on its post, with its pink warning stripe.
        b.Box(new Vector3(side / 2 + 0.6f, 0.5f, 0), new Vector3(0.08f, 1f, 0.08f), new Color("#5A5A5A"));
        b.Box(new Vector3(side / 2 + 0.6f, 1.05f, 0), new Vector3(0.35f, 0.25f, 0.2f), new Color("#F5B72A"));
        b.Box(new Vector3(side / 2 + 0.6f, 1.05f, 0.105f), new Vector3(0.36f, 0.06f, 0.01f), new Color("#EC4F7F"));
        var holder = new Node3D { Name = $"{kind}Pad" };
        holder.AddChild(b.BuildNode("Deck"));
        return holder;
    }

    public void Rebuild()
    {
        _stale = false;
        foreach (var child in _built.GetChildren())
        {
            _built.RemoveChild(child);
            child.QueueFree();
        }

        if (_game.Site.LaunchPad is not { } pad)
        {
            return;
        }

        var model = Model(pad.Kind);
        model.Position = SiteView.Middle(pad.Rect);
        _built.AddChild(model);
        if (pad.Fenced)
        {
            var b = new MeshBuilder();
            var z = pad.Zone;
            var (l, r, top, bottom) = ((z.X + 0.5f) * T, (z.Right - 0.5f) * T, (z.Y + 0.5f) * T, (z.Bottom - 0.5f) * T);
            SiteProps.AddFence(b, FenceKind.SteelMesh, new Vector3(l, 0, top), new Vector3(r, 0, top));
            SiteProps.AddFence(b, FenceKind.SteelMesh, new Vector3(r, 0, top), new Vector3(r, 0, bottom));
            SiteProps.AddFence(b, FenceKind.SteelMesh, new Vector3(r, 0, bottom), new Vector3(l, 0, bottom));
            SiteProps.AddFence(b, FenceKind.SteelMesh, new Vector3(l, 0, bottom), new Vector3(l, 0, top));
            _built.AddChild(b.BuildNode("SafeZoneFence"));
        }
    }

    /// <summary>Plays a show from the pad straight away, as a try-out: nobody's mood changes.</summary>
    public void Preview(FireworkShow show) => Start(show);

    /// <summary>Stops whatever show is going off.</summary>
    public void StopShow()
    {
        _playing = null;
    }

    private void OnStarted(FireworkShow show, FireworkResult result)
    {
        if (result.Outcome != FireworkOutcome.CalledOff)
        {
            Start(show);
        }
    }

    private void Start(FireworkShow show)
    {
        _playing = show with { Cues = show.Cues.ToList() };
        _bigPad = _game.Site.LaunchPad?.Kind == LaunchPadKind.Big;
        _time = 0;
        _fired.Clear();
        _oohed = false;
    }

    /// <summary>Where on the pad a firework goes up from, in metres: spread over the pad by its place in the show.</summary>
    private Vector3 LaunchPoint(int cue)
    {
        var pad = _game.Site.LaunchPad;
        var middle = pad is null ? Vector3.Zero : SiteView.Middle(pad.Rect);
        var half = pad is null ? 1.5f : pad.Rect.Width * T / 2 - 0.4f;
        var across = (((cue * 37) % 7) - 3) / 3f * half;
        var along = (((cue * 53) % 5) - 2) / 2f * half;
        return middle + new Vector3(across, 0.6f, along);
    }

    private float Rnd(float low, float high) => low + ((float)_random.NextDouble() * (high - low));

    private static readonly Color[] Pinks = { new("#FF5F95"), new("#FFC2D6"), Colors.White };
    private static readonly Color[] Golds = { new("#FFD25E"), new("#FFF1B8"), new("#FFA43B") };
    private static readonly Color[] Blues = { new("#69B8FF"), new("#D7ECFF"), Colors.White };
    private static readonly Color[] Violets = { new("#A98BFF"), new("#FF8FD8") };

    private void Ring(Vector3 at, int count, Color[] colours, float speed, float life, float gravity = 4f, float drag = 0.985f, float size = 0.9f)
    {
        for (var i = 0; i < count; i++)
        {
            // Evenly over a sphere, so a burst is round from every side.
            var y = 1 - (2 * (i + 0.5f) / count);
            var r = Mathf.Sqrt(1 - (y * y));
            var a = i * 2.39996f;
            var dir = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
            Add(new Spark { Position = at, Velocity = dir * speed * Rnd(0.85f, 1.05f), Colour = colours[i % colours.Length], Life = life, MaxLife = life, Size = size, Gravity = gravity, Drag = drag });
        }

        Flash(at, colours[0], count / 40f);
    }

    private void Shell(Vector3 from, float height, Action<Spark> burst, string launch = Sounds.FireworkLaunch)
    {
        // Faster than a real shell, so the burst comes soon after the thud.
        const float gravity = 22f;
        Add(new Spark { Position = from, Velocity = new Vector3(Rnd(-1, 1), Mathf.Sqrt(2 * gravity * height), Rnd(-1, 1)), Colour = new Color("#FFE7B0"), Life = 99, MaxLife = 99, Size = 1.1f, Gravity = gravity, Drag = 1, Burst = burst });
        SoundBoard.Cue(launch);
    }

    /// <summary>A shell bursting: its stars, and the bang that goes with that kind.</summary>
    private static void Boom(string sound, Action stars)
    {
        stars();
        SoundBoard.Cue(sound);
    }

    private void Add(Spark spark)
    {
        if (_live.Count < MostSparks)
        {
            _live.Add(spark);
        }
    }

    private void Flash(Vector3 at, Color colour, float strength)
    {
        _flash.Position = at;
        _flash.LightColor = colour;
        _flash.LightEnergy = Math.Max(_flash.LightEnergy, Mathf.Clamp(strength * 2, 1f, 5f));
    }

    /// <summary>Lets off whatever in the show is due between two moments, in seconds into the show.</summary>
    private void Emit(int index, FireworkCue cue, double from, double to)
    {
        var info = Fireworks.Of(cue.Kind);
        if (info.BigPadOnly && !_bigPad)
        {
            return;
        }

        var (rel0, rel1) = (from - cue.At, to - cue.At);
        if (rel1 < 0 || rel0 > info.Seconds)
        {
            return;
        }

        var at = LaunchPoint(index);
        bool Once(string key) => _fired.Add($"{index}:{key}");
        void Every(double step, Action<int> fire, string tag = "")
        {
            for (var k = (int)Math.Ceiling(Math.Max(0, rel0) / step); k * step < Math.Min(rel1, info.Seconds); k++)
            {
                if (Once(tag + k))
                {
                    fire(k);
                }
            }
        }

        switch (cue.Kind)
        {
            case FireworkKind.Fountain:
                for (var i = 0; i < 3; i++)
                {
                    Add(new Spark { Position = at, Velocity = new Vector3(Rnd(-1.6f, 1.6f), Rnd(8, 11), Rnd(-1.6f, 1.6f)), Colour = Golds[i], Life = Rnd(0.8f, 1.3f), MaxLife = 1.3f, Size = 0.35f, Gravity = 9.8f, Drag = 0.99f });
                }

                if (Once("s"))
                {
                    SoundBoard.Cue(Sounds.FireworkFountain);
                }

                break;
            case FireworkKind.GroundMine:
                if (Once("m"))
                {
                    for (var i = 0; i < 46; i++)
                    {
                        Add(new Spark { Position = at, Velocity = new Vector3(Rnd(-4, 4), Rnd(12, 19), Rnd(-4, 4)), Colour = Pinks[i % 3], Life = Rnd(0.9f, 1.5f), MaxLife = 1.5f, Size = 0.6f, Gravity = 9.8f, Drag = 0.985f });
                    }

                    Flash(at + new Vector3(0, 4, 0), Pinks[0], 1);
                    SoundBoard.Cue(Sounds.FireworkMine);
                }

                break;
            case FireworkKind.RomanCandle:
                Every(0.6, k =>
                {
                    Add(new Spark { Position = at, Velocity = new Vector3(Rnd(-1, 1), Rnd(17, 20), Rnd(-1, 1)), Colour = Violets[k % 2], Life = 1.5f, MaxLife = 1.5f, Size = 1.2f, Gravity = 4, Drag = 0.995f });
                    SoundBoard.Cue(Sounds.FireworkCandle);
                });
                break;
            case FireworkKind.CrackleCake:
                Every(0.25, _ => Ring(at + new Vector3(Rnd(-6, 6), Rnd(10, 16), Rnd(-6, 6)), 14, Golds, 5, 0.7f, 2, 0.98f, 0.5f));
                Every(2.5, _ => SoundBoard.Cue(Sounds.FireworkCrackle), "c");

                break;
            case FireworkKind.Rocket:
                if (Once("r"))
                {
                    Shell(at, Rnd(28, 34), s => Boom(Sounds.FireworkBurst2, () => Ring(s.Position, 40, Blues, 9, 1.4f)), Sounds.FireworkWhistle);
                }

                break;
            case FireworkKind.PeonyShell:
                if (Once("p"))
                {
                    Shell(at, Rnd(36, 40), s => Boom(Sounds.FireworkBurst, () =>
                    {
                        Ring(s.Position, 70, Pinks, 13, 1.8f);
                        Ring(s.Position, 30, new[] { Colors.White }, 6, 1.4f);
                    }));
                }

                break;
            case FireworkKind.GoldWillow:
                if (Once("w"))
                {
                    Shell(at, Rnd(38, 42), s => Boom(Sounds.FireworkWillow, () => Ring(s.Position, 80, Golds, 9, 3.2f, 1.6f, 0.975f, 0.7f)));
                }

                break;
            case FireworkKind.FinaleBarrage:
                Every(0.35, k =>
                {
                    var colours = (k % 4) switch { 0 => Pinks, 1 => Blues, 2 => Golds, _ => Violets };
                    var boom = k % 2 == 0 ? Sounds.FireworkBurst : Sounds.FireworkBurst2;
                    Shell(at + new Vector3(Rnd(-8, 8), 0, Rnd(-8, 8)), Rnd(24, 40), s => Boom(boom, () => Ring(s.Position, 50, colours, Rnd(9, 13), 1.5f)));
                });
                break;
        }
    }

    public override void _Process(double delta)
    {
        if (_stale)
        {
            Rebuild();
        }

        if (_game.Site.LaunchPad is { } pad && ZoneShown)
        {
            var z = pad.Zone;
            _zone.Visible = true;
            _zone.Position = SiteView.Middle(z) + new Vector3(0, 0.04f, 0);
            _zone.Scale = new Vector3(z.Width * T, 0.02f, z.Height * T);
            _zoneMaterial.AlbedoColor = _game.Site.WhatIsIn(z) is null ? new Color(0.925f, 0.31f, 0.5f, 0.22f) : new Color(0.76f, 0.25f, 0.18f, 0.4f);
        }
        else
        {
            _zone.Visible = false;
        }

        Tick(_game.Clock.IsPaused ? 0 : (float)Math.Min(delta, 0.05));
    }

    /// <summary>Runs the show on by some seconds in small steps whatever the clock is doing, for pictures and tests.</summary>
    public void RunFor(double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 60.0)
        {
            Tick(1 / 60f);
        }
    }

    private void Tick(float dt)
    {
        if (_playing is { } show && dt > 0)
        {
            var next = Math.Min(Fireworks.ShowSeconds + 4, _time + dt);
            for (var i = 0; i < show.Cues.Count; i++)
            {
                Emit(i, show.Cues[i], _time, next);
            }

            _time = next;
            if (!_oohed && _time > 6)
            {
                _oohed = true;
                SoundBoard.Cue(Sounds.FireworkOoh);
            }

            if (_time >= Fireworks.ShowSeconds + 4)
            {
                _playing = null;
                SoundBoard.Cue(Sounds.FireworkCheer);
            }
        }

        Step(dt);
        Draw();
        _flash.LightEnergy = Math.Max(0, _flash.LightEnergy - (dt * 3));
    }

    private void Step(float dt)
    {
        if (dt <= 0)
        {
            return;
        }

        for (var i = _live.Count - 1; i >= 0; i--)
        {
            var s = _live[i];
            s.Velocity.Y -= s.Gravity * dt;
            var drag = Mathf.Pow(s.Drag, dt * 60);
            s.Velocity *= drag;
            s.Position += s.Velocity * dt;
            s.Life -= dt;
            if (s.Burst is not null && s.Velocity.Y <= 1)
            {
                var burst = s.Burst;
                s.Burst = null;
                s.Life = 0;
                burst(s);
            }

            if (s.Life <= 0 || s.Position.Y < 0)
            {
                _live.RemoveAt(i);
            }
        }
    }

    private void Draw()
    {
        var multi = _sparks.Multimesh;
        var count = Math.Min(_live.Count, MostSparks);
        for (var i = 0; i < count; i++)
        {
            var s = _live[i];
            var fade = Mathf.Clamp(s.Life / s.MaxLife * 1.3f, 0, 1);
            multi.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.One * s.Size * SparkScale), s.Position));
            // Brighter than white, so the glow catches it.
            multi.SetInstanceColor(i, new Color(s.Colour.R * 2.2f, s.Colour.G * 2.2f, s.Colour.B * 2.2f, fade));
        }

        multi.VisibleInstanceCount = count;
    }
}
