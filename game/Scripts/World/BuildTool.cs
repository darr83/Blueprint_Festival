using System;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using Godot;

namespace BlueprintFestival.World;

/// <summary>What the mouse is building.</summary>
public enum BuildMode
{
    None,
    Stage,
    Fence,
    Gate,
    Stall,
    Staff,
    Ground,
    Remove,
}

/// <summary>
/// Building with the mouse. Pick something from the build bar, and a see-through copy follows the
/// pointer over the field: click to put a stage or gate down, drag to run a fence, R to turn it,
/// right-click or Esc to stop. Everything goes through the game's rules, which say what fits and
/// what it costs.
/// </summary>
public partial class BuildTool : Node3D
{
    private const float T = (float)FieldMap.TileMetres;

    private FestivalGame _game = null!;
    private CameraRig _camera = null!;
    private Node3D? _ghost;
    private MeshInstance3D _pad = null!;
    private StandardMaterial3D _padMaterial = null!;
    private TilePoint? _dragFrom;
    private TilePoint _hover;
    private bool _hovering;
    private string _ghostKey = "";

    public BuildMode Mode { get; private set; }

    public StageKind Stage { get; private set; }

    public FenceKind Fence { get; private set; }

    public GateKind Gate { get; private set; }

    public StallKind Stall { get; private set; }

    public StaffKind Hire { get; private set; }

    public GroundKind Ground { get; private set; }

    public Facing Facing { get; private set; } = Facing.South;

    public bool Vertical { get; private set; }

    /// <summary>What the bar should say just now: the price, or why it cannot go there.</summary>
    public string Status { get; private set; } = "";

    /// <summary>True when the last thing tried was refused.</summary>
    public bool Refused { get; private set; }

