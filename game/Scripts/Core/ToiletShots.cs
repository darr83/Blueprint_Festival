using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Pictures of the toilets, run with: godot --path game -- --toilet-shots=DIR (needs a display).
/// Builds every kind of toilet in a real festival through the game's rules and takes pictures.
/// </summary>
public partial class ToiletShots : Node
{
    private static readonly (string Name, double Hour)[] Scenes =
    {
        ("1_all_the_toilets", 13.5),
        ("2_back_to_back_front", 13.5),
        ("3_back_to_back_behind", 13.5),
        ("4_accessible_toilet", 13.5),
        ("5_urinal_stand", 13.5),
        ("6_urinal_trough", 13.5),
    };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;

    public ToiletShots(string directory)
    {
        _directory = directory;
    }

    public ToiletShots() : this("shots")
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

        if (_frame == 60)
        {
            var path = $"{_directory}/{Scenes[_scene].Name}.png";
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"[toilets] {path}");
            NextScene();
        }
    }

    private void NextScene()
    {
        _scene++;
        while (_scene < Scenes.Length && System.Environment.GetEnvironmentVariable("SHOTS_ONLY") is { Length: > 0 } only && !Scenes[_scene].Name.Contains(only))
        {
            _scene++;
        }

        _ready = false;
        _frame = 0;
        if (_scene >= Scenes.Length)
        {
            GD.Print("[toilets] Done.");
            GetTree().Quit(0);
            return;
        }

        GameManager.Instance.StartNewGame(null, null, 9);
    }

    /// <summary>Builds the festival the pictures are taken of: every kind of toilet in a row along the top of the field.</summary>
    public static void Build(FestivalGame game)
    {
        var f = game.Field.Field;
        var gap = game.Field.Gap;
        game.BuildStage(StageKind.Bandstand, f.X + 74, f.Y + 62, Facing.North);
        game.BuildFence(FenceKind.Paling, new TilePoint(gap.X, gap.Y + 1), new TilePoint(gap.Right - 1, gap.Y + 1));
        game.BuildGate(GateKind.VillageGate, gap.X + 2, gap.Y + 1, false);
        game.BuildStall(StallKind.Burgers, f.X + 8, f.Y + 30, Facing.South);
        game.BuildStall(StallKind.Bar, f.X + 26, f.Y + 30, Facing.South);
        foreach (var (kind, x, y) in new[]
        {
            (StallKind.Toilets, 4, 8),
            (StallKind.BackToBackToilets, 16, 8),
            (StallKind.AccessibleToilet, 36, 8),
            (StallKind.UrinalStand, 46, 9),
            (StallKind.UrinalTrough, 56, 9),
        })
        {
            var check = game.BuildStall(kind, f.X + x, f.Y + y, Facing.South);
            if (!check.Ok)
            {
                GD.Print($"[toilets] {kind}: {check.Problem}");
            }
        }
    }

    private void Arrange(FestivalScene festival)
    {
        var scene = Scenes[_scene];
        var game = festival.Game;
        var f = game.Field.Field;
        game.SetWeather(DayWeather.Of(WeatherKind.Fine, 3));
        Build(game);
        var until = game.Now.Date.AddHours(scene.Hour);
        while (game.Now < until)
        {
            game.Clock.Step();
        }

        game.Clock.SetSpeed(GameSpeed.Normal);
        festival.Crowd.Snap();
        festival.SetHudShown(_scene == 0);
        const float t = (float)FieldMap.TileMetres;
        var left = f.X * t;
        var top = f.Y * t;
        switch (_scene)
        {
            case 0:
                festival.Camera.LookAt(left + 18, top + 8, 24f);
                festival.BuildBar.Toggle(BuildMode.Stall);
                festival.BuildBar.ShowShelf(StallGroup.Services);
                break;
            case 1:
                festival.Camera.LookCloser(left + 12, top + 6, 8f);
                break;
            case 2:
                festival.Camera.Turn(2);
                festival.Camera.LookCloser(left + 12, top + 5, 8f);
                break;
            case 3:
                festival.Camera.LookCloser(left + 19.5f, top + 6, 5.5f);
                break;
            case 4:
                festival.Camera.LookCloser(left + 24.25f, top + 6, 5.5f);
                break;
            default:
                festival.Camera.LookCloser(left + 31, top + 6, 6.5f);
                break;
        }

        festival.Camera.Snap();
        GD.Print($"[toilets] {scene.Name}: £{game.Money}, stalls {game.Site.Stalls.Count}, on site {game.Crowd.OnSite}");
    }
}
