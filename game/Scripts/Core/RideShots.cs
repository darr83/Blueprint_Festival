using System;
using System.Linq;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Pictures of the fairground, run with: godot --path game -- --ride-shots=DIR (needs a display).
/// Each one starts a real festival, hires the rides through the game's rules, runs the day and
/// takes a picture, so what is shown is what the game does.
/// </summary>
public partial class RideShots : Node
{
    private static readonly string[] Scenes =
    {
        "1_the_fairground_shelf", "2_the_fairground", "3_big_wheel", "4_waltzer", "5_twister", "6_carousel", "7_skylark", "8_hook_a_duck", "9_setting_a_price",
    };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;

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
                f.Camera.Snap();
                f.Crowd.Snap();
                f.Staff.Snap();
                f.Weather.Settle();
                f.Sky.Apply(f.Game, f.Camera.Yaw, settle: true);
                _ready = true;
                _frame = 0;
            }

            return;
        }

        if (_frame == 2 && GetTree().CurrentScene is FestivalScene again)
        {
            again.Crowd.Snap();
        }

        if (_frame == 30)
        {
            var path = $"{_directory}/{Scenes[_scene]}.png";
            GetViewport().GetTexture().GetImage().SavePng(path);
            var festival = GetTree().CurrentScene as FestivalScene;
            GD.Print($"[rides] {path}: drawn on rides {festival?.Crowd.Riders}, rides going {festival?.Site.RidesRunning}");
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
            GD.Print("[rides] Done.");
            GetTree().Quit(0);
            return;
        }

        var only = OS.GetEnvironment("SHOTS_ONLY");
        if (only.Length > 0 && !Scenes[_scene].Contains(only, StringComparison.Ordinal))
        {
            NextScene();
            return;
        }

        // A third-year festival of 800, with money in the bank for a whole fairground.
        GameManager.Instance.Open(FestivalGame.StartYear(null, new YearSetup(3, 800, 14000, false, 82)));
    }

    private static int Hire(FestivalGame game, StallKind kind, int x, int y, Facing facing = Facing.South)
    {
        var f = game.Field.Field;
        var check = game.BuildStall(kind, f.X + x, f.Y + y, facing);
        if (!check.Ok)
        {
            GD.Print($"[rides] {kind} at {x},{y}: {check.Problem}");
            return -1;
        }

        return game.Site.Stalls.Count - 1;
    }

    private static Vector3 MiddleOf(FestivalGame game, StallKind kind) => SiteView.Middle(game.Site.Stalls.First(s => s.Kind == kind).Rect);

    private void Arrange(FestivalScene festival)
    {
        var game = festival.Game;
        var f = game.Field.Field;
        var gap = game.Field.Gap;
        var name = Scenes[_scene];
        festival.Year.PickHeadliner(0);
        festival.Year.StartBuilding();
        game.SetWeather(DayWeather.Of(WeatherKind.Fine, 3));

        // A festival to hold the fairground: a gate, a stage at the top, food, drink and toilets down the east side.
        var y = gap.Y + 1;
        game.BuildFence(FenceKind.SteelMesh, new TilePoint(gap.X, y), new TilePoint(gap.Right - 1, y));
        var arch = Catalogue.Of(GateKind.ArchGate);
        for (var x = gap.X; x + arch.Width <= gap.Right; x += arch.Width)
        {
            game.BuildGate(GateKind.ArchGate, x, y, false);
            for (var lane = 0; lane < arch.Lanes; lane++)
            {
                game.HireStaff(StaffKind.TicketStaff, new TilePoint(x + (lane * arch.Width / arch.Lanes) + 1, y));
            }
        }

        game.BuildStage(StageKind.HiredStage, f.X + (f.Width / 2) - 12, f.Y + 4, Facing.South);
        var east = f.Width - 18;
        Hire(game, StallKind.Burgers, east, 30, Facing.West);
        Hire(game, StallKind.Bar, east - 3, 48, Facing.West);
        Hire(game, StallKind.ToiletBlock, east + 10, 66, Facing.West);
        Hire(game, StallKind.WaterPoint, east + 10, 96, Facing.West);
        Hire(game, StallKind.Pizza, east, 104, Facing.West);
        game.HireStaff(StaffKind.Security, new TilePoint(f.X + 60, f.Y + 60));
        game.HireStaff(StaffKind.FirstAider, new TilePoint(f.X + 70, f.Y + 70));
        Hire(game, StallKind.FirstAidTent, east - 10, 8);

        // The fairground, down the west side and across the bottom.
        var south = f.Height - 40;
        Hire(game, StallKind.BigWheel, 6, 36);
        Hire(game, StallKind.Skylark, 50, 22);
        Hire(game, StallKind.Waltzer, 6, south - 40);
        Hire(game, StallKind.Twister, 46, south - 40);
        Hire(game, StallKind.Carousel, 86, south - 28);
        Hire(game, StallKind.HookADuck, 64, south + 2);
        GD.Print($"[rides] {name}: £{game.Money} left, field {f.Width}x{f.Height}");

        if (name == "1_the_fairground_shelf")
        {
            festival.Camera.ShowWholeField();
            festival.BuildBar.Toggle(BuildMode.Stall);
            festival.BuildBar.ShowShelf(StallGroup.Fair);
            festival.BuildBar.Choose("stallCarousel");
            return;
        }

        if (name == "9_setting_a_price")
        {
            var at = MiddleOf(game, StallKind.Skylark);
            festival.Camera.LookAt(at.X + 6, at.Z + 4, 34f);
            festival.StallMenu.Open(game.Site.Stalls.ToList().FindIndex(s => s.Kind == StallKind.Skylark));
            festival.StallMenu.Nudge(0, 1);
            return;
        }

        game.OpenGates();
        var kind = name switch
        {
            "3_big_wheel" => StallKind.BigWheel,
            "4_waltzer" => StallKind.Waltzer,
            "5_twister" => StallKind.Twister,
            "6_carousel" => StallKind.Carousel,
            "7_skylark" => StallKind.Skylark,
            "8_hook_a_duck" => StallKind.HookADuck,
            _ => (StallKind?)null,
        };

        // Run to the early afternoon, then on until the ride in the picture is going round with people on it.
        var until = game.Day.AddHours(13.5);
        while (game.Phase == Phase.Day && game.Now < until)
        {
            game.Clock.Step();
        }

        if (kind is { } wanted)
        {
            var stall = game.Site.Stalls.ToList().FindIndex(s => s.Kind == wanted);
            var placed = game.Site.Stalls[stall];
            var last = game.Day.AddHours(17);
            // Give the view a few looks at the ride turning, so it is caught part of the way round.
            festival.Site.Rebuild();
            while (game.Now < last)
            {
                game.Clock.Step();
                festival.Site.TurnRides(0, snap: true);
                var turn = Rides.TurnAt(placed, game.Now);
                if (wanted == StallKind.HookADuck ? game.Crowd.BeingServed(stall) >= 2 && game.Crowd.TeddiesWon > 0
                    : !turn.Boarding && turn.Running >= 50 && game.Crowd.Riding(stall) >= Math.Min(6, Catalogue.Of(wanted).Servers * 2))
                {
                    break;
                }
            }

            var at = SiteView.Middle(placed.Rect);
            var (up, size) = wanted switch
            {
                StallKind.BigWheel => (8f, 27f),
                StallKind.Skylark => (11f, 33f),
                StallKind.Waltzer => (3f, 20f),
                StallKind.Twister => (2.5f, 19f),
                StallKind.Carousel => (3f, 15f),
                _ => (1.5f, 10f),
            };
            // The camera looks down from the south-east: aiming further off raises the view over a tall ride.
            var d = up / MathF.Tan(Mathf.DegToRad(CameraRig.PitchDegrees)) / MathF.Sqrt(2);
            festival.Camera.LookCloser(at.X - d, at.Z - d, size);
            GD.Print($"[rides] {name}: {game.Now:HH:mm}, riding {game.Crowd.Riding(stall)}, served {game.Crowd.BeingServed(stall)}, queue {game.Crowd.StallQueueLength(stall)}, sold {game.SalesOf(stall).Items}, teddies {game.Crowd.TeddiesWon}");
        }
        else
        {
            for (var i = 0; i < 900; i++)
            {
                game.Clock.Step();
                festival.Site.TurnRides(0, snap: true);
            }

            festival.Camera.LookAt((f.X + 62) * 0.5f, (f.Y + (f.Height / 2) + 30) * 0.5f, 62f);
        }

        game.Clock.SetSpeed(GameSpeed.Paused);
        var fair = Enumerable.Range(0, game.Site.Stalls.Count).Where(i => Rides.IsFair(game.Site.Stalls[i].Kind)).ToList();
        GD.Print($"[rides] {name}: on site {game.Crowd.OnSite}, happy {game.Crowd.Happiness}%, fair goes {string.Join(", ", fair.Select(i => $"{game.Site.Stalls[i].Kind} {game.SalesOf(i).Items}"))}, teddies {game.Crowd.TeddiesWon}");
    }
}
