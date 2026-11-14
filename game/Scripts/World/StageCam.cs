using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// The stage cameras: a perspective camera that takes over from the site view, either out front
/// looking at the band or at the back of the stage looking out at the crowd. It starts on the stage
/// that is playing, the player can move it round every stage on the site, and it hands back to the
/// site view when switched off or when there is no stage.
/// </summary>
public partial class StageCam : Node3D
{
    private Camera3D _camera = null!;
    private CameraRig _rig = null!;
    private FestivalGame _game = null!;
    private PlacedStage? _chosen;

    /// <summary>Which stage camera is on, or null for the normal view of the site.</summary>
    public StageShot? Showing { get; private set; }

    /// <summary>The stage being filmed, while a stage camera is on.</summary>
    public PlacedStageInfo? Filming { get; private set; }

    /// <summary>The stage being filmed, while a stage camera is on.</summary>
    public PlacedStage? FilmingStage { get; private set; }

    /// <summary>What the player calls the stage being filmed ("Stage 1"), or an empty string.</summary>
    public string FilmingName => FilmingStage is { } stage ? StageCameras.Name(_game, stage) : "";

    /// <summary>How many stages the cameras can go round.</summary>
    public int StageCount => _game.Site.Stages.Count;

    public Camera3D Camera => _camera;

    /// <summary>Tells whoever shows the buttons that the camera changed.</summary>
    public event System.Action? Changed;

    /// <summary>The compass direction the camera looks, in degrees, the same way as the site view's turn.</summary>
    public float Yaw => Showing is null ? _rig.Yaw : Mathf.RadToDeg(_camera.GlobalRotation.Y);

    public void Initialise(FestivalGame game, CameraRig rig)
    {
        _game = game;
        _rig = rig;
        _camera = new Camera3D
        {
            Name = "StageCamera",
            Projection = Camera3D.ProjectionType.Perspective,
            Near = 0.1f,
            Far = 700f,
            KeepAspect = Camera3D.KeepAspectEnum.Height,
            Current = false,
        };
        AddChild(_camera);
    }

    /// <summary>Switches a stage camera on, or back to the site view with null. Returns false if there is no stage to film.</summary>
    public bool Show(StageShot? shot)
    {
        // A camera switched on from the site view starts on the stage that is playing; switching
        // between the front and back cams stays on the same stage.
        if (Showing is null)
        {
            _chosen = null;
        }

        var stage = shot is null ? null : StageCameras.Pick(_game, _chosen);
        var before = Showing;
        if (stage is null)
        {
            Showing = null;
            Filming = null;
            FilmingStage = null;
            _chosen = null;
            _rig.Camera.MakeCurrent();
        }
        else
        {
            Showing = shot;
            _chosen = stage;
            Place();
            _camera.MakeCurrent();
        }

        if (before != Showing)
        {
            Changed?.Invoke();
        }

        return shot is null || stage is not null;
    }

    /// <summary>Pressing a camera's button: on, or off again if it was already on.</summary>
    public bool Toggle(StageShot shot) => Show(Showing == shot ? null : shot);

    /// <summary>Moves the stage camera that is on to the next stage round. Returns false if no camera is on.</summary>
    public bool NextStage()
    {
        if (Showing is null)
        {
            return false;
        }

        var before = FilmingStage;
        _chosen = StageCameras.After(_game, FilmingStage);
        Place();
        if (before != FilmingStage)
        {
            Changed?.Invoke();
        }

        return true;
    }

    public override void _Process(double delta)
    {
        if (Showing is null)
        {
            return;
        }

        if (StageCameras.Pick(_game, _chosen) is null)
        {
            // The stage was taken down: go back to the view of the site.
            Show(null);
            return;
        }

        Place();
    }

    private void Place()
    {
        if (Showing is not { } shot || StageCameras.Pick(_game, _chosen) is not { } stage)
        {
            return;
        }

        // Stay on the stage the camera is on, unless it was taken down.
        _chosen = stage;
        FilmingStage = stage;
        var at = StageCameras.Of(stage, shot);
        Filming = new PlacedStageInfo(stage.Kind, stage.X, stage.Y);
        _camera.Fov = (float)at.FieldOfView;
        var eye = new Vector3((float)at.EyeX, (float)at.EyeHeight, (float)at.EyeY);
        var look = new Vector3((float)at.LookX, (float)at.LookHeight, (float)at.LookY);
        _camera.GlobalTransform = new Transform3D(Basis.Identity, eye).LookingAt(look, Vector3.Up);
    }
}

/// <summary>Which stage a stage camera is on: its kind and corner, enough to tell two stages apart.</summary>
public readonly record struct PlacedStageInfo(StageKind Kind, int X, int Y);
