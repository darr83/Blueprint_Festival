using System;
using System.Linq;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Mock-up pictures of three looks for the big screens, run with: godot --path game -- --screen-mock=DIR
/// (needs a display). Each look shows a small and a medium screen beside the stage and a large one
/// further back, all showing the live front-of-stage camera.
/// </summary>
public partial class ScreenMockShots : Node
{
    private static readonly (ScreenLook Look, string Shot)[] Scenes =
    {
        (ScreenLook.Scaffold, "A1_scaffold_towers_wide"), (ScreenLook.Scaffold, "A2_scaffold_towers_close"), (ScreenLook.Scaffold, "A3_scaffold_towers_night"),
        (ScreenLook.Trailer, "B1_screen_trailers_wide"), (ScreenLook.Trailer, "B2_screen_trailers_close"), (ScreenLook.Trailer, "B3_screen_trailers_night"),
        (ScreenLook.Programme, "C1_programme_frames_wide"), (ScreenLook.Programme, "C2_programme_frames_close"), (ScreenLook.Programme, "C3_programme_frames_night"),
    };

    private static readonly (string Name, float Width, float Height, int Speakers)[] Sizes =
    {
        ("Small", 4.0f, 2.25f, 1), ("Medium", 6.0f, 3.4f, 2), ("Large", 9.0f, 5.0f, 4),
    };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;
    private Camera3D? _shotCamera;

    public ScreenMockShots(string directory)
    {
        _directory = directory;
    }

    public ScreenMockShots() : this("shots")
    {
    }

    public override void _Ready()
    {
        DirAccess.MakeDirRecursiveAbsolute(_directory);
        Callable.From(NextScene).CallDeferred();
    }

