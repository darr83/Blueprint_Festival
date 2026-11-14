using System;
using BlueprintFestival.Simulation.World;
using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// The view of the site: a camera with no perspective, tilted down 30 degrees, that looks from one
/// of four corners. Q and E turn it a quarter turn, WASD or the arrow keys move it, the wheel zooms.
/// </summary>
public partial class CameraRig : Node3D
{
    /// <summary>The camera tilts down 30 degrees: the classic 2:1 look shared with Blueprint Hospital.</summary>
    public const float PitchDegrees = 30f;

    public const float MinSize = 8f;
    public const float MaxSize = 260f;

    private Camera3D _camera = null!;
    private FieldMap _map = null!;
    private Vector3 _target;
    private float _size = 60f;
    private float _shownSize = 60f;
    private float _yaw = 45f;

    /// <summary>Which of the four corners the view is from: 0 to 3.</summary>
    public int Quarter { get; private set; }

    public Camera3D Camera => _camera;

    /// <summary>The point on the ground in the middle of the screen.</summary>
    public Vector3 Target => _target;

    /// <summary>How many metres of the world fit down the screen. Smaller is closer.</summary>
    public float Size => _size;

    /// <summary>The compass direction the view is turned to, in degrees, eased while turning.</summary>
    public float Yaw => _yaw;

    public void Initialise(FieldMap map)
    {
        _map = map;
        _camera = new Camera3D
        {
            Name = "Camera",
            Projection = Camera3D.ProjectionType.Orthogonal,
            Near = 0.5f,
            Far = 900f,
            KeepAspect = Camera3D.KeepAspectEnum.Height,
            Current = true,
        };
        AddChild(_camera);
        ShowWholeField();
        Snap();
    }

    /// <summary>Turns the view by quarter turns: 1 is clockwise seen from above.</summary>
    public void Turn(int quarters) => Quarter = (((Quarter + quarters) % 4) + 4) % 4;

    public void ZoomBy(float factor) => _size = Mathf.Clamp(_size * factor, MinSize, MaxSize);

    public void ShowWholeField()
    {
        var field = _map.HedgeLine;
        _target = new Vector3(
            (float)((field.X + (field.Width / 2.0)) * FieldMap.TileMetres),
            0,
            (float)((field.Y + (field.Height / 2.0) + 10) * FieldMap.TileMetres));
        _size = 62f;
    }

    /// <summary>Looks at a point on the ground, given in metres.</summary>
    public void LookAt(float xMetres, float zMetres, float size)
    {
        _target = new Vector3(xMetres, 0, zMetres);
        _size = Mathf.Clamp(size, MinSize, MaxSize);
    }

    /// <summary>Jumps straight to where the view is heading, with no easing. Used for screenshots.</summary>
    public void Snap()
    {
        _yaw = TargetYaw;
        _shownSize = _size;
        Place();
    }

    private float TargetYaw => 45f + (90f * Quarter);

    public override void _Process(double delta)
    {
        var step = (float)delta;
        var move = Vector2.Zero;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) move.Y -= 1;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) move.Y += 1;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) move.X -= 1;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) move.X += 1;
        if (move != Vector2.Zero)
        {
            Pan(move.Normalized() * _size * 0.9f * step);
        }

        // Ease the turn the short way round, and the zoom.
        var diff = Mathf.Wrap(TargetYaw - _yaw, -180f, 180f);
        _yaw = Mathf.Abs(diff) < 0.05f ? TargetYaw : _yaw + (diff * Mathf.Min(1f, step * 9f));
        _shownSize = Mathf.Lerp(_shownSize, _size, Mathf.Min(1f, step * 12f));
        Place();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp }:
                ZoomBy(0.9f);
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown }:
                ZoomBy(1.1f);
                break;
            case InputEventMouseMotion motion when (motion.ButtonMask & MouseButtonMask.Middle) != 0:
                var perPixel = _shownSize / GetViewport().GetVisibleRect().Size.Y;
                Pan(new Vector2(-motion.Relative.X * perPixel, -motion.Relative.Y * perPixel / Mathf.Sin(Mathf.DegToRad(PitchDegrees))));
                break;
            case InputEventKey { Pressed: true, Echo: false } key:
                if (key.Keycode == Key.Q) Turn(-1);
                else if (key.Keycode == Key.E) Turn(1);
                else if (key.Keycode == Key.Home) ShowWholeField();
                break;
        }
    }

    /// <summary>Moves the view across the ground: X is to the right of the screen, Y is towards the viewer.</summary>
    private void Pan(Vector2 screen)
    {
        var yaw = Mathf.DegToRad(_yaw);
        var right = new Vector3(Mathf.Cos(yaw), 0, -Mathf.Sin(yaw));
        var towards = new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw));
        _target += (right * screen.X) + (towards * screen.Y);
        _target.X = Mathf.Clamp(_target.X, -20f, (float)_map.WidthMetres + 20f);
        _target.Z = Mathf.Clamp(_target.Z, -20f, (float)_map.HeightMetres + 20f);
    }

    private void Place()
    {
        var yaw = Mathf.DegToRad(_yaw);
        var pitch = Mathf.DegToRad(PitchDegrees);
        var back = new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch));
        _camera.Size = _shownSize;
        _camera.Transform = new Transform3D(Basis.Identity, _target + (back * 300f)).LookingAt(_target, Vector3.Up);
    }
}
