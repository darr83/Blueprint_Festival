using System;
using System.Collections.Generic;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Mock-up pictures of the fairground rides, run with: godot --path game -- --ride-shots=DIR
/// (needs a display). The rides are stood in the field with riders sat in them, to show how they look.
/// </summary>
public partial class RideShots : Node
{
    private static readonly string[] Names = { "1_big_wheel", "2_waltzer", "3_twister", "4_carousel", "5_skylark", "6_hook_a_duck", "7_the_fairground" };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private FestivalScene? _festival;
    private Node3D? _set;
    private int _seed;

    public RideShots(string directory)
    {
        _directory = directory;
    }

    public RideShots() : this("shots")
    {
    }

    public override void _Ready()
    {
        DirAccess.MakeDirRecursiveAbsolute(_directory);
        Callable.From(() => GameManager.Instance.StartNewGame(null, new DateTime(2027, 7, 17, 13, 0, 0))).CallDeferred();
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
            GD.Print($"[rides] {path}");
            Next();
        }
    }

    private void Next()
    {
        _scene++;
        _frame = 0;
        _set?.Free();
        _set = null;
        if (_scene >= Names.Length)
        {
            GD.Print("[rides] Done.");
            GetTree().Quit(0);
            return;
        }

        var only = OS.GetEnvironment("SHOTS_ONLY");
        if (!string.IsNullOrEmpty(only) && !Names[_scene].Contains(only, StringComparison.Ordinal))
        {
            Next();
            return;
        }

        var festival = _festival!;
        var field = festival.Game.Field.Field;
        var centre = new Vector3((field.X + (field.Width / 2f)) * 0.5f, 0, (field.Y + (field.Height / 2f)) * 0.5f);
        // Turned a little to the camera, which looks from the south-east.
        _set = new Node3D { Name = "Set", Position = centre, RotationDegrees = new Vector3(0, 30, 0) };
        festival.AddChild(_set);
        _seed = 100 * (_scene + 1);
        switch (_scene)
        {
            case 0:
                Stand(RideKind.BigWheel, Vector3.Zero, 3.3f, 0.75f);
                Queue(new Vector3(0, 0, 5.2f), 6);
                Look(festival, centre, 0, 9.5f, 26f);
                break;
            case 1:
                Stand(RideKind.Waltzer, Vector3.Zero, 1.3f, 0.85f);
                Queue(new Vector3(-1, 0, 9.4f), 5);
                Look(festival, centre, 0, 3.6f, 17f);
                break;
            case 2:
                Stand(RideKind.Twister, Vector3.Zero, 0.6f, 0.8f);
                Queue(new Vector3(-0.5f, 0, 8.6f), 5);
                Look(festival, centre, 0, 2.6f, 15f);
                break;
            case 3:
                Stand(RideKind.Carousel, Vector3.Zero, 0.9f, 0.7f);
                Queue(new Vector3(0.5f, 0, 6.2f), 5);
                Look(festival, centre, 0, 3.2f, 12.5f);
                break;
            case 4:
                Stand(RideKind.Skylark, Vector3.Zero, 0.95f, 1f);
                Queue(new Vector3(-4.6f, 0, 5.6f), 6);
                Look(festival, centre, 0, 13f, 33f);
                break;
            case 5:
                Stand(RideKind.HookADuck, Vector3.Zero, 0, 0);
                Players();
                Look(festival, centre, 0.6f, 2.0f, 8.2f);
                break;
            default:
                Stand(RideKind.BigWheel, new Vector3(-12, 0, -14), 2.1f, 0.7f);
                Stand(RideKind.Skylark, new Vector3(13, 0, -13), 0.55f, 1f);
                Stand(RideKind.Waltzer, new Vector3(-15, 0, 6), 2.2f, 0.8f);
                Stand(RideKind.Twister, new Vector3(4, 0, 6), 1.7f, 0.8f);
                Stand(RideKind.Carousel, new Vector3(19, 0, 5), 0.4f, 0.7f);
                Stand(RideKind.HookADuck, new Vector3(-5, 0, 15.5f), 0, 0);
                Stroll(30);
                Look(festival, centre, 2, 9f, 46f);
                break;
        }

        festival.Camera.Snap();
    }

    /// <summary>Points the camera at a spot in the set, lifted so tall rides fit in the picture.</summary>
    private static void Look(FestivalScene festival, Vector3 centre, float along, float up, float size)
    {
        // The camera looks down at 30 degrees from the south-east: looking at a point further off raises the view.
        var back = up / MathF.Tan(Mathf.DegToRad(CameraRig.PitchDegrees));
        var d = back / MathF.Sqrt(2);
        festival.Camera.LookCloser(centre.X + along - d, centre.Z - d, size);
    }

    /// <summary>Stands a ride in the set at a moment of its turn, with riders in a share of its seats.</summary>
    private void Stand(RideKind kind, Vector3 at, float seconds, float full)
    {
        var ride = SiteProps.Ride(kind);
        ride.Position = at;
        _set!.AddChild(ride);
        SiteProps.TurnRide(ride, kind, seconds);
        var seats = new List<Node3D>();
        Seats(ride, seats);
        var random = new Random(_seed + (int)kind);
        foreach (var seat in seats)
        {
            if (random.NextDouble() > full)
            {
                continue;
            }

            var age = kind == RideKind.Carousel
                ? (random.Next(3) == 0 ? AgeGroup.Adult : AgeGroup.Child)
                : (AgeGroup)random.Next(1, 3);
            var person = PersonView.Build(LookGenerator.FestivalGoer(_seed++, age));
            person.SeatHeight = 0.45f;
            seat.AddChild(person);
            person.SetPose(kind != RideKind.Carousel && random.Next(3) == 0 ? Pose.Cheering : Pose.Sitting);
            if (person.Pose == Pose.Cheering)
            {
                // Arms up, but still sat down.
                person.Position = new Vector3(0, 0.45f - (person.StandingHeight * 0.42f), 0);
            }
        }
    }

    private static void Seats(Node node, List<Node3D> found)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Node3D n && n.Name.ToString().StartsWith("Seat", StringComparison.Ordinal) && !n.Name.ToString().StartsWith("Seats", StringComparison.Ordinal))
            {
                found.Add(n);
            }
            else
            {
                Seats(child, found);
            }
        }
    }

    /// <summary>People waiting in a line that runs away to the south.</summary>
    private void Queue(Vector3 head, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var person = PersonView.Build(LookGenerator.FestivalGoer(_seed++, (AgeGroup)(_seed % 4)));
            person.Position = head + new Vector3(0.12f * (i % 2), 0, i * 0.75f);
            person.RotationDegrees = new Vector3(0, 180, 0);
            person.SetPose(Pose.Standing);
            _set!.AddChild(person);
        }
    }

    /// <summary>People playing hook a duck, and two walking off with the teddies they won.</summary>
    private void Players()
    {
        foreach (var (angle, age) in new[] { (15f, AgeGroup.Child), (-25f, AgeGroup.Adult), (60f, AgeGroup.Teenager), (100f, AgeGroup.Child) })
        {
            var a = Mathf.DegToRad(angle);
            var person = PersonView.Build(LookGenerator.FestivalGoer(_seed++, age));
            person.Position = new Vector3(2.85f * MathF.Sin(a), 0, 2.85f * MathF.Cos(a));
            person.RotationDegrees = new Vector3(0, angle + 180, 0);
            person.SetPose(Pose.ShowingTicket);
            _set!.AddChild(person);
        }

        var keeper = SiteProps.Stallholder(7);
        keeper.Position = new Vector3(-3.1f, 0, 1.5f);
        keeper.RotationDegrees = new Vector3(0, 20, 0);
        _set!.AddChild(keeper);
        Winner(new Vector3(-1.6f, 0, 4.6f), AgeGroup.Child, 0, -20);
        Winner(new Vector3(-0.7f, 0, 4.9f), AgeGroup.Adult, 1, -20);
        Winner(new Vector3(3.6f, 0, 3.6f), AgeGroup.Teenager, 4, 60);
    }

    private void Winner(Vector3 at, AgeGroup age, int teddy, float turn)
    {
        var person = PersonView.Build(LookGenerator.FestivalGoer(_seed++, age));
        person.Position = at;
        person.RotationDegrees = new Vector3(0, turn, 0);
        person.SetPose(Pose.Walking, 0.2f);
        var prize = SiteProps.WonTeddy(teddy);
        prize.Position = new Vector3(0, person.StandingHeight * 0.36f, 0.2f);
        person.AddChild(prize);
        _set!.AddChild(person);
    }

    /// <summary>People walking about between the rides.</summary>
    private void Stroll(int count)
    {
        var random = new Random(9);
        for (var i = 0; i < count; i++)
        {
            var person = PersonView.Build(LookGenerator.FestivalGoer(_seed++, (AgeGroup)random.Next(4)));
            person.Position = new Vector3(-20 + (float)(random.NextDouble() * 44), 0, 12.5f + (float)(random.NextDouble() * 5));
            if (i % 3 == 0)
            {
                person.Position = new Vector3(-5.5f + (float)(random.NextDouble() * 3), 0, -8 + (float)(random.NextDouble() * 20));
            }

            person.RotationDegrees = new Vector3(0, random.Next(360), 0);
            person.SetPose(Pose.Walking, (float)random.NextDouble());
            if (i % 6 == 0)
            {
                var prize = SiteProps.WonTeddy(i);
                prize.Position = new Vector3(0, person.StandingHeight * 0.36f, 0.2f);
                person.AddChild(prize);
            }

            _set!.AddChild(person);
        }
    }
}
