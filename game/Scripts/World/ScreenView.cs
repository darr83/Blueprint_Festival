using System.Collections.Generic;
using System.Linq;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// Draws the big screens the player has put up, each on its scaffold tower with its speakers, and
/// shows on each the live front-of-stage camera of the nearest stage. Each stage being shown has
/// one camera feeding every screen that shows it.
/// </summary>
public partial class ScreenView : Node3D
{
    /// <summary>The render layer the pictures are on: the feed cameras leave it out, so a screen never films itself.</summary>
    public const uint PictureLayer = 1u << 10;

    /// <summary>How big the picture from a stage camera is, in pixels.</summary>
    public static readonly Vector2I FeedSize = new(512, 288);

    private static readonly Color NoSignal = new("#14202A");

    private readonly Dictionary<PlacedStage, (SubViewport Feed, Camera3D Camera, StandardMaterial3D Picture)> _feeds = new();
    private FestivalGame _game = null!;
    private Node3D _screens = null!;
    private StandardMaterial3D _blank = null!;
    private bool _stale = true;

    /// <summary>How many screens are drawn.</summary>
    public int ScreenCount { get; private set; }

    /// <summary>How many stage cameras are feeding screens.</summary>
    public int FeedCount => _feeds.Count;

    public void Initialise(FestivalGame game)
    {
        _game = game;
        _blank = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = NoSignal };
        _screens = new Node3D { Name = "Screens" };
        AddChild(_screens);
        game.Site.Changed += MarkStale;
        Rebuild();
    }

    public override void _ExitTree() => _game.Site.Changed -= MarkStale;

    private void MarkStale() => _stale = true;

    public override void _Process(double delta)
    {
        if (_stale)
        {
            Rebuild();
        }
    }

    /// <summary>The stage a screen shows: the nearest one, or null with no stage up.</summary>
    public static PlacedStage? StageFor(FestivalGame game, PlacedScreen screen)
    {
        var (sx, sy, _, _) = SiteLayout.Front(screen.Rect, screen.Facing);
        return game.Site.Stages
            .OrderBy(s =>
            {
                var (fx, fy, _, _) = RunningOrder.Front(s);
                return ((fx - sx) * (fx - sx)) + ((fy - sy) * (fy - sy));
            })
            .FirstOrDefault();
    }

    /// <summary>A screen of one size with a blank picture, as the build tool shows it under the pointer.</summary>
    public static Node3D Model(ScreenKind kind, Material? picture = null)
    {
        var info = BigScreens.Of(kind);
        var (w, h) = ((float)info.PictureWidth, (float)info.PictureHeight);
        var builder = new MeshBuilder();
        ScreenModels.Add(builder, w, h, info.Speakers);
        var holder = new Node3D { Name = $"{kind}Screen" };

        // The tower stands a little back from the front of its ground, so its subwoofers sit on it.
        var body = builder.BuildNode("Tower", new Vector3(0, 0, 0.3f));
        holder.AddChild(body);
        body.AddChild(new MeshInstance3D
        {
            Name = "Picture",
            Mesh = new QuadMesh { Size = new Vector2(w, h) },
            MaterialOverride = picture ?? new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = NoSignal },
            Position = ScreenModels.PictureCentre(h),
            Layers = PictureLayer,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        return holder;
    }

    /// <summary>Draws every screen again, with a camera on each stage that is being shown.</summary>
    public void Rebuild()
    {
        _stale = false;
        foreach (var child in _screens.GetChildren())
        {
            _screens.RemoveChild(child);
            child.QueueFree();
        }

        var shown = new HashSet<PlacedStage>();
        foreach (var screen in _game.Site.Screens)
        {
            var stage = StageFor(_game, screen);
            Material picture = _blank;
            if (stage is not null)
            {
                shown.Add(stage);
                picture = FeedFor(stage).Picture;
            }

            var node = Model(screen.Kind, picture);
            node.Position = SiteView.Middle(screen.Rect);
            node.RotationDegrees = new Vector3(0, SiteView.Turn(screen.Facing), 0);
            _screens.AddChild(node);
        }

        foreach (var gone in _feeds.Keys.Where(s => !shown.Contains(s)).ToList())
        {
            _feeds[gone].Feed.QueueFree();
            _feeds.Remove(gone);
        }

        ScreenCount = _game.Site.Screens.Count;
    }

    private (SubViewport Feed, Camera3D Camera, StandardMaterial3D Picture) FeedFor(PlacedStage stage)
    {
        if (_feeds.TryGetValue(stage, out var feed))
        {
            return feed;
        }

        var viewport = new SubViewport
        {
            Name = "StageFeed",
            Size = FeedSize,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        };
        var camera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Perspective,
            Near = 0.1f,
            Far = 400f,
            CullMask = 0xFFFFF & ~PictureLayer,
        };
        viewport.AddChild(camera);
        AddChild(viewport);
        camera.MakeCurrent();
        Aim(camera, stage);
        var picture = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoTexture = viewport.GetTexture(),
        };
        feed = (viewport, camera, picture);
        _feeds[stage] = feed;
        return feed;
    }

    private static void Aim(Camera3D camera, PlacedStage stage)
    {
        var at = StageCameras.Of(stage, StageShot.Front);
        camera.Fov = (float)at.FieldOfView;
        var eye = new Vector3((float)at.EyeX, (float)at.EyeHeight, (float)at.EyeY);
        var look = new Vector3((float)at.LookX, (float)at.LookHeight, (float)at.LookY);
        camera.GlobalTransform = new Transform3D(Basis.Identity, eye).LookingAt(look, Vector3.Up);
    }
}
