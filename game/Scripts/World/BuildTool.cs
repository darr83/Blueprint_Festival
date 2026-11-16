using BlueprintFestival.Audio;
using BlueprintFestival.Simulation.Sound;
using System;
using System.Linq;
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

    /// <summary>Dragging out a queue lane: its barriers go up by themselves.</summary>
    Lane,

    /// <summary>Marking ground the public must keep off.</summary>
    KeepOut,

    /// <summary>Putting up a path light or a floodlight. Also the Lights group on the build bar.</summary>
    Light,

    /// <summary>Dragging out a string of fairy lights: the poles go in by themselves.</summary>
    Fairy,

    /// <summary>Putting up a big screen with its speakers. It is on the Stages group of the build bar.</summary>
    Screen,

    /// <summary>Putting down the launch pad for the fireworks, with its safe zone round it.</summary>
    LaunchPad,

    /// <summary>The Fireworks group of the build bar: the launch pads and the show mixer.</summary>
    Fireworks,

    /// <summary>Putting out a seat, a sink or a shade sail. Also the Comforts group on the build bar.</summary>
    Comfort,

    /// <summary>The Paths group on the build bar. The tool itself lays a path as <see cref="Ground"/>.</summary>
    Path,

    /// <summary>Putting up a sign to write on. Also the Decor group on the build bar.</summary>
    Sign,

    /// <summary>Putting out a decoration.</summary>
    Decor,

    /// <summary>Dragging a line of bunting from one pole to another.</summary>
    Bunting,

    /// <summary>Marking ground for VIP guests only.</summary>
    VipGround,

    /// <summary>Drawing a crowd area in front of a stage, opening its gaps and setting its limit. Also the Areas group on the build bar.</summary>
    Area,

    /// <summary>Marking camping ground for quiet, family or accessible camping, or a fire lane. Also the Camping group on the build bar.</summary>
    Camp,
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
    private bool _held;
    private MeshInstance3D? _keepOut;
    private MeshInstance3D? _vipMarks;
    private StandardMaterial3D _vipMaterial = null!;
    private StandardMaterial3D _keepOutMaterial = null!;

    public BuildMode Mode { get; private set; }

    public StageKind Stage { get; private set; }

    public FenceKind Fence { get; private set; }

    public GateKind Gate { get; private set; }

    public StallKind Stall { get; private set; }

    public StaffKind Hire { get; private set; }

    public GroundKind Ground { get; private set; }

    public LightKind Light { get; private set; }

    public ScreenKind Screen { get; private set; }

    public LaunchPadKind Pad { get; private set; }

    public ComfortKind Comfort { get; private set; }

    public SignKind Sign { get; private set; }

    public DecorKind Decor { get; private set; }

    /// <summary>The kind of crowd area being drawn.</summary>
    public AreaKind Area { get; private set; }

    /// <summary>The crowd area whose limit is being set, or -1.</summary>
    public int SelectedArea { get; private set; } = -1;

    /// <summary>Raised when a crowd area is picked to change, or put down.</summary>
    public event Action? AreaPicked;

    /// <summary>Picks a crowd area to change its limit, or none.</summary>
    public void SelectArea(int area)
    {
        SelectedArea = area;
        AreaPicked?.Invoke();
    }

    private MeshInstance3D? _areaMarks;
    private readonly StandardMaterial3D[] _areaMaterials = new StandardMaterial3D[3];

    /// <summary>True while the crowd areas are tinted on the ground: while drawing them.</summary>
    public bool AreaMarksShown => _areaMarks is { Visible: true };

    /// <summary>Raised when a sign has just gone up, with a tile it stands on, so its words can be written.</summary>
    public event Action<TilePoint>? SignPlaced;

    /// <summary>How many patches wide a dragged path is laid: 1 (2 m), 2 or 3.</summary>
    public int PathWidth { get; private set; } = 1;

    /// <summary>Sets how wide dragged paths are laid, in patches.</summary>
    public void SetPathWidth(int patches) => PathWidth = Math.Clamp(patches, 1, FestivalGame.WidestPath);

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
        _keepOutMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(0.92f, 0.12f, 0.1f, 0.62f),
        };
        _vipMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(0.89f, 0.69f, 0.24f, 0.62f),
        };
        for (var k = 0; k < 3; k++)
        {
            _areaMaterials[k] = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                AlbedoColor = AreaColour((AreaKind)k, 0.5f),
            };
        }

        game.Site.Changed += DrawKeepOut;
        DrawKeepOut();
        StartCampMarks();
    }

    public override void _ExitTree()
    {
        _game.Site.Changed -= DrawKeepOut;
        StopCampMarks();
    }

    /// <summary>The colour each kind of crowd area is tinted: pink for a front pen, teal for standing, gold for VIPs.</summary>
    public static Color AreaColour(AreaKind kind, float alpha) => kind switch
    {
        AreaKind.FrontPen => new Color(1f, 0.36f, 0.62f, alpha),
        AreaKind.Vip => new Color(0.96f, 0.72f, 0.16f, alpha),
        _ => new Color(0.11f, 0.61f, 0.6f, alpha),
    };

    public void PickArea(AreaKind kind) => Pick(BuildMode.Area, () => Area = kind);

    /// <summary>Draws a crowd area over the box between two tiles, and picks it to set its limit.</summary>
    public BuildCheck DragArea(TilePoint from, TilePoint to)
    {
        var result = _game.BuildArea(Area, from, to);
        Refused = !result.Ok;
        SoundBoard.Cue(result.Ok ? Sounds.BuildFence : Sounds.Refuse);
        Status = !result.Ok ? result.Problem ?? "That cannot go there." : $"{CrowdAreas.Name(Area)} up. Set how many it lets in.";
        if (result.Ok)
        {
            SelectArea(_game.Site.Areas.Count - 1);
        }

        return result;
    }

    /// <summary>A click with the area tool: on an area's barrier it opens or closes a gap, inside an area it picks it.</summary>
    public void ClickArea(TilePoint at)
    {
        var site = _game.Site;
        var area = site.AreaAt(at);
        if (area < 0)
        {
            SelectArea(-1);
            Status = $"Hold the mouse button and drag out a {CrowdAreas.Name(Area).ToLowerInvariant()}.";
            return;
        }

        if (site.Areas[area].IsEdge(at))
        {
            var problem = _game.ToggleAreaGap(at);
            Refused = problem is not null;
            SoundBoard.Cue(problem is null ? Sounds.BuildFence : Sounds.Refuse);
            Status = problem ?? (site.IsAreaBarrier(at) ? "Gap closed." : "Gap opened. Post security by it to count people in.");
        }

        SelectArea(area);
    }

    public void PickVipGround() => Pick(BuildMode.VipGround, () => { });

    /// <summary>True while the ground marked for VIPs is being shown in gold: only while it is being marked.</summary>
    public bool VipMarksShown => _vipMarks is { Visible: true };

    public void PickStage(StageKind kind) => Pick(BuildMode.Stage, () => Stage = kind);

    public void PickFence(FenceKind kind) => Pick(BuildMode.Fence, () => Fence = kind);

    public void PickGate(GateKind kind) => Pick(BuildMode.Gate, () => Gate = kind);

    public void PickStall(StallKind kind) => Pick(BuildMode.Stall, () => Stall = kind);

    public void PickStaff(StaffKind kind) => Pick(BuildMode.Staff, () => Hire = kind);

    public void PickGround(GroundKind kind) => Pick(BuildMode.Ground, () => Ground = kind);

    public void PickLight(LightKind kind) => Pick(kind == LightKind.FairyLights ? BuildMode.Fairy : BuildMode.Light, () => Light = kind);

    public void PickScreen(ScreenKind kind) => Pick(BuildMode.Screen, () => Screen = kind);

    public void PickLaunchPad(LaunchPadKind kind) => Pick(BuildMode.LaunchPad, () => Pad = kind);

    public void PickComfort(ComfortKind kind) => Pick(BuildMode.Comfort, () => Comfort = kind);

    public void PickSign(SignKind kind) => Pick(BuildMode.Sign, () => Sign = kind);

    public void PickDecor(DecorKind kind) => Pick(BuildMode.Decor, () => Decor = kind);

    public void PickBunting() => Pick(BuildMode.Bunting, () => { });

    /// <summary>Strings bunting between two tiles, a pole at each end.</summary>
    public BuildCheck DragBunting(TilePoint from, TilePoint to)
    {
        var result = _game.BuildBunting(from, to);
        Said(result, "Bunting up");
        return result;
    }

    public void PickRemove() => Pick(BuildMode.Remove, () => { });

    public void PickLane() => Pick(BuildMode.Lane, () => { });

    public void PickKeepOut() => Pick(BuildMode.KeepOut, () => { });

    /// <summary>True while the ground marked "no customers" is being shown: only while it is being marked.</summary>
    public bool KeepOutShown => _keepOut is { Visible: true };

    /// <summary>How many stretches of marked ground are drawn.</summary>
    public int KeepOutRuns { get; private set; }

    private void DrawKeepOut()
    {
        _keepOut?.QueueFree();
        var builder = new MeshBuilder();
        var runs = SiteLayout.RunsOf(_game.Site.KeepOut);
        foreach (var (x, y, length, vertical) in runs)
        {
            var rect = vertical ? new TileRect(x, y, 1, length) : new TileRect(x, y, length, 1);
            builder.Box(SiteView.Middle(rect) + new Vector3(0, 0.035f, 0), new Vector3((rect.Width * T) - 0.08f, 0.02f, (rect.Height * T) - 0.08f), Colors.White);
        }

        KeepOutRuns = runs.Count;
        _vipMarks?.QueueFree();
        var gold = new MeshBuilder();
        foreach (var (x, y, length, vertical) in SiteLayout.RunsOf(_game.Site.VipGround))
        {
            var rect = vertical ? new TileRect(x, y, 1, length) : new TileRect(x, y, length, 1);
            gold.Box(SiteView.Middle(rect) + new Vector3(0, 0.05f, 0), new Vector3((rect.Width * T) - 0.08f, 0.02f, (rect.Height * T) - 0.08f), Colors.White);
        }

        _vipMarks = gold.IsEmpty ? new MeshInstance3D { Name = "VipMarks" } : gold.BuildNode("VipMarks");
        _vipMarks.MaterialOverride = _vipMaterial;
        _vipMarks.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _vipMarks.Visible = Mode == BuildMode.VipGround;
        AddChild(_vipMarks);
        _areaMarks?.QueueFree();
        _areaMarks = new MeshInstance3D { Name = "AreaMarks", Visible = Mode == BuildMode.Area };
        foreach (var kind in new[] { AreaKind.FrontPen, AreaKind.Standing, AreaKind.Vip })
        {
            var tint = new MeshBuilder();
            foreach (var a in _game.Site.Areas.Where(a => a.Kind == kind))
            {
                var inner = new TileRect(a.Rect.X + 1, a.Rect.Y + 1, a.Rect.Width - 2, a.Rect.Height - 2);
                tint.Box(SiteView.Middle(inner) + new Vector3(0, 0.04f, 0), new Vector3(inner.Width * T, 0.02f, inner.Height * T), Colors.White);
            }

            if (!tint.IsEmpty)
            {
                var node = tint.BuildNode($"Area{kind}");
                node.MaterialOverride = _areaMaterials[(int)kind];
                node.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                _areaMarks.AddChild(node);
            }
        }

        AddChild(_areaMarks);
        _keepOut = builder.IsEmpty ? new MeshInstance3D { Name = "KeepOut" } : builder.BuildNode("KeepOut");
        _keepOut.MaterialOverride = _keepOutMaterial;
        _keepOut.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _keepOut.Visible = Mode == BuildMode.KeepOut;
        AddChild(_keepOut);
    }

    public void Stop() => Pick(BuildMode.None, () => { });

    private void Pick(BuildMode mode, Action set)
    {
        Mode = mode;
        set();
        _dragFrom = null;
        _held = false;
        Refused = false;
        Status = mode switch
        {
            BuildMode.Stage => "Click on the grass to put the stage up. R turns it.",
            BuildMode.Fence => "Hold the mouse button and drag to run a fence.",
            BuildMode.Gate => "Click to put the gate up. R turns it. Close the fence either side so it is the only way in.",
            BuildMode.Stall => "Click on the grass to put it up. R turns it. People queue at the front.",
            BuildMode.Staff => Hire == StaffKind.TicketStaff ? "Click a gate to put them on it. One for each lane." : "Click on the grass to post them there.",
            BuildMode.Ground when IsPathLine => $"Hold the mouse button and drag to lay a path, {PathWidth * 2} m wide. Some people keep to paths; others cut across.",
            BuildMode.Decor => Decor == DecorKind.GiantLetters ? $"Click on the grass to stand {_game.LettersWord} up in giant letters. R turns it." : "Click on the grass to put it out. R turns it. Decorations cheer up the people near them, up to a point.",
            BuildMode.Bunting => "Hold the mouse button and drag to string bunting, up to 16 m. Start on a pole to carry on from it.",
            BuildMode.Sign => "Click on the grass to put it up, then write your own words on it. R turns it. Click a sign later to change it.",
            BuildMode.Comfort => Comforts.Of(Comfort).Seats.Length > 0 ? "Click on the grass to put it out. R turns it. People with food look for a seat near where they bought it."
                : Comforts.Of(Comfort).Washes ? "Click on the grass to put it out. R turns it. It counts for toilets and food stalls within 14 m."
                : "Click on the grass to put it up. R turns it. People walk and stand underneath.",
            BuildMode.Ground => "Click a patch of grass, or hold the mouse button and drag to cover a stretch.",
            BuildMode.Lane => "Hold the mouse button and drag out a queue from the front of a gate, stall or toilets. Drag again from its end to turn a corner.",
            BuildMode.KeepOut => "Hold the mouse button and drag over ground customers must keep off. Start on marked ground to clear it.",
            BuildMode.VipGround => "Hold the mouse button and drag over ground for VIP guests only. Start on VIP ground to clear it.",
            BuildMode.Camp => $"Hold the mouse button and drag over camping ground to mark it for {CampLayout.Name(CampZone).ToLowerInvariant()}. Start on ground marked that way to clear it.",
            BuildMode.Area => $"Hold the mouse button and drag out a {CrowdAreas.Name(Area).ToLowerInvariant()}. Click an area's barrier to open or close a gap; click inside to set how many it lets in.",
            BuildMode.Light => "Click on the grass to put the light up.",
            BuildMode.LaunchPad => "Click on the grass to put the launch pad down. Its safe zone must be clear. Fence it off from the firework mixer.",
            BuildMode.Screen => "Click on the grass to put the screen up. R turns it. It shows the nearest stage to the people in front of it.",
            BuildMode.Fairy => "Hold the mouse button and drag to string fairy lights. The poles go in by themselves. Start on a pole to carry on from it.",
            BuildMode.Remove => "Click a stage, a screen, the launch pad, a stall, a seat, a sign, a decoration, a bunting pole, a gate, a light or a stretch of fence to take it down. You get the money back. Click staff to send them home.",
            _ => "",
        };
        ClearGhost();
        if (_keepOut is not null)
        {
            _keepOut.Visible = mode == BuildMode.KeepOut;
        }

        if (_vipMarks is not null)
        {
            _vipMarks.Visible = mode == BuildMode.VipGround;
        }

        if (_areaMarks is not null)
        {
            _areaMarks.Visible = mode == BuildMode.Area;
        }

        ShowCampMarks();

        if (mode != BuildMode.Area && SelectedArea >= 0)
        {
            SelectArea(-1);
        }
    }

    /// <summary>Shows a drag from one tile to another and holds it there whatever the mouse does, until another tool is picked. For pictures and the launch test.</summary>
    public void HoldDrag(TilePoint from, TilePoint to)
    {
        _dragFrom = from;
        _held = true;
        ShowGhost(to);
    }

    /// <summary>Lays a queue lane between two tiles, or carries one on from its end.</summary>
    public BuildCheck DragLane(TilePoint from, TilePoint to)
    {
        var result = _game.BuildLane(from, to);
        Said(result, "Queue lane laid");
        return result;
    }

    /// <summary>Strings fairy lights between two tiles. A drag that starts or ends by a pole ties on to it.</summary>
    public BuildCheck DragFairyLights(TilePoint from, TilePoint to)
    {
        var result = _game.BuildFairyLights(from, to);
        Said(result, "Fairy lights up");
        return result;
    }

    /// <summary>Marks the box between two tiles "no customers", or clears it if the drag started on marked ground.</summary>
    public BuildCheck DragKeepOut(TilePoint from, TilePoint to)
    {
        var mark = !_game.Site.IsKeptClear(from);
        var result = _game.KeepOut(from, to, mark);
        Refused = !result.Ok;
        SoundBoard.Cue(result.Ok ? mark ? Sounds.BuildTape : Sounds.TakeDown : Sounds.Refuse);
        Status = !result.Ok ? result.Problem ?? "That cannot be marked." : mark ? "Customers will keep off that ground." : "Customers can walk there again.";
        return result;
    }

    /// <summary>Marks the box between two tiles for VIPs only, or clears it if the drag started on VIP ground.</summary>
    public BuildCheck DragVipGround(TilePoint from, TilePoint to)
    {
        var mark = !_game.Site.IsVipGround(from);
        var result = _game.VipGround(from, to, mark);
        Refused = !result.Ok;
        SoundBoard.Cue(result.Ok ? mark ? Sounds.BuildVip : Sounds.TakeDown : Sounds.Refuse);
        Status = !result.Ok ? result.Problem ?? "That cannot be marked." : mark ? "Only VIP guests will go there." : "Everyone can walk there again.";
        return result;
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

    private (int X, int Y) ScreenCorner(TilePoint at)
    {
        var rect = new PlacedScreen(Screen, 0, 0, Facing).Rect;
        return (at.X - (rect.Width / 2), at.Y - (rect.Height / 2));
    }

    private (int X, int Y) PadCorner(TilePoint at)
    {
        var size = LaunchPads.Of(Pad).Size;
        return (at.X - (size / 2), at.Y - (size / 2));
    }

    private (int X, int Y) DecorCorner(TilePoint at)
    {
        var rect = new PlacedDecor(Decor, 0, 0, Facing, _game.LettersWord).Rect;
        return (at.X - (rect.Width / 2), at.Y - (rect.Height / 2));
    }

    private (int X, int Y) SignCorner(TilePoint at)
    {
        var rect = new PlacedSign(Sign, 0, 0, Facing).Rect;
        return (at.X - (rect.Width / 2), at.Y - (rect.Height / 2));
    }

    private (int X, int Y) ComfortCorner(TilePoint at)
    {
        var rect = new PlacedComfort(Comfort, 0, 0, Facing).Rect;
        return (at.X - (rect.Width / 2), at.Y - (rect.Height / 2));
    }

    private (int X, int Y) StallCorner(TilePoint at)
    {
        var rect = new PlacedStall(Stall, 0, 0, Facing).Rect;
        return (at.X - (rect.Width / 2), at.Y - (rect.Height / 2));
    }

    private (int X, int Y) GateCorner(TilePoint at)
    {
        // Over a gap in a hedge the gate goes across the gap, whichever way it was turned.
        if (_game.Field.GapRunsNorthSouth(at) is { } upAndDown)
        {
            Vertical = upAndDown;
        }

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

            case BuildMode.Light:
                result = _game.BuildLight(Light, at);
                break;

            case BuildMode.Screen:
            {
                var (x, y) = ScreenCorner(at);
                result = _game.BuildScreen(Screen, x, y, Facing);
                break;
            }

            case BuildMode.LaunchPad:
            {
                var (x, y) = PadCorner(at);
                result = _game.BuildLaunchPad(Pad, x, y);
                break;
            }

            case BuildMode.Comfort:
            {
                var (x, y) = ComfortCorner(at);
                result = _game.BuildComfort(Comfort, x, y, Facing);
                break;
            }

            case BuildMode.Decor:
            {
                var (x, y) = DecorCorner(at);
                result = _game.BuildDecor(Decor, x, y, Facing);
                break;
            }

            case BuildMode.Sign:
            {
                var (x, y) = SignCorner(at);
                result = _game.BuildSign(Sign, x, y, Facing);
                if (result.Ok)
                {
                    Said(result, "Built");
                    SignPlaced?.Invoke(new TilePoint(x, y));
                    return result;
                }

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
                SoundBoard.Cue(Sounds.TakeDown);
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
                SoundBoard.Cue(result.Ok ? Sounds.TakeDown : Sounds.Refuse);
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

    /// <summary>True when the ground picked is a path, laid in a line one patch wide rather than over a box.</summary>
    private bool IsPathLine => FestivalGame.IsPathLine(Ground);

    /// <summary>The patches a path dragged between two tiles would cover, as the box round them, clipped to the field.</summary>
    private (int C0, int R0, int C1, int R1)? PathPatches(TilePoint from, TilePoint to)
    {
        if (Patches(from, from) is not var (a, b, _, _) || Patches(to, to) is not var (c, d, _, _))
        {
            return Patches(from, to);
        }

        var line = _game.PathLine((a, b), (c, d), PathWidth).ToList();
        return line.Count == 0 ? null : (line.Min(p => p.Column), line.Min(p => p.Row), line.Max(p => p.Column), line.Max(p => p.Row));
    }

    /// <summary>What covering the box between two tiles would cost, leaving out patches already covered the same way.</summary>
    public BuildCheck CheckGround(TilePoint from, TilePoint to)
    {
        if ((IsPathLine ? PathPatches(from, to) : Patches(from, to)) is not var (c0, r0, c1, r1))
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

    /// <summary>Lays straw or trackway over the box between two tiles, or a path in a line between them.</summary>
    public BuildCheck DragGround(TilePoint from, TilePoint to)
    {
        var result = CheckGround(from, to);
        if (result.Ok && (IsPathLine ? PathPatches(from, to) : Patches(from, to)) is var (c0, r0, c1, r1))
        {
            _game.LayGround(Ground, c0, r0, c1, r1);
        }

        Said(result, $"{FestivalGame.NameOf(Ground)} laid");
        return result;
    }

    /// <summary>The sound of the thing just built going up: each kind of thing has its own.</summary>
    private string BuiltSound() => Mode switch
    {
        BuildMode.Stage => Sounds.BuildStage,
        BuildMode.Screen => Sounds.BuildScreen,
        BuildMode.LaunchPad => Sounds.BuildLaunchPad,
        BuildMode.Fence or BuildMode.Lane => Sounds.BuildFence,
        BuildMode.Gate => Sounds.BuildGate,
        BuildMode.Staff => Sounds.HireStaff,
        BuildMode.Ground => Sounds.BuildGround,
        BuildMode.Light => Sounds.BuildLight,
        BuildMode.Fairy => Sounds.BuildFairy,
        BuildMode.Bunting => Sounds.BuildBunting,
        BuildMode.Sign => Sounds.BuildSign,
        BuildMode.Comfort => Comfort switch
        {
            ComfortKind.StrawBale => Sounds.BuildStraw,
            ComfortKind.Deckchairs => Sounds.BuildDeckchair,
            ComfortKind.ParasolTable => Sounds.BuildParasol,
            ComfortKind.HandWash => Sounds.BuildHandWash,
            ComfortKind.HandGel => Sounds.BuildHandGel,
            ComfortKind.ShadeSail => Sounds.BuildShadeSail,
            ComfortKind.SeatingMarquee => Sounds.BuildStall,
            _ => Sounds.BuildBench,
        },
        BuildMode.Decor => Decor switch
        {
            DecorKind.Scarecrow => Sounds.BuildStraw,
            DecorKind.FlagPole => Sounds.BuildFlag,
            DecorKind.FirePit => Sounds.BuildFirePit,
            DecorKind.GiantLetters => Sounds.BuildLetters,
            _ => Sounds.BuildPlanter,
        },
        BuildMode.Stall when Rides.IsRide(Stall) => Sounds.BuildRide,
        _ => Sounds.BuildStall,
    };

    private TileRect PatchRect(int c0, int r0, int c1, int r1)
    {
        var f = _game.Field.Field;
        return new TileRect(f.X + (c0 * FestivalGame.WearTiles), f.Y + (r0 * FestivalGame.WearTiles), (c1 - c0 + 1) * FestivalGame.WearTiles, (r1 - r0 + 1) * FestivalGame.WearTiles);
    }

    private void Said(BuildCheck result, string done)
    {
        Refused = !result.Ok;
        SoundBoard.Cue(!result.Ok ? Sounds.Refuse : BuiltSound());
        Status = result.Ok ? $"{done} for £{result.Cost:N0}." : result.Problem ?? "That cannot go there.";
    }

    /// <summary>The tile under the mouse, or null if the mouse is over a panel or off the map.</summary>
    /// <summary>The tile the mouse is over, or null when it is over a panel, off the map, or the view is a stage camera's.</summary>
    public TilePoint? TileUnderMouse()
    {
        var viewport = GetViewport();
        if (viewport.GuiGetHoveredControl() is not null || !_camera.Camera.Current)
        {
            // Nothing is built from a stage camera's view.
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
        if (Mode == BuildMode.None || _held)
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
                _ghost.RotationDegrees = new Vector3(0, SiteView.Turn(_game.Site, gate), 0);
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

            case BuildMode.Lane:
            {
                var from = _dragFrom ?? at;
                check = _game.CheckLane(from, at);
                var last = SiteLayout.Line(from, at).Last();
                pad = new TileRect(Math.Min(from.X, last.X), Math.Min(from.Y, last.Y), Math.Abs(last.X - from.X) + 1, Math.Abs(last.Y - from.Y) + 1);
                var across = pad.Width >= pad.Height;
                Ghost($"lane{pad}", () =>
                {
                    // The barrier that will go up either side.
                    var builder = new MeshBuilder();
                    foreach (var side in new[] { -1, 1 })
                    {
                        var run = across ? new FenceRun(FenceKind.CrowdBarrier, pad.X, pad.Y + side, pad.Width, false) : new FenceRun(FenceKind.CrowdBarrier, pad.X + side, pad.Y, pad.Height, true);
                        SiteView.AddRun(builder, run);
                    }

                    return builder.BuildNode("Lane");
                });
                break;
            }

            case BuildMode.Decor:
            {
                var (x, y) = DecorCorner(at);
                check = _game.CheckDecor(Decor, x, y, Facing);
                var decor = new PlacedDecor(Decor, x, y, Facing, _game.LettersWord);
                pad = decor.Rect;
                Ghost($"decor{Decor}", () => SiteProps.Decoration(decor));
                break;
            }

            case BuildMode.Bunting:
            {
                var from = _dragFrom ?? at;
                pad = new TileRect(at.X, at.Y, 1, 1);
                if (_dragFrom is null || from == at)
                {
                    // Not dragging yet: show where the first pole would stand.
                    ClearGhostOnly();
                    ShowPad(pad, true);
                    Refused = false;
                    Status = _game.Site.IsBuntingPole(at) ? "Drag from this pole to carry the bunting on." : "Hold the mouse button and drag to string bunting.";
                    return;
                }

                check = _game.CheckBunting(from, at);
                var run = new BuntingRun(from, at);
                Ghost($"bunting{run}", () => SiteProps.BuntingBetween(run));
                _ghost!.Position = Vector3.Zero;
                _ghost.RotationDegrees = Vector3.Zero;
                ShowPad(pad, check.Ok);
                Refused = !check.Ok;
                Status = check.Ok ? $"Bunting, {run.Metres:0} m: £{check.Cost:N0}" : check.Problem ?? "";
                return;
            }

            case BuildMode.Sign:
            {
                var (x, y) = SignCorner(at);
                check = _game.CheckSign(Sign, x, y, Facing);
                pad = new PlacedSign(Sign, x, y, Facing).Rect;
                Ghost($"sign{Sign}", () => SiteProps.Sign(new PlacedSign(Sign, 0, 0, Facing.South, Signs.Of(Sign).Words.ToList(), Sign == SignKind.FingerPost ? 3 : Sign == SignKind.Banner ? 1 : 0, 0b101), null));
                break;
            }

            case BuildMode.Comfort:
            {
                var (x, y) = ComfortCorner(at);
                check = _game.CheckComfort(Comfort, x, y, Facing);
                pad = new PlacedComfort(Comfort, x, y, Facing).Rect;
                Ghost($"comfort{Comfort}", () => SiteProps.Comfort(Comfort));
                _ghost!.Position = SiteView.Middle(pad);
                _ghost.RotationDegrees = new Vector3(0, SiteView.Turn(Facing), 0);
                break;
            }

            case BuildMode.Screen:
            {
                var (x, y) = ScreenCorner(at);
                check = _game.CheckScreen(Screen, x, y, Facing);
                pad = new PlacedScreen(Screen, x, y, Facing).Rect;
                Ghost($"screen{Screen}", () => ScreenView.Model(Screen));
                _ghost!.Position = SiteView.Middle(pad);
                _ghost.RotationDegrees = new Vector3(0, SiteView.Turn(Facing), 0);
                break;
            }

            case BuildMode.LaunchPad:
            {
                var (x, y) = PadCorner(at);
                check = _game.CheckLaunchPad(Pad, x, y);
                var placed = new PlacedLaunchPad(Pad, x, y, false);
                pad = placed.Zone;
                Ghost($"pad{Pad}", () => FireworksView.Model(Pad));
                _ghost!.Position = SiteView.Middle(placed.Rect);
                _ghost.RotationDegrees = Vector3.Zero;
                break;
            }

            case BuildMode.Light:
            {
                check = _game.CheckLight(Light, at);
                pad = new TileRect(at.X, at.Y, 1, 1);
                Ghost($"light{Light}", () => LightsView.Single(Light));
                _ghost!.Position = LightsView.Middle(at);
                _ghost.RotationDegrees = Vector3.Zero;
                break;
            }

            case BuildMode.Fairy:
            {
                var site = _game.Site;
                var (from, to) = (site.SnapToPole(_dragFrom ?? at), site.SnapToPole(at));
                pad = new TileRect(to.X, to.Y, 1, 1);
                if (_dragFrom is null)
                {
                    // Not dragging yet: show where the first pole would stand.
                    check = new BuildCheck(true, 0, null);
                    ClearGhostOnly();
                    ShowPad(pad, true);
                    Refused = false;
                    Status = site.IsFairyPole(to) ? "Drag from this pole to carry the fairy lights on." : "Hold the mouse button and drag to string fairy lights.";
                    return;
                }

                check = _game.CheckFairyLights(from, to);
                var fairy = new FairyString(from, to);
                Ghost($"fairy{fairy}", () => LightsView.Fairy(fairy));
                _ghost!.Position = Vector3.Zero;
                _ghost.RotationDegrees = Vector3.Zero;
                break;
            }

            case BuildMode.KeepOut:
            {
                var from = _dragFrom ?? at;
                var mark = !_game.Site.IsKeptClear(from);
                check = _game.CheckKeepOut(from, at, mark);
                pad = new TileRect(Math.Min(from.X, at.X), Math.Min(from.Y, at.Y), Math.Abs(at.X - from.X) + 1, Math.Abs(at.Y - from.Y) + 1);
                ClearGhostOnly();
                ShowPad(pad, check.Ok);
                Refused = !check.Ok;
                Status = !check.Ok ? check.Problem ?? "" : mark ? "No customers here: free" : "Let customers back on this ground";
                return;
            }

            case BuildMode.Area:
            {
                ClearGhostOnly();
                var site = _game.Site;
                if (_dragFrom is not { } start || start == at)
                {
                    var under = site.AreaAt(at);
                    pad = new TileRect(at.X, at.Y, 1, 1);
                    ShowPad(pad, true);
                    Refused = false;
                    Status = under < 0 ? $"Hold the mouse button and drag out a {CrowdAreas.Name(Area).ToLowerInvariant()}."
                        : site.Areas[under].IsEdge(at) ? (site.IsAreaBarrier(at) ? "Click to open a gap in the barrier here." : "Click to close this gap.")
                        : $"{CrowdAreas.Name(site.Areas[under].Kind)}: click to set how many it lets in.";
                    return;
                }

                check = _game.CheckArea(Area, start, at);
                pad = SiteLayout.BoxOf(start, at);
                ShowPad(pad, check.Ok);
                Refused = !check.Ok;
                var metres = Math.Max(0, pad.Width - 2) * Math.Max(0, pad.Height - 2) * FieldMap.TileMetres * FieldMap.TileMetres;
                Status = !check.Ok ? check.Problem ?? ""
                    : $"{CrowdAreas.Name(Area)}: {pad.Width * FieldMap.TileMetres:0.#} m × {pad.Height * FieldMap.TileMetres:0.#} m, lets in {CrowdAreas.Suggested(metres):N0}. Barrier £{check.Cost:N0}";
                return;
            }

            case BuildMode.Camp:
            {
                var from = _dragFrom ?? at;
                var zone = MarkingZone(from);
                check = _game.CheckCampZone(from, at, zone);
                pad = new TileRect(Math.Min(from.X, at.X), Math.Min(from.Y, at.Y), Math.Abs(at.X - from.X) + 1, Math.Abs(at.Y - from.Y) + 1);
                ClearGhostOnly();
                ShowPad(pad, check.Ok);
                Refused = !check.Ok;
                Status = !check.Ok ? check.Problem ?? "" : zone == CampZone.General ? "Clear the marks: general camping again" : $"{CampLayout.Name(zone)}: free";
                return;
            }

            case BuildMode.VipGround:
            {
                var from = _dragFrom ?? at;
                var mark = !_game.Site.IsVipGround(from);
                check = _game.CheckVipGround(from, at, mark);
                pad = new TileRect(Math.Min(from.X, at.X), Math.Min(from.Y, at.Y), Math.Abs(at.X - from.X) + 1, Math.Abs(at.Y - from.Y) + 1);
                ClearGhostOnly();
                ShowPad(pad, check.Ok);
                Refused = !check.Ok;
                Status = !check.Ok ? check.Problem ?? "" : mark ? "VIP guests only: free" : "Open this ground to everyone again";
                return;
            }

            case BuildMode.Ground:
            {
                var from = _dragFrom ?? at;
                check = CheckGround(from, at);
                pad = (IsPathLine ? PathPatches(from, at) : Patches(from, at)) is var (c0, r0, c1, r1) ? PatchRect(c0, r0, c1, r1) : new TileRect(at.X, at.Y, 1, 1);
                ClearGhostOnly();
                break;
            }

            default:
            {
                var site = _game.Site;
                pad = (site.FireworksAt(at) ? site.LaunchPad?.Zone : null) ?? site.StageAt(at)?.Rect ?? site.StallAt(at)?.Rect ?? site.GateAt(at)?.Rect ?? new TileRect(at.X, at.Y, 1, 1);
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

        ShowPad(pad, check.Ok);

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

    /// <summary>The ground under what is being placed: white where it can go, dark where it cannot.</summary>
    private void ShowPad(TileRect pad, bool ok)
    {
        _pad.Visible = true;
        _pad.Position = SiteView.Middle(pad) + new Vector3(0, 0.03f, 0);
        _pad.Scale = new Vector3(pad.Width * T, 0.02f, pad.Height * T);
        _padMaterial.AlbedoColor = ok ? new Color(1, 1, 1, 0.45f) : new Color(0.11f, 0.17f, 0.21f, 0.55f);
    }

    private string Label() => Mode switch
    {
        BuildMode.Lane => "Queue lane",
        BuildMode.Light or BuildMode.Fairy => Lighting.Of(Light).Name,
        BuildMode.Screen => BigScreens.Of(Screen).Name,
        BuildMode.LaunchPad => LaunchPads.Of(Pad).Name,
        BuildMode.Comfort => Comforts.Of(Comfort).Name,
        BuildMode.Sign => Signs.Of(Sign).Name,
        BuildMode.Decor => Simulation.Site.Decor.Of(Decor).Name,
        BuildMode.Bunting => "Bunting",
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
                if (Mode is BuildMode.Fence or BuildMode.Ground or BuildMode.Lane or BuildMode.KeepOut or BuildMode.VipGround or BuildMode.Fairy or BuildMode.Bunting or BuildMode.Area or BuildMode.Camp)
                {
                    if (click.Pressed)
                    {
                        _dragFrom = _hover;
                    }
                    else if (_dragFrom is { } from)
                    {
                        _dragFrom = null;
                        if (Mode == BuildMode.Area)
                        {
                            if (from == _hover)
                            {
                                ClickArea(from);
                            }
                            else
                            {
                                DragArea(from, _hover);
                            }
                        }
                        else if (Mode == BuildMode.Fence)
                        {
                            DragFence(from, _hover);
                        }
                        else if (Mode == BuildMode.Lane)
                        {
                            DragLane(from, _hover);
                        }
                        else if (Mode == BuildMode.KeepOut)
                        {
                            DragKeepOut(from, _hover);
                        }
                        else if (Mode == BuildMode.VipGround)
                        {
                            DragVipGround(from, _hover);
                        }
                        else if (Mode == BuildMode.Camp)
                        {
                            DragCampZone(from, _hover);
                        }
                        else if (Mode == BuildMode.Fairy)
                        {
                            DragFairyLights(from, _hover);
                        }
                        else if (Mode == BuildMode.Bunting)
                        {
                            DragBunting(from, _hover);
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
