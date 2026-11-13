using System;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.UI;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Mock-up pictures for step 5, run with: godot --path game -- --stall-shots=DIR (needs a display).
/// The stalls in a line, then a village festival with them in place and queues posed by hand,
/// thought bubbles over people who need something, and a sketch of the money panel.
/// </summary>
public partial class StallShots : Node
{
    private static readonly string[] Names =
    {
        "1_the_stalls", "2_village_festival_with_stalls", "3_food_and_tea", "4_the_bar", "5_too_few_toilets", "6_what_people_need", "7_money_panel",
    };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;
    private Node3D _set = null!;
    private int _person = 3000;

    public StallShots(string directory)
    {
        _directory = directory;
    }

    public StallShots() : this("shots")
    {
    }

    public override void _Ready()
    {
        DirAccess.MakeDirRecursiveAbsolute(_directory);
        Callable.From(NextScene).CallDeferred();
    }

    public override void _Process(double delta)
    {
        if (_scene < 0 || _scene >= Names.Length)
        {
            return;
        }

        _frame++;
        if (!_ready)
        {
            if (GetTree().CurrentScene is FestivalScene f && f.IsNodeReady() && f.Game == GameManager.Instance.Game)
            {
                f.Game.Clock.SetSpeed(GameSpeed.Paused);
                f.SetHudShown(_scene == 6);
                _set = new Node3D { Name = "Set" };
                f.AddChild(_set);
                Arrange(f);
                f.Camera.Snap();
                _ready = true;
                _frame = 0;
            }

            return;
        }

        if (_frame == 20)
        {
            var path = $"{_directory}/{Names[_scene]}.png";
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"[stalls] {path}");
            NextScene();
        }
    }

    private void NextScene()
    {
        _scene++;
        _ready = false;
        _frame = 0;
        if (_scene >= Names.Length)
        {
            GD.Print("[stalls] Done.");
            GetTree().Quit(0);
            return;
        }

        GameManager.Instance.StartNewGame(null, new DateTime(2027, 7, 17).AddHours(14.5));
    }

    private void Arrange(FestivalScene festival)
    {
        var field = festival.Game.Field.Field;
        var west = field.X * 0.5f;
        var north = field.Y * 0.5f;
        var centre = new Vector3(west + (field.Width * 0.25f), 0, north + (field.Height * 0.25f));
        if (_scene == 0)
        {
            _set.Position = centre;
            _set.RotationDegrees = new Vector3(0, 45, 0);
            var kinds = Enum.GetValues<StallKind>();
            var xs = new[] { -9.5f, -4.2f, 2.6f, 8.6f, 12.2f };
            for (var i = 0; i < kinds.Length; i++)
            {
                var stall = SiteProps.Stall(kinds[i]);
                stall.Position = new Vector3(xs[i], 0, 0);
                _set.AddChild(stall);
                Staff(kinds[i], stall.Position, 0);
                _set.AddChild(new Label3D
                {
                    Text = SiteProps.StallName(kinds[i]), FontSize = 40, PixelSize = 0.008f, OutlineSize = 10,
                    OutlineModulate = new Color(0.1f, 0.2f, 0.1f, 0.9f), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true,
                    Position = new Vector3(xs[i], 0.02f, 3.6f),
                });
            }

            festival.Camera.LookAt(centre.X + 1.5f, centre.Z + 1f, 15f);
            return;
        }

        // A village festival: bandstand at the north end, stalls down the east side, toilets in the far corner.
        var info = Catalogue.Of(StageKind.Bandstand);
        festival.Game.BuildStage(StageKind.Bandstand, field.X + ((field.Width - info.Width) / 2) + 20, field.Y + 10, Facing.South);
        festival.Site.Rebuild();
        var tea = Place(StallKind.TeaAndCake, new Vector3(west + 5, 0, north + 12), 90);
        var van = Place(StallKind.FoodVan, new Vector3(west + 4.5f, 0, north + 20), 90);
        var bar = Place(StallKind.Bar, new Vector3(west + 5.5f, 0, north + 29), 90);
        var loos = Place(StallKind.Toilets, new Vector3(west + 15, 0, north + 3), 0);
        var water = Place(StallKind.WaterPoint, new Vector3(west + 21, 0, north + 2.5f), 0);
        Queue(tea, 90, 1.9f, _scene == 5 ? 3 : 5, 0);
        Queue(van, 90, 2.3f, 9, _scene == 5 ? 1 : 0);
        Queue(bar, 90, 2.6f, 9, 0, 3);
        Queue(loos, 0, 1.4f, _scene == 4 || _scene == 5 ? 16 : 6, _scene >= 4 && _scene != 6 ? 3 : 0);
        Queue(water, 0, 0.9f, 3, _scene == 5 ? 2 : 0);
        Wanderers(centre, _scene == 5);

        switch (_scene)
        {
            case 1:
            case 6:
                festival.Camera.ShowWholeField();
                break;
            case 2:
                festival.Camera.LookAt(van.X + 4f, van.Z - 3f, 16f);
                break;
            case 3:
                festival.Camera.LookAt(bar.X + 4f, bar.Z, 13f);
                break;
            case 4:
                festival.Camera.LookAt(loos.X + 3f, loos.Z + 6f, 16f);
                break;
            default:
                festival.Camera.LookAt(centre.X - 3, centre.Z - 2, 30f);
                break;
        }

        if (_scene == 6)
        {
            festival.GetNode<CanvasLayer>("Hud").AddChild(MoneyPanel());
        }
    }

    private Vector3 Place(StallKind kind, Vector3 at, float turn)
    {
        var stall = SiteProps.Stall(kind);
        stall.Position = at;
        stall.RotationDegrees = new Vector3(0, turn, 0);
        _set.AddChild(stall);
        Staff(kind, at, turn);
        return at;
    }

    private void Staff(StallKind kind, Vector3 at, float turn)
    {
        Vector3[] spots = kind switch
        {
            StallKind.TeaAndCake => new[] { new Vector3(0.3f, 0, 0) },
            StallKind.FoodVan => new[] { new Vector3(-0.9f, 0.55f, 0.55f), new Vector3(0.4f, 0.55f, 0.55f) },
            StallKind.Bar => new[] { new Vector3(-1.4f, 0, 0.55f), new Vector3(1.3f, 0, 0.55f) },
            _ => Array.Empty<Vector3>(),
        };
        foreach (var spot in spots)
        {
            var person = SiteProps.Stallholder(_person++);
            person.Position = at + spot.Rotated(Vector3.Up, Mathf.DegToRad(turn));
            person.RotationDegrees = new Vector3(0, turn, 0);
            _set.AddChild(person);
        }
    }

    /// <summary>A line of people out from the front of a stall, a little crooked, with some bubbles.</summary>
    private void Queue(Vector3 stall, float turn, float start, int count, int need, int lanes = 1)
    {
        var random = new Random(count + (int)stall.Z);
        var bubble = need switch { 1 => Need.Hungry, 2 => Need.Thirsty, 3 => Need.Toilet, _ => (Need?)null };
        for (var i = 0; i < count; i++)
        {
            var lane = i % lanes;
            var along = start + ((i / lanes) * 0.75f);
            var across = ((lane - ((lanes - 1) / 2f)) * 1.7f) + (((float)random.NextDouble() - 0.5f) * 0.25f) + (MathF.Sin(i * 0.5f) * 0.25f * (i / (float)lanes) / 4f);
            var local = new Vector3(across, 0, along);
            var person = PersonView.Build(LookGenerator.FestivalGoer(_person++));
            person.Position = stall + local.Rotated(Vector3.Up, Mathf.DegToRad(turn));
            person.RotationDegrees = new Vector3(0, turn + 180 + (((float)random.NextDouble() - 0.5f) * 30), 0);
            _set.AddChild(person);
            if (bubble is { } b && i >= 4 && i % 2 == 0)
            {
                Bubble(person, b);
            }
        }
    }

    private void Wanderers(Vector3 centre, bool needy)
    {
        var random = new Random(77);
        for (var i = 0; i < 70; i++)
        {
            var person = PersonView.Build(LookGenerator.FestivalGoer(_person++));
            var watching = i < 40;
            var at = watching
                ? new Vector3(centre.X + 10 + (((float)random.NextDouble() - 0.5f) * 13), 0, centre.Z - 7.5f + (MathF.Pow((float)random.NextDouble(), 1.4f) * 8))
                : new Vector3(centre.X + 6 + (((float)random.NextDouble() - 0.5f) * 34), 0, centre.Z + 1 + ((float)random.NextDouble() * 14));
            person.Position = at;
            person.RotationDegrees = new Vector3(0, watching ? 180 : (float)random.NextDouble() * 360, 0);
            person.SetPose(watching && random.NextDouble() < 0.3 ? Pose.Cheering : watching ? Pose.Standing : Pose.Walking, (float)random.NextDouble());
            _set.AddChild(person);
            if (needy && i % 4 == 0)
            {
                Bubble(person, (Need)(i / 4 % 3));
            }
        }
    }

    private enum Need { Hungry, Thirsty, Toilet }

    /// <summary>A thought bubble over someone's head with a simple picture of what they want.</summary>
    private static void Bubble(PersonView person, Need need)
    {
        var b = new MeshBuilder();
        var top = person.StandingHeight + 0.75f;
        b.Ball(new Vector3(0, top, 0), new Vector3(0.62f, 0.56f, 0.2f), Colors.White);
        b.Ball(new Vector3(0.14f, top - 0.36f, 0), 0.12f, Colors.White);
        b.Ball(new Vector3(0.2f, top - 0.5f, 0), 0.07f, Colors.White);
        var at = new Vector3(0, top, 0.1f);
        switch (need)
        {
            case Need.Hungry:
                b.Ball(at + new Vector3(0, 0.08f, 0), new Vector3(0.34f, 0.2f, 0.1f), new Color("#D9963B"));
                b.Box(at, new Vector3(0.36f, 0.06f, 0.1f), new Color("#7A4A2A"));
                b.Box(at + new Vector3(0, 0.04f, 0.01f), new Vector3(0.38f, 0.025f, 0.1f), new Color("#5DAE4B"));
                b.Ball(at + new Vector3(0, -0.07f, 0), new Vector3(0.34f, 0.12f, 0.1f), new Color("#D9963B"));
                break;
            case Need.Thirsty:
                b.Ball(at + new Vector3(0, -0.05f, 0), new Vector3(0.26f, 0.26f, 0.1f), new Color("#3C8DD8"));
                b.Cone(at + new Vector3(0, 0.11f, 0), new Vector3(0.2f, 0.22f, 0.1f), new Color("#3C8DD8"));
                break;
            default:
                b.Tube(at, new Vector3(0.3f, 0.1f, 0.3f), new Color("#2FB7A8"), new Vector3(90, 0, 0));
                b.Tube(at + new Vector3(0, 0, 0.02f), new Vector3(0.12f, 0.1f, 0.12f), Colors.White, new Vector3(90, 0, 0));
                b.Box(at + new Vector3(0.13f, -0.14f, 0), new Vector3(0.05f, 0.2f, 0.09f), new Color("#2FB7A8"));
                break;
        }

        var node = b.BuildNode("Bubble");
        node.RotationDegrees = new Vector3(0, -person.RotationDegrees.Y + 45, 0);
        person.AddChild(node);
    }

    /// <summary>A sketch of the money panel: what has come in and gone out so far today.</summary>
    private static Control MoneyPanel()
    {
        var panel = new PanelContainer { Theme = UiStyle.Theme, CustomMinimumSize = new Vector2(340, 0) };
        panel.AddThemeStyleboxOverride("panel", UiStyle.Card(margin: 18));
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 6);
        panel.AddChild(box);
        box.AddChild(UiStyle.Heading("Today's money", 20));
        void Row(string what, string amount, bool total = false, bool muted = false)
        {
            var row = new HBoxContainer();
            var left = total ? UiStyle.Heading(what, 17) : UiStyle.Body(what, muted, 15);
            left.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(left);
            row.AddChild(total ? UiStyle.Heading(amount, 17) : UiStyle.Body(amount, muted, 15));
            box.AddChild(row);
        }

        Row("In", "", muted: true);
        Row("Tickets  162 at £5", "£810");
        Row("Tea and cake", "£94");
        Row("Food van", "£388");
        Row("Bar", "£512");
        box.AddChild(new HSeparator());
        Row("Out", "", muted: true);
        Row("Stage and fences", "£458");
        Row("Stall hire", "£260");
        Row("Toilet hire", "£120");
        Row("Acts", "£350");
        box.AddChild(new HSeparator());
        Row("Up so far", "£616", total: true);
        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        box.AddChild(UiStyle.Heading("How people feel", 17));
        foreach (var (what, share, colour) in new[] { ("Fed", 0.82f, "#1E9E52"), ("Watered", 0.74f, "#1E9E52"), ("Toilets", 0.38f, "#E8A317") })
        {
            var row = new HBoxContainer();
            var label = UiStyle.Body(what, size: 15);
            label.CustomMinimumSize = new Vector2(90, 0);
            row.AddChild(label);
            var bar = new ProgressBar { Value = share * 100, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 14), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            bar.AddThemeStyleboxOverride("fill", UiStyle.Box(new Color(colour), 7));
            bar.AddThemeStyleboxOverride("background", UiStyle.Box(UiStyle.Border, 7));
            row.AddChild(bar);
            box.AddChild(row);
        }

        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight, Control.LayoutPresetMode.Minsize, 18);
        panel.GrowHorizontal = Control.GrowDirection.Begin;
        panel.Position = new Vector2(panel.Position.X, 62);
        return panel;
    }
}