    public void Initialise(FestivalGame game, CameraRig camera)
    {
        _game = game;
        _camera = camera;
        _padMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(1, 1, 1, 0.4f),
        };
        _pad = new MeshInstance3D { Name = "Pad", Mesh = new BoxMesh { Size = Vector3.One }, MaterialOverride = _padMaterial, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_pad);
    }

    public void PickStage(StageKind kind) => Pick(BuildMode.Stage, () => Stage = kind);

    public void PickFence(FenceKind kind) => Pick(BuildMode.Fence, () => Fence = kind);

    public void PickGate(GateKind kind) => Pick(BuildMode.Gate, () => Gate = kind);

    public void PickStall(StallKind kind) => Pick(BuildMode.Stall, () => Stall = kind);

    public void PickStaff(StaffKind kind) => Pick(BuildMode.Staff, () => Hire = kind);

    public void PickGround(GroundKind kind) => Pick(BuildMode.Ground, () => Ground = kind);

    public void PickRemove() => Pick(BuildMode.Remove, () => { });

    public void Stop() => Pick(BuildMode.None, () => { });

    private void Pick(BuildMode mode, Action set)
    {
        Mode = mode;
        set();
        _dragFrom = null;
        Refused = false;
        Status = mode switch
        {
            BuildMode.Stage => "Click on the grass to put the stage up. R turns it.",
            BuildMode.Fence => "Hold the mouse button and drag to run a fence.",
            BuildMode.Gate => "Click to put the gate up. R turns it. Close the fence either side so it is the only way in.",
            BuildMode.Stall => "Click on the grass to put it up. R turns it. People queue at the front.",
            BuildMode.Staff => Hire == StaffKind.TicketStaff ? "Click a gate to put them on it. One for each lane." : "Click on the grass to post them there.",
            BuildMode.Ground => "Click a patch of grass, or hold the mouse button and drag to cover a stretch.",
            BuildMode.Remove => "Click a stage, a stall, a gate or a stretch of fence to take it down. You get the money back. Click staff to send them home.",
            _ => "",
        };
        ClearGhost();
    }

    /// <summary>Turns the thing being placed a quarter turn.</summary>
    public void Turn()
    {
        Facing = (Facing)(((int)Facing + 1) % 4);
        Vertical = !Vertical;
        ClearGhost();
    }

    private void ClearGhost()
    {
        _ghost?.QueueFree();
        _ghost = null;
        _ghostKey = "";
        _pad.Visible = false;
    }

    /// <summary>The top-left tile for a stage whose middle is under the pointer.</summary>
    private (int X, int Y) StageCorner(TilePoint at)
    {
        var rect = new PlacedStage(Stage, 0, 0, Facing).Rect;
        return (at.X - (rect.Width / 2), at.Y - (rect.Height / 2));
    }

    private (int X, int Y) StallCorner(TilePoint at)
    {
        var rect = new PlacedStall(Stall, 0, 0, Facing).Rect;
        return (at.X - (rect.Width / 2), at.Y - (rect.Height / 2));
    }

    private (int X, int Y) GateCorner(TilePoint at)
    {
        var width = Catalogue.Of(Gate).Width;
        return Vertical ? (at.X, at.Y - (width / 2)) : (at.X - (width / 2), at.Y);
    }

    /// <summary>Does what a click on a tile does with the current tool. Returns what the rules said.</summary>
    public BuildCheck ClickAt(TilePoint at)
    {
        BuildCheck result;
        switch (Mode)
        {
            case BuildMode.Stage:
            {
                var (x, y) = StageCorner(at);
                result = _game.BuildStage(Stage, x, y, Facing);
                break;
            }

            case BuildMode.Gate:
            {
                var (x, y) = GateCorner(at);
                result = _game.BuildGate(Gate, x, y, Vertical);
                break;
            }

            case BuildMode.Stall:
            {
                var (x, y) = StallCorner(at);
                result = _game.BuildStall(Stall, x, y, Facing);
                break;
            }

            case BuildMode.Staff:
                result = _game.HireStaff(Hire, at);
                Said(result, "Hired");
                return result;

            case BuildMode.Remove when _game.StaffAt(at) is { } member:
            {
                var back = _game.DismissStaff(member);
                Refused = false;
                Status = back > 0 ? $"Sent home. £{back:N0} back." : "Sent home. They are still paid for today.";
                return new BuildCheck(true, -back, null);
            }

            case BuildMode.Ground:
                return DragGround(at, at);

            case BuildMode.Remove:
            {
                var covered = _game.Site.IsEmpty(at) && _game.PatchAt(at) is var (c, r) && _game.GroundAt(c, r) is not null;
                var refund = _game.Remove(at);
                result = refund > 0 || covered || !_game.Site.IsEmpty(at) ? new BuildCheck(true, -refund, null) : BuildCheck.No("There is nothing there to take down.");
                Refused = !result.Ok;
                Status = result.Ok ? $"Taken down. £{refund:N0} back." : result.Problem!;
                return result;
            }

            default:
                return BuildCheck.No("Pick something to build first.");
        }

        Said(result, "Built");
        return result;
    }

    /// <summary>Runs a fence between two tiles with the current kind of fence.</summary>
    public BuildCheck DragFence(TilePoint from, TilePoint to)
    {
        var result = _game.BuildFence(Fence, from, to);
        Said(result, "Fence up");
        return result;
    }

    /// <summary>The patches of ground in the box between two tiles, clipped to the field.</summary>
    private (int C0, int R0, int C1, int R1)? Patches(TilePoint from, TilePoint to)
    {
        var f = _game.Field.Field;
        TilePoint Clip(TilePoint p) => new(Math.Clamp(p.X, f.X, f.Right - 1), Math.Clamp(p.Y, f.Y, f.Bottom - 1));
        if (!f.Contains(from) && !f.Contains(to))
        {
            return null;
        }

        var (a, b) = (_game.PatchAt(Clip(from))!.Value, _game.PatchAt(Clip(to))!.Value);
        return (Math.Min(a.Column, b.Column), Math.Min(a.Row, b.Row), Math.Max(a.Column, b.Column), Math.Max(a.Row, b.Row));
    }

    /// <summary>What covering the box between two tiles would cost, leaving out patches already covered the same way.</summary>
    public BuildCheck CheckGround(TilePoint from, TilePoint to)
    {
        if (Patches(from, to) is not var (c0, r0, c1, r1))
        {
            return BuildCheck.No("That is outside the field.");
        }

        var cost = 0;
        for (var r = r0; r <= r1; r++)
        {
            for (var c = c0; c <= c1; c++)
            {
                cost += _game.GroundAt(c, r) == Ground ? 0 : FestivalGame.CostOf(Ground);
            }
        }

        return cost == 0 ? new BuildCheck(false, 0, $"There is {FestivalGame.NameOf(Ground).ToLowerInvariant()} here already.")
            : !_game.CanBuild ? new BuildCheck(false, cost, "Nothing can be built just now.")
            : cost > _game.Money ? new BuildCheck(false, cost, $"That costs £{cost:N0} and there is £{_game.Money:N0} in the bank.")
            : new BuildCheck(true, cost, null);
    }

    /// <summary>Lays straw or trackway over the box between two tiles.</summary>
    public BuildCheck DragGround(TilePoint from, TilePoint to)
    {
        var result = CheckGround(from, to);
        if (result.Ok && Patches(from, to) is var (c0, r0, c1, r1))
        {
            for (var r = r0; r <= r1; r++)
            {
                for (var c = c0; c <= c1; c++)
                {
                    _game.LayGround(Ground, c, r);
                }
            }
        }

        Said(result, $"{FestivalGame.NameOf(Ground)} laid");
        return result;
    }

    private TileRect PatchRect(int c0, int r0, int c1, int r1)
    {
        var f = _game.Field.Field;
        return new TileRect(f.X + (c0 * FestivalGame.WearTiles), f.Y + (r0 * FestivalGame.WearTiles), (c1 - c0 + 1) * FestivalGame.WearTiles, (r1 - r0 + 1) * FestivalGame.WearTiles);
    }

    private void Said(BuildCheck result, string done)
    {
        Refused = !result.Ok;
        Status = result.Ok ? $"{done} for £{result.Cost:N0}." : result.Problem ?? "That cannot go there.";
    }

    /// <summary>The tile under the mouse, or null if the mouse is over a panel or off the map.</summary>
    private TilePoint? TileUnderMouse()
    {
        var viewport = GetViewport();
        if (viewport.GuiGetHoveredControl() is not null)
        {
            return null;
        }

        var mouse = viewport.GetMousePosition();
        var origin = _camera.Camera.ProjectRayOrigin(mouse);
        var direction = _camera.Camera.ProjectRayNormal(mouse);
        if (Mathf.Abs(direction.Y) < 0.001f)
        {
            return null;
        }

        var hit = origin + (direction * (-origin.Y / direction.Y));
        var tile = FieldMap.TileAt(hit.X, hit.Z);
        return _game.Field.InBounds(tile.X, tile.Y) ? tile : null;
    }

    public override void _Process(double delta)
    {
        if (Mode == BuildMode.None)
        {
            return;
        }

        if (_dragFrom is not null && !Input.IsMouseButtonPressed(MouseButton.Left) && !_hovering)
        {
            // The button came up somewhere off the field: drop the fence being dragged.
            _dragFrom = null;
        }

        var tile = TileUnderMouse();
        _hovering = tile is not null;
        if (tile is { } at)
        {
            _hover = at;
            ShowGhost(at);
        }
        else if (_dragFrom is null)
        {
            _pad.Visible = false;
            if (_ghost is not null)
            {
                _ghost.Visible = false;
            }
        }
    }

    /// <summary>Shows what would be built with the pointer on a tile, and says what it costs.</summary>
    public void ShowGhost(TilePoint at)
    {
        BuildCheck check;
        TileRect pad;
        switch (Mode)
        {
            case BuildMode.Stage:
            {
                var (x, y) = StageCorner(at);
                var stage = new PlacedStage(Stage, x, y, Facing);
                check = _game.CheckStage(Stage, x, y, Facing);
                pad = stage.Rect;
                Ghost($"stage{Stage}", () => SiteProps.Stage(Stage, _game.Name));
                _ghost!.Position = SiteView.Middle(pad);
                _ghost.RotationDegrees = new Vector3(0, SiteView.Turn(Facing), 0);
                break;
            }

            case BuildMode.Gate:
            {
                var (x, y) = GateCorner(at);
                var gate = new PlacedGate(Gate, x, y, Vertical);
                check = _game.CheckGate(Gate, x, y, Vertical);
                pad = gate.Rect;
                Ghost($"gate{Gate}", () => SiteProps.Gate(Gate, _game.Name));
                _ghost!.Position = SiteView.Middle(pad);
                _ghost.RotationDegrees = new Vector3(0, Vertical ? 90 : 0, 0);
                break;
            }

            case BuildMode.Stall:
            {
                var (x, y) = StallCorner(at);
                check = _game.CheckStall(Stall, x, y, Facing);
                pad = new PlacedStall(Stall, x, y, Facing).Rect;
                Ghost($"stall{Stall}", () => SiteProps.Stall(Stall));
                _ghost!.Position = SiteView.Middle(pad);
                _ghost.RotationDegrees = new Vector3(0, SiteView.Turn(Facing), 0);
                break;
            }

            case BuildMode.Staff:
            {
                check = _game.CheckStaff(Hire, at);
                pad = Hire == StaffKind.TicketStaff && _game.Site.GateAt(at) is { } on ? on.Rect : new TileRect(at.X - 1, at.Y - 1, 3, 3);
                Ghost($"staff{Hire}", () => SiteProps.Staff(Hire, 0));
                _ghost!.Position = new Vector3((at.X + 0.5f) * T, 0, (at.Y + 0.5f) * T);
                _ghost.RotationDegrees = Vector3.Zero;
                break;
            }

            case BuildMode.Fence:
            {
                var from = _dragFrom ?? at;
                check = _game.CheckFence(Fence, from, at);
                var first = from;
                var last = from;
                foreach (var p in SiteLayout.Line(from, at))
                {
                    last = p;
                }

                var x0 = Math.Min(first.X, last.X);
                var y0 = Math.Min(first.Y, last.Y);
                pad = new TileRect(x0, y0, Math.Abs(last.X - first.X) + 1, Math.Abs(last.Y - first.Y) + 1);
                var vertical = pad.Height > pad.Width;
                var run = new FenceRun(Fence, x0, y0, Math.Max(pad.Width, pad.Height), vertical);
                Ghost($"fence{Fence}{run}", () =>
                {
                    var builder = new MeshBuilder();
                    SiteView.AddRun(builder, run);
                    return builder.BuildNode("Fence");
                });
                break;
            }

            case BuildMode.Ground:
            {
                var from = _dragFrom ?? at;
                check = CheckGround(from, at);
                pad = Patches(from, at) is var (c0, r0, c1, r1) ? PatchRect(c0, r0, c1, r1) : new TileRect(at.X, at.Y, 1, 1);
                ClearGhostOnly();
                break;
            }

            default:
            {
                var site = _game.Site;
                pad = site.StageAt(at)?.Rect ?? site.StallAt(at)?.Rect ?? site.GateAt(at)?.Rect ?? new TileRect(at.X, at.Y, 1, 1);
                var refund = site.RefundAt(at);
                if (refund == 0 && site.IsEmpty(at) && _game.StaffAt(at) is null && _game.PatchAt(at) is var (c, r) && _game.GroundAt(c, r) is { } laid)
                {
                    // Straw or trackway with nothing standing on it.
                    pad = PatchRect(c, r, c, r);
                    refund = _game.Phase == Phase.Build ? FestivalGame.CostOf(laid) : 0;
                    check = new BuildCheck(true, -refund, null);
                    ClearGhostOnly();
                    break;
                }

                check = refund > 0 || _game.StaffAt(at) is not null ? new BuildCheck(true, -refund, null) : BuildCheck.No("");
                ClearGhostOnly();
                break;
            }
        }

        if (_ghost is not null)
        {
            _ghost.Visible = true;
        }

        // The ground under it: white where it can go, dark where it cannot.
        _pad.Visible = true;
        _pad.Position = SiteView.Middle(pad) + new Vector3(0, 0.03f, 0);
        _pad.Scale = new Vector3(pad.Width * T, 0.02f, pad.Height * T);
        _padMaterial.AlbedoColor = check.Ok ? new Color(1, 1, 1, 0.45f) : new Color(0.11f, 0.17f, 0.21f, 0.55f);

        if (Mode != BuildMode.Remove)
        {
            Refused = !check.Ok;
            Status = check.Ok ? $"{Label()}: £{check.Cost:N0}" : check.Problem ?? "";
        }
        else if (check.Ok)
        {
            Refused = false;
            Status = $"Take down: £{-check.Cost:N0} back";
        }
    }

    private string Label() => Mode switch
    {
        BuildMode.Stage => Catalogue.Of(Stage).Name,
        BuildMode.Gate => Catalogue.Of(Gate).Name,
        BuildMode.Stall => Catalogue.Of(Stall).Name,
        BuildMode.Staff => Catalogue.Of(Hire).Name,
        BuildMode.Ground => FestivalGame.NameOf(Ground),
        _ => Catalogue.Of(Fence).Name,
    };

    private void ClearGhostOnly()
    {
        _ghost?.QueueFree();
        _ghost = null;
        _ghostKey = "";
    }

    private void Ghost(string key, Func<Node3D> build)
    {
        if (_ghostKey == key && _ghost is not null)
        {
            return;
        }

        ClearGhostOnly();
        _ghostKey = key;
        _ghost = build();
        _ghost.Name = "Ghost";
        AddChild(_ghost);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Mode == BuildMode.None)
        {
            return;
        }

        switch (@event)
        {
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.R }:
                Turn();
                GetViewport().SetInputAsHandled();
                break;

            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }:
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }:
                if (_dragFrom is not null)
                {
                    _dragFrom = null;
                }
                else
                {
                    Stop();
                }

                GetViewport().SetInputAsHandled();
                break;

            case InputEventMouseButton { ButtonIndex: MouseButton.Left } click when _hovering:
                if (Mode is BuildMode.Fence or BuildMode.Ground)
                {
                    if (click.Pressed)
                    {
                        _dragFrom = _hover;
                    }
                    else if (_dragFrom is { } from)
                    {
                        _dragFrom = null;
                        if (Mode == BuildMode.Fence)
                        {
                            DragFence(from, _hover);
                        }
                        else
                        {
                            DragGround(from, _hover);
                        }

                        ClearGhostOnly();
                    }
                }
                else if (click.Pressed)
                {
                    ClickAt(_hover);
                }

                GetViewport().SetInputAsHandled();
                break;
        }
    }
}
