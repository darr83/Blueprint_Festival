using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Pictures of the entertainers, run with: godot --path game -- --busk-shots=DIR (needs a display).
/// Each one starts a real festival, hires the guitarist, pianist, drummer and DJ through the game's
/// rules among the food stalls, runs the day on into a set and takes a picture.
/// </summary>
public partial class BuskShots : Node
{
    private static readonly (string Name, double Hour, int View)[] Scenes =
    {
        ("1_the_guitarist", 12.4, 0),
        ("2_the_pianist", 12.4, 1),
        ("3_the_drummer", 12.4, 2),
        ("4_the_dj", 12.4, 3),
        ("5_round_the_food", 14.35, 4),
        ("6_picking_their_hours", 11.5, 5),
        ("7_the_staff_tab", 8.1, 6),
    };

    /// <summary>Where each entertainer stands, in tiles from the field's corner: guitarist, pianist, drummer, DJ.</summary>
    private static readonly (StaffKind Kind, int X, int Y)[] Pitches =
    {
        (StaffKind.Guitarist, 16, 30), (StaffKind.Pianist, 44, 30), (StaffKind.Drummer, 16, 62), (StaffKind.Dj, 44, 62),
    };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;

    public BuskShots(string directory)
    {
        _directory = directory;
    }

    public BuskShots() : this("shots")
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

        if (_frame == 90)
        {
            var path = $"{_directory}/{Scenes[_scene].Name}.png";
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"[busk] {path}");
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
            GD.Print("[busk] Done.");
            GetTree().Quit(0);
            return;
        }

        GameManager.Instance.StartNewGame(null, null, 9);
    }

    /// <summary>A village festival with a farm trailer in the top corner, food along the top and the four entertainers on the grass.</summary>
    public static void Build(FestivalGame game)
    {
        var f = game.Field.Field;
        var gap = game.Field.Gap;
        game.BuildStage(StageKind.FarmTrailer, f.X + 98, f.Y + 4, Facing.South);
        game.BuildFence(FenceKind.Paling, new TilePoint(gap.X, gap.Y + 1), new TilePoint(gap.Right - 1, gap.Y + 1));
        game.BuildGate(GateKind.VillageGate, gap.X + 2, gap.Y + 1, false);
        game.BuildStall(StallKind.Burgers, f.X + 6, f.Y + 6, Facing.South);
        game.BuildStall(StallKind.Pizza, f.X + 24, f.Y + 6, Facing.South);
        game.BuildStall(StallKind.Bar, f.X + 38, f.Y + 4, Facing.South);
        game.BuildStall(StallKind.IceCream, f.X + 56, f.Y + 6, Facing.South);
        game.BuildStall(StallKind.Toilets, f.X + 72, f.Y + 70, Facing.North);
        game.BuildStall(StallKind.Bin, f.X + 30, f.Y + 20, Facing.South);
        foreach (var (x, y) in new[] { (26, 40), (34, 40), (26, 48) })
        {
            game.BuildComfort(ComfortKind.PicnicTable, f.X + x, f.Y + y, Facing.South);
        }

        foreach (var (kind, x, y) in Pitches)
        {
            var hired = game.HireStaff(kind, new TilePoint(f.X + x, f.Y + y));
            if (!hired.Ok)
            {
                GD.PrintErr($"[busk] {kind}: {hired.Problem}");
            }
        }

        foreach (var member in game.Entertainers)
        {
            game.SetBuskHour(member, 13, true);
        }
    }

    private void Arrange(FestivalScene festival)
    {
        var (name, hour, view) = Scenes[_scene];
        var game = festival.Game;
        var f = game.Field.Field;
        game.SetWeather(DayWeather.Of(WeatherKind.Fine, 3));
        Build(game);
        var until = game.Now.Date.AddHours(hour);
        while (game.Now < until)
        {
            game.Clock.Step();
        }

        game.Clock.SetSpeed(GameSpeed.Normal);
        festival.Crowd.Snap();
        festival.Staff.Snap();
        festival.SetHudShown(view >= 5);
        const float t = (float)FieldMap.TileMetres;
        var left = f.X * t;
        var top = f.Y * t;
        if (view < 4)
        {
            var (_, x, y) = Pitches[view];
            festival.Camera.LookCloser(left + (x * t), top + (y * t) + 2.2f, 7.5f);
        }
        else if (view == 4)
        {
            festival.Camera.LookAt(left + 16, top + 22, 34f);
        }
        else if (view == 5)
        {
            festival.Camera.LookCloser(left + 9, top + 16, 12f);
            foreach (var member in game.Entertainers)
            {
                festival.BuskTimes.Open(member);
                break;
            }
        }
        else
        {
            festival.Camera.LookAt(left + 16, top + 22, 30f);
            festival.BuildBar.Toggle(BuildMode.Staff);
        }

        festival.Camera.Snap();
        var listening = 0;
        foreach (var member in game.Entertainers)
        {
            listening += game.ListeningTo(member);
        }

        GD.Print($"[busk] {name}: {festival.Staff.Count} drawn, {listening} listening, {game.BuskListeners} stopped so far, £{game.Money}");
    }
}
