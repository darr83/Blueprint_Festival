using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.UI;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Mock-up pictures for step 7, run with: godot --path game -- --staff-shots=DIR (needs a display).
/// The festival behind is a real game; the staff, the first aid tent and the hiring panel are
/// posed on top of it, and nothing here is wired to the rules yet.
/// </summary>
public partial class StaffShots : Node
{
    private static readonly (string Name, double Hour)[] Scenes =
    {
        ("1_the_three_uniforms", 8), ("2_ticket_staff_at_the_gate", 9.7), ("3_security_by_the_stage", 14.7), ("4_first_aid_tent", 14.7),
        ("5_someone_taken_ill", 15.3), ("6_hiring_staff", 8),
    };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;

    public StaffShots(string directory)
    {
        _directory = directory;
    }

    public StaffShots() : this("shots")
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
                _ready = true;
                _frame = 0;
            }

            return;
        }

        if (_frame == 40)
        {
            var path = $"{_directory}/{Scenes[_scene].Name}.png";
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"[staff] {path}");
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
            GD.Print("[staff] Done.");
            GetTree().Quit(0);
            return;
        }

        GameManager.Instance.StartNewGame(null, null, 9);
    }

    /// <summary>Stands something on the field at a place in tiles, turned so many degrees from facing south.</summary>
    private static Node3D Put(FestivalScene festival, Node3D node, double tileX, double tileY, float turn = 0, float up = 0)
    {
        festival.AddChild(node);
        node.Position = new Vector3((float)(tileX * FieldMap.TileMetres), up, (float)(tileY * FieldMap.TileMetres));
        node.RotationDegrees = new Vector3(0, turn, 0);
        return node;
    }

    private void Arrange(FestivalScene festival)
    {
        var game = festival.Game;
        var f = game.Field.Field;
        var gap = game.Field.Gap;
        game.BuildStage(StageKind.Bandstand, f.X + 74, f.Y + 10, Facing.South);
        game.BuildFence(FenceKind.Paling, new TilePoint(gap.X, gap.Y + 1), new TilePoint(gap.Right - 1, gap.Y + 1));
        game.BuildGate(GateKind.VillageGate, gap.X + 2, gap.Y + 1, false);
        game.BuildStall(StallKind.TeaAndCake, f.X + 7, f.Y + 21, Facing.East);
        game.BuildStall(StallKind.FoodVan, f.X + 7, f.Y + 34, Facing.East);
        game.BuildStall(StallKind.Bar, f.X + 7, f.Y + 52, Facing.East);
        game.BuildStall(StallKind.Toilets, f.X + 26, f.Y + 5, Facing.South);
        game.BuildStall(StallKind.Toilets, f.X + 36, f.Y + 5, Facing.South);
        game.BuildStall(StallKind.WaterPoint, f.X + 48, f.Y + 5, Facing.South);

        var until = game.Now.Date.AddHours(Scenes[_scene].Hour);
        while (game.Now < until)
        {
            game.Clock.Step();
        }

        game.Clock.SetSpeed(GameSpeed.Paused);
        festival.Site.Rebuild();
        festival.Crowd.Snap();
        festival.Camera.ShowWholeField();
        const float t = 0.5f;

        // The staff every picture has: one on the gate, security by the stage and the bar, and the first aid tent.
        var tentX = f.X + 60.0;
        var tentY = f.Y + 6.0;
        if (_scene > 0)
        {
            Put(festival, SiteProps.Staff(StaffKind.TicketStaff, 1), gap.X + 8.6, gap.Y + 3.4);
            Put(festival, SiteProps.Staff(StaffKind.Security, 2), f.X + 75, f.Y + 24);
            Put(festival, SiteProps.Staff(StaffKind.Security, 3), f.X + 85, f.Y + 24);
            Put(festival, SiteProps.Staff(StaffKind.Security, 4), f.X + 19, f.Y + 49, 90);
            Put(festival, SiteProps.FirstAidTent(), tentX, tentY);
            Put(festival, SiteProps.Staff(StaffKind.FirstAider, 5), tentX + 2.5, tentY + 5);
        }

        switch (_scene)
        {
            case 0:
                Put(festival, SiteProps.Staff(StaffKind.TicketStaff, 1), f.X + 57, f.Y + 41.5, 45);
                Put(festival, SiteProps.Staff(StaffKind.Security, 2), f.X + 60, f.Y + 40, 45);
                Put(festival, SiteProps.Staff(StaffKind.FirstAider, 5), f.X + 63, f.Y + 38.5, 45);
                festival.Camera.LookAt((f.X + 60) * t, (f.Y + 40) * t, 8f);
                festival.SetHudShown(false);
                break;

            case 1:
                festival.Camera.LookAt((gap.X + 7) * t, (gap.Y + 1) * t, 13f);
                break;

            case 2:
                festival.Camera.LookAt((f.X + 80) * t, (f.Y + 27) * t, 15f);
                break;

            case 3:
            {
                // Someone resting on a camp bed with a first-aider beside them.
                var patient = PersonView.Build(LookGenerator.FestivalGoer(77, AgeGroup.Adult) with { Poncho = null });
                Put(festival, patient, tentX - 2.3, tentY - 0.4, 0, 0.62f);
                patient.RotationDegrees = new Vector3(-90, 0, 0);
                Put(festival, SiteProps.Staff(StaffKind.FirstAider, 6), tentX - 0.4, tentY + 0.5, -70);
                festival.Camera.LookAt((float)tentX * t, ((float)tentY * t) + 1.5f, 8f);
                break;
            }

            case 4:
            {
                // Someone has gone down in the middle of the field; a first-aider is on the way.
                var ill = PersonView.Build(LookGenerator.FestivalGoer(31, AgeGroup.Adult) with { Poncho = null });
                Put(festival, ill, f.X + 56, f.Y + 44, 0, 0.14f);
                ill.RotationDegrees = new Vector3(-90, 30, 0);
                var marker = SiteProps.HelpMarker(0.5f);
                Put(festival, marker, f.X + 56, f.Y + 43, festival.Camera.Yaw);
                var medic = SiteProps.Staff(StaffKind.FirstAider, 6);
                Put(festival, medic, f.X + 60, f.Y + 39, -135);
                medic.GetChild<PersonView>(0).SetPose(Pose.Walking);
                festival.Camera.LookAt((f.X + 57) * t, (f.Y + 42) * t, 9f);
                break;
            }

            default:
                HiringPanel(festival);
                break;
        }

        festival.Camera.Snap();
    }

    /// <summary>What the Staff tab on the build bar could look like.</summary>
    private static void HiringPanel(FestivalScene festival)
    {
        var hud = festival.GetNode<CanvasLayer>("Hud");
        var panel = new PanelContainer { Theme = UiStyle.Theme };
        panel.AddThemeStyleboxOverride("panel", UiStyle.Card(margin: 16, radius: 16));
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 10);
        panel.AddChild(box);
        box.AddChild(UiStyle.Heading("Staff for the day", 20));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        (string Name, string Price, string About, string Hired)[] choices =
        {
            ("Ticket staff", "£40 a day", "Checks tickets at a gate.\nOne for each lane.", "1 hired"),
            ("Security", "£60 a day", "Stands where you put them\nand deals with trouble.", "3 hired"),
            ("First-aider", "£50 a day", "Goes to anyone taken ill\nor hurt.", "2 hired"),
            ("First aid tent", "£80", "Two beds. First-aiders bring\npeople here to rest.", "1 built"),
        };
        foreach (var (name, price, about, hired) in choices)
        {
            var tile = new PanelContainer { CustomMinimumSize = new Vector2(215, 0) };
            tile.AddThemeStyleboxOverride("panel", UiStyle.Box(UiStyle.Surface, 12, UiStyle.Border, 12));
            var inner = new VBoxContainer();
            inner.AddChild(UiStyle.Heading(name, 17));
            inner.AddChild(UiStyle.Body(price, size: 15));
            inner.AddChild(UiStyle.Body(about, muted: true, size: 13));
            inner.AddChild(UiStyle.Body(hired, size: 14));
            tile.AddChild(inner);
            row.AddChild(tile);
        }

        box.AddChild(row);
        box.AddChild(UiStyle.Body("Click a kind of staff, then click the field to post them there. Wages for today: £320.", muted: true, size: 14));
        hud.AddChild(panel);
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom, Control.LayoutPresetMode.Minsize, 18);
        panel.GrowHorizontal = Control.GrowDirection.Both;
        panel.GrowVertical = Control.GrowDirection.Begin;
        panel.Position = new Vector2(panel.Position.X, panel.Position.Y - 62);
    }
}
