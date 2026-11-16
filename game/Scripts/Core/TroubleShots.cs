using System;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Posed pictures of drunk people, fights and people being sick, run with:
/// godot --path game -- --trouble-mock=DIR (needs a display). These show how each thing looks
/// close up; the pictures from a real festival day come from --trouble-shots.
/// </summary>
public partial class TroubleShots : Node
{
    private static readonly string[] Names = { "1_how_drunk", "2_a_fight", "3_security_break_it_up", "4_being_sick", "5_the_cleaner" };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private FestivalScene? _festival;
    private Node3D? _set;
    private int _seed;

    public TroubleShots(string directory)
    {
        _directory = directory;
    }

    public TroubleShots() : this("shots")
    {
    }

    public override void _Ready()
    {
        DirAccess.MakeDirRecursiveAbsolute(_directory);
        Callable.From(() => GameManager.Instance.StartNewGame(null, new DateTime(2027, 7, 17, 15, 0, 0))).CallDeferred();
    }

    public override void _Process(double delta)
    {
        if (_festival is null)
        {
            if (GetTree().CurrentScene is FestivalScene f && f.IsNodeReady())
            {
                _festival = f;
                f.Game.Clock.SetSpeed(Simulation.Core.GameSpeed.Paused);
                f.SetHudShown(false);
                Next();
            }

            return;
        }

        _frame++;
        if (_frame == 12)
        {
            var path = $"{_directory}/{Names[_scene]}.png";
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"[trouble] {path}");
            Next();
        }
    }

    private void Next()
    {
        _scene++;
        _frame = 0;
        _set?.Free();
        _set = null;
        var only = OS.GetEnvironment("SHOTS_ONLY");
        while (only.Length > 0 && _scene < Names.Length && !Names[_scene].Contains(only, StringComparison.Ordinal))
        {
            _scene++;
        }

        if (_scene >= Names.Length)
        {
            GD.Print("[trouble] Done.");
            GetTree().Quit(0);
            return;
        }

        var festival = _festival!;
        var field = festival.Game.Field.Field;
        var centre = new Vector3((field.X + (field.Width / 2f)) * 0.5f, 0, (field.Y + (field.Height / 2f)) * 0.5f);

        // A stand turned to face the camera: along its X is across the screen, +Z is towards the viewer.
        _set = new Node3D { Name = "Set", Position = centre, RotationDegrees = new Vector3(0, 45, 0) };
        festival.AddChild(_set);
        _seed = 40 + (_scene * 100);
        var look = 6.6f;
        switch (_scene)
        {
            case 0:
                HowDrunk();
                look = 5.6f;
                break;
            case 1:
                Fight();
                break;
            case 2:
                BrokenUp();
                look = 6.2f;
                break;
            case 3:
                Sick();
                look = 5.4f;
                break;
            case 4:
                Cleaner();
                look = 4.6f;
                break;
        }

        festival.Camera.LookCloser(centre.X - 0.9f, centre.Z - 0.9f, look);
        festival.Camera.Snap();
    }

    private PersonView Person(float x, float z, float facing, Pose pose = Pose.Standing, float wobble = 0, Carried holding = Carried.None, bool? female = null, float stride = 0.25f)
    {
        var look = LookGenerator.FestivalGoer(_seed++, AgeGroup.Adult) with { Poncho = null };
        if (female is { } f)
        {
            look = look with { Female = f, Beard = look.Beard && !f };
        }

        var person = PersonView.Build(look);
        person.Position = new Vector3(x, 0, z);
        person.RotationDegrees = new Vector3(0, facing, 0);
        person.Wobble = wobble;
        person.Hold(holding);
        person.SetPose(pose, stride);
        if (wobble >= 0.7f)
        {
            // A still picture cannot show the sway, so they are caught mid-lurch.
            person.RotationDegrees += new Vector3(0, 0, wobble * (_seed % 2 == 0 ? 11 : -11));
        }

        _set!.AddChild(person);
        return person;
    }

    private Node3D StaffAt(StaffKind kind, float x, float z, float facing, Pose pose = Pose.Standing, int seed = 3)
    {
        var staff = SiteProps.Staff(kind, seed);
        staff.Position = new Vector3(x, 0, z);
        staff.RotationDegrees = new Vector3(0, facing, 0);
        staff.GetChild<PersonView>(0).SetPose(pose, 0.25f);
        _set!.AddChild(staff);
        return staff;
    }

    private void Label(string text, float x, float z, int size = 36)
    {
        _set!.AddChild(new Label3D
        {
            Text = text,
            FontSize = size,
            PixelSize = 0.0042f,
            OutlineSize = 10,
            OutlineModulate = new Color(0.1f, 0.2f, 0.1f, 0.9f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            Position = new Vector3(x, 0.02f, z),
        });
    }

    private void Prop(Node3D prop, float x, float z, float turn = 0)
    {
        prop.Position = new Vector3(x, 0, z);
        prop.RotationDegrees = new Vector3(0, turn, 0);
        _set!.AddChild(prop);
    }

    /// <summary>Lays someone flat on their back on the grass.</summary>
    private static void LayDown(PersonView person)
    {
        person.RotationDegrees = new Vector3(-90, person.RotationDegrees.Y, 0);
        person.Position += new Vector3(0, 0.14f, 0);
    }

    private void HowDrunk()
    {
        const float gap = 1.75f;
        Person(-2 * gap, 0, 0, Pose.Standing, 0, Carried.Pint, false);
        Label("Sober", -2 * gap, 0.95f);
        Person(-gap, 0, 0, Pose.Cheering, 0.25f, Carried.Pint, true);
        Label("Merry\n(happier, dances more)", -gap, 1.1f, 30);
        Person(0, 0, -8, Pose.Standing, 0.75f, Carried.Cocktail, false);
        Label("Tipsy\n(sways, buys more)", 0, 1.1f, 30);
        Person(gap, 0, 14, Pose.Walking, 1f, Carried.Pint, false, 0.3f);
        Prop(SiteProps.DroppedGlass(), gap - 0.5f, 0.55f, 30);
        Label("Drunk\n(staggers, drops things,\nfights, is sick)", gap, 1.25f, 30);
        var out1 = Person(2 * gap + 0.5f, 0.2f, 90, Pose.Standing, 0, Carried.None, false);
        LayDown(out1);
        Prop(SiteProps.DroppedGlass(), 2 * gap + 0.2f, -0.5f, 200);
        Label("Passed out\n(needs first aid)", 2 * gap + 0.3f, 1.1f, 30);
    }

    private void Ring(float radius, int count, float skipFrom = 400, float skipTo = 400)
    {
        for (var i = 0; i < count; i++)
        {
            var angle = (i * 360f / count) + ((_seed * 37) % 11);
            if ((angle % 360 > skipFrom && angle % 360 < skipTo) || angle % 360 > 318)
            {
                continue;
            }

            var r = radius + (((i * 53) % 10) / 10f * 0.9f);
            var x = Mathf.Sin(Mathf.DegToRad(angle)) * r;
            var z = Mathf.Cos(Mathf.DegToRad(angle)) * r;
            // Everyone has stepped back and turned to look.
            var pose = i % 5 == 0 ? Pose.Cheering : Pose.Standing;
            Person(x, z, angle + 180, pose, i % 4 == 0 ? 0.4f : 0, i % 3 == 0 ? Carried.Pint : Carried.None);
        }
    }

    private void Fight()
    {
        Person(-0.5f, 0, 90, Pose.Fighting, 0.5f, Carried.None, false);
        Person(0.5f, 0, -90, Pose.Fighting, 0.4f, Carried.None, false);
        Prop(SiteProps.DroppedGlass(), -0.9f, 0.7f, 40);
        Prop(SiteProps.DroppedGlass(), 0.7f, -0.8f, 250);
        Ring(2.5f, 14, -1, 80);
        var marker = SiteProps.TroubleMarker(1.6f);
        marker.Position = new Vector3(0, 0, 0);
        marker.RotationDegrees = new Vector3(0, 0, 0);
        _set!.AddChild(marker);
        StaffAt(StaffKind.Security, 2.6f, 2.4f, -134, Pose.Walking, 3);
        StaffAt(StaffKind.Security, 3.5f, 1.6f, -122, Pose.Walking, 8);
        Label("A fight: people step back and watch.\nSecurity come running.", 0, 3.9f);
    }

    private void BrokenUp()
    {
        // Two security walk the two who were fighting away to the gate, one each, well apart.
        Person(-2.6f, -0.4f, -90, Pose.Walking, 0.4f, Carried.None, false, 0.2f);
        StaffAt(StaffKind.Security, -2.05f, -0.15f, -90, Pose.Walking, 3);
        Person(-0.3f, 1.1f, -90, Pose.Walking, 0.5f, Carried.None, false, 0.7f);
        StaffAt(StaffKind.Security, 0.25f, 1.35f, -90, Pose.Walking, 8);
        Label("Thrown out: walked to the gate by security", -1.4f, 2.6f);

        // The one who came off worst sits on the grass with a first aider.
        var hurt = Person(3.3f, -0.6f, 20, Pose.Sitting, 0, Carried.None, false);
        hurt.SeatHeight = 0.02f;
        hurt.SetPose(Pose.Sitting);
        StaffAt(StaffKind.FirstAider, 4.15f, -0.5f, -70, Pose.Standing, 5);
        Label("Anyone hurt gets a first aider", 3.6f, 1.6f);
        Prop(SiteProps.DroppedGlass(), 2.2f, 0.4f, 120);
    }

    private void Sick()
    {
        Person(-0.9f, 0, 20, Pose.BeingSick, 0, Carried.None, false);
        Prop(SiteProps.SickPuddle(4, 0.6f), -0.72f, 0.72f);
        Person(-1.6f, -0.2f, 60, Pose.Standing, 0.3f, Carried.Pint, true);
        Label("Being sick\n(too much drink, sun or a bad burger)", -1.0f, 1.9f, 30);

        // Afterwards the puddle stays until a cleaner comes, and people walk round it.
        Prop(SiteProps.SickPuddle(9, 0.65f), 2.5f, 0.4f);
        Person(1.5f, -0.5f, 100, Pose.Walking, 0, Carried.Fries, true, 0.2f);
        Person(3.5f, 1.3f, -60, Pose.Walking, 0, Carried.None, false, 0.6f);
        Person(2.9f, -0.9f, 20, Pose.Standing, 0, Carried.None, true);
        Label("The mess stays: people walk round it\nand their mood drops", 2.6f, 2.3f, 30);
    }

    private void Cleaner()
    {
        Prop(SiteProps.SickPuddle(9, 0.65f), 0, 0.5f);
        var cleaner = StaffAt(StaffKind.Cleaner, -0.1f, -0.35f, 0, Pose.Standing, 4);
        cleaner.GetNode<Node3D>("Uniform").Visible = false;
        var person = cleaner.GetChild<PersonView>(0);
        var h = person.StandingHeight / 1.6f;
        cleaner.AddChild(SiteProps.MoppingKit(h));
        var (left, right) = SiteProps.MopHands(h);
        person.Perform(left, right, 0, new Vector3(10, 0, 0));
        person.SetPose(Pose.Performing);
        Prop(SiteProps.MopBucket(), -1.2f, 0.2f, 20);
        Person(2.2f, 0.2f, -100, Pose.Standing, 0, Carried.IceCream, true);
        Label("A cleaner mops it up", -0.3f, 1.7f);
    }
}