    public override void _Process(double delta)
    {
        if (_scene < 0 || _scene >= Scenes.Length)
        {
            return;
        }

        _frame++;
        if (!_ready)
        {
            if (GetTree().CurrentScene is FestivalScene f && f.IsNodeReady() && f.Game == GameManager.Instance.Game)
            {
                Arrange(f);
                f.Site.Rebuild();
                f.Litter.Refresh();
                f.Camera.Snap();
                f.Crowd.Snap();
                f.Staff.Snap();
                f.Weather.Settle();
                f.Sky.Apply(f.Game, 0, settle: true);
                f.SetHudShown(false);
                _ready = true;
                _frame = 0;
            }

            return;
        }

        if (_frame == 2 && GetTree().CurrentScene is FestivalScene again)
        {
            again.Crowd.Snap();
            _shotCamera?.MakeCurrent();
        }

        if (_frame == 40)
        {
            var path = $"{_directory}/{Scenes[_scene].Shot}.png";
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"[screens] {path}");
            NextScene();
        }
    }

    private void NextScene()
    {
        _scene++;
        _ready = false;
        _frame = 0;
        if (_scene >= Scenes.Length)
        {
            GD.Print("[screens] Done.");
            GetTree().Quit(0);
            return;
        }

        var only = OS.GetEnvironment("SHOTS_ONLY");
        if (only.Length > 0 && !Scenes[_scene].Shot.Contains(only, StringComparison.Ordinal))
        {
            NextScene();
            return;
        }

        GameManager.Instance.Open(FestivalGame.StartYear(null, new YearSetup(4, 1500, 4000 + (1500 * 14), false, 82)));
    }

    private void Arrange(FestivalScene festival)
    {
        var game = festival.Game;
        var (look, shot) = Scenes[_scene];
        var night = shot.Contains("night", StringComparison.Ordinal);
        festival.Camera.ShowWholeField();
        festival.Year.PickHeadliner(game.Headliners.ToList().FindIndex(h => h.Stars == game.Headliners.Max(o => o.Stars)));
        festival.Year.StartBuilding();
        GrowthShots.Build(game);
        game.OpenGates();
        WeatherShots.RunTo(game, night ? 21.4 : 15.0);
        game.Clock.SetSpeed(GameSpeed.Paused);

        var stage = game.Site.Stages[0];
        var r = stage.Rect;
        const float t = (float)FieldMap.TileMetres;
        var left = r.X * t;
        var right = (r.X + r.Width) * t;
        var front = (r.Y + r.Height) * t;
        var middle = (left + right) / 2f;

        // The live front-of-stage camera, drawn into a picture every screen shows.
        var feed = new SubViewport { Name = "ScreenFeed", Size = new Vector2I(640, 360), RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        var at = StageCameras.Of(stage, StageShot.Front);
        var cam = new Camera3D { Fov = (float)at.FieldOfView, Far = 400f, Current = true };
        feed.AddChild(cam);
        festival.AddChild(feed);
        cam.GlobalTransform = new Transform3D(Basis.Identity, new Vector3((float)at.EyeX, (float)at.EyeHeight, (float)at.EyeY))
            .LookingAt(new Vector3((float)at.LookX, (float)at.LookHeight, (float)at.LookY), Vector3.Up);
        var picture = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoTexture = feed.GetTexture(),
        };

        var spots = new[]
        {
            new Vector3(left - 4.5f, 0, front - 2f),
            new Vector3(right + 5.5f, 0, front - 1.5f),
            new Vector3(right + 4f, 0, front + 30f),
        };
        var turns = new[] { 8f, -8f, -20f };
        var rig = new Node3D { Name = "MockScreens" };
        festival.AddChild(rig);
        for (var i = 0; i < Sizes.Length; i++)
        {
            var (label, w, h, speakers) = Sizes[i];
            var builder = new MeshBuilder();
            ScreenModels.Add(builder, look, w, h, speakers);
            var node = builder.BuildNode($"{label}Screen");
            var holder = new Node3D { Position = spots[i], RotationDegrees = new Vector3(0, turns[i], 0) };
            holder.AddChild(node);
            holder.AddChild(new MeshInstance3D
            {
                Mesh = new QuadMesh { Size = new Vector2(w, h) },
                MaterialOverride = picture,
                Position = ScreenModels.PictureCentre(look, h),
            });
            if (look == ScreenLook.Programme)
            {
                holder.AddChild(new Label3D
                {
                    Text = "LARKMOOR",
                    FontSize = 64,
                    PixelSize = 0.004f + (h * 0.0012f),
                    Modulate = new Color("#7A2E4A"),
                    OutlineSize = 0,
                    Position = new Vector3(0, ScreenModels.Bottom(look, h) + h + 0.6f, 0.08f),
                });
            }

            holder.AddChild(new Label3D
            {
                Text = label,
                FontSize = 48,
                PixelSize = 0.012f,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Modulate = new Color("#FFFFFF"),
                OutlineModulate = new Color("#1C1F22"),
                OutlineSize = 14,
                NoDepthTest = true,
                Position = new Vector3(0, ScreenModels.Bottom(look, h) + h + (look == ScreenLook.Programme ? 2.0f : 1.2f), 0),
            });
            rig.AddChild(holder);
        }

        // The picture's own camera.
        _shotCamera = new Camera3D { Fov = 50f, Far = 600f };
        festival.AddChild(_shotCamera);
        Vector3 eye;
        Vector3 target;
        if (shot.Contains("close", StringComparison.Ordinal))
        {
            eye = new Vector3(right - 7f, 2.6f, front + 50f);
            target = new Vector3(right + 1f, 3.6f, front + 30f);
            _shotCamera.Fov = 55f;
        }
        else
        {
            eye = new Vector3(middle + 4f, 26f, front + 66f);
            target = new Vector3(middle + 6f, 2f, front + 12f);
        }

        _shotCamera.GlobalTransform = new Transform3D(Basis.Identity, eye).LookingAt(target, Vector3.Up);
        _shotCamera.MakeCurrent();
        GD.Print($"[screens] {shot}: {game.Now:HH:mm}, watching {game.Crowd.Watching}, in {game.Crowd.ThroughGates}, queuing {game.Crowd.Queuing}, phase {game.Phase}, playing {game.Show.Playing(game.Now)?.Act.Name ?? "nobody"}");
    }
}
