using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Pictures of the seats, hand washing, shade and new floors, run with:
/// godot --path game -- --comfort-shots=DIR (needs a display). Each one starts a real festival,
/// builds it through the game's rules, runs the day on to a set time and takes a picture, so what
/// is shown is what the game does.
/// </summary>
public partial class ComfortShots : Node
{
    private static readonly (string Name, double Hour, int View, WeatherKind Weather)[] Scenes =
    {
        ("1_the_comforts_tab", 8.1, 1, WeatherKind.Fine),
        ("2_sitting_down_to_eat", 13.3, 2, WeatherKind.Fine),
        ("3_washing_hands_by_the_toilets", 13.3, 3, WeatherKind.Fine),
        ("4_shade_and_deckchairs_on_a_hot_day", 15.0, 4, WeatherKind.Hot),
        ("5_under_the_marquee_in_the_rain", 15.5, 5, WeatherKind.Rain),
        ("6_the_whole_field", 13.3, 6, WeatherKind.Fine),
        ("7_the_paths_tab", 8.1, 7, WeatherKind.Fine),
    };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;

    public ComfortShots(string directory)
    {
        _directory = directory;
    }

    public ComfortShots() : this("shots")
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
            GD.Print($"[comfort] {path}");
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
            GD.Print("[comfort] Done.");
            GetTree().Quit(0);
            return;
        }

        GameManager.Instance.StartNewGame(null, null, 9);
    }

    /// <summary>Builds the festival the pictures are taken of, through the game's own rules.</summary>
    public static void Build(FestivalGame game, bool signs = false)
    {
        var f = game.Field.Field;
        var gap = game.Field.Gap;
        game.BuildStage(StageKind.Bandstand, f.X + 74, f.Y + 62, Facing.North);
        game.BuildFence(FenceKind.Paling, new TilePoint(gap.X, gap.Y + 1), new TilePoint(gap.Right - 1, gap.Y + 1));
        game.BuildGate(GateKind.VillageGate, gap.X + 2, gap.Y + 1, false);

        // Food along the back, with somewhere to sit in front of it on rubber mats.
        game.BuildStall(StallKind.Burgers, f.X + 8, f.Y + 8, Facing.South);
        game.BuildStall(StallKind.Pizza, f.X + 26, f.Y + 8, Facing.South);
        game.BuildStall(StallKind.Bar, f.X + 40, f.Y + 6, Facing.South);
        for (var column = 2; column <= 10; column++)
        {
            for (var row = 5; row <= 8; row++)
            {
                game.LayGround(GroundKind.RubberMats, column, row);
            }
        }

        foreach (var (x, y) in new[] { (9, 22), (15, 22), (9, 29), (15, 29) })
        {
            game.BuildComfort(ComfortKind.PicnicTable, f.X + x, f.Y + y, Facing.South);
        }

        foreach (var (x, y) in new[] { (22, 21), (29, 21), (22, 28), (29, 28) })
        {
            game.BuildComfort(ComfortKind.ParasolTable, f.X + x, f.Y + y, Facing.South);
        }

        game.BuildComfort(ComfortKind.Bench, f.X + 37, f.Y + 22, Facing.West);
        game.BuildComfort(ComfortKind.Bench, f.X + 37, f.Y + 28, Facing.West);
        game.BuildStall(StallKind.Bin, f.X + 5, f.Y + 26, Facing.South);
        game.BuildComfort(ComfortKind.HandGel, f.X + 22, f.Y + 16, Facing.South);

        // Toilets on pallets, with sinks beside them.
        game.BuildStall(StallKind.Toilets, f.X + 62, f.Y + 8, Facing.South);
        for (var column = 15; column <= 19; column++)
        {
            game.LayGround(GroundKind.Pallets, column, 3);
        }

        game.BuildComfort(ComfortKind.HandWash, f.X + 72, f.Y + 9, Facing.South);
        game.BuildComfort(ComfortKind.HandGel, f.X + 60, f.Y + 10, Facing.South);

        // A marquee, a lawn with deckchairs and a sail, straw bales, and the charging tent.
        if (!signs)
        {
            // Left out of the sign pictures, to leave money for the signs.
            game.BuildComfort(ComfortKind.SeatingMarquee, f.X + 46, f.Y + 24, Facing.South);
        }

        for (var column = 2; column <= 7; column++)
        {
            for (var row = 11; row <= 13; row++)
            {
                game.LayGround(column <= 4 ? GroundKind.ArtificialGrass : GroundKind.Sand, column, row);
            }
        }

        game.BuildComfort(ComfortKind.Deckchairs, f.X + 21, f.Y + 46, Facing.South);
        game.BuildComfort(ComfortKind.Deckchairs, f.X + 21, f.Y + 51, Facing.South);
        game.BuildComfort(ComfortKind.ShadeSail, f.X + 34, f.Y + 44, Facing.South);
        game.BuildStall(StallKind.PhoneCharging, f.X + 50, f.Y + 44, Facing.South);
        foreach (var (x, y) in new[] { (60, 44), (64, 44), (62, 48) })
        {
            game.BuildComfort(ComfortKind.StrawBale, f.X + x, f.Y + y, Facing.South);
        }

        // A boardwalk two patches wide from the way in.
        game.LayPath(GroundKind.Boardwalk, (1, 9), (20, 9), 2);
    }

    private void Arrange(FestivalScene festival)
    {
        var scene = Scenes[_scene];
        var game = festival.Game;
        var f = game.Field.Field;
        game.SetWeather(DayWeather.Of(scene.Weather, 3));
        Build(game);
        var until = game.Now.Date.AddHours(scene.Hour);
        while (game.Now < until)
        {
            game.Clock.Step();
        }

        game.Clock.SetSpeed(GameSpeed.Normal);
        festival.Crowd.Snap();
        festival.SetHudShown(true);
        const float t = (float)FieldMap.TileMetres;
        var left = f.X * t;
        var top = f.Y * t;
        switch (scene.View)
        {
            case 1:
                festival.Camera.LookAt(left + 14, top + 12, 26f);
                festival.BuildBar.Toggle(BuildMode.Comfort);
                festival.BuildBar.Choose(UI.BuildBar.ComfortKey(ComfortKind.PicnicTable));
                break;
            case 2:
                festival.Camera.LookCloser(left + 11.5f, top + 12.5f, 11f);
                break;
            case 3:
                festival.Camera.LookCloser(left + 34, top + 6, 9f);
                break;
            case 4:
                festival.Camera.LookCloser(left + 14, top + 24.5f, 13f);
                break;
            case 5:
                festival.Camera.LookCloser(left + 27, top + 15, 11f);
                break;
            case 6:
                festival.Camera.LookAt(left + 20, top + 16, 34f);
                break;
            default:
                festival.Camera.LookAt(left + 14, top + 16, 26f);
                festival.BuildBar.Toggle(BuildMode.Path);
                festival.BuildBar.SetPathWidth(2);
                festival.BuildBar.Choose("groundBoardwalk");
                break;
        }

        festival.Camera.Snap();
        var crowd = game.Crowd;
        GD.Print($"[comfort] {scene.Name}: £{game.Money}, happy {crowd.Happiness}, on site {crowd.OnSite}, seats {game.Seats}, sitting {crowd.Seated}, sat down {crowd.SatDown}, washed {crowd.WashedHands}, hygiene {game.HygienePercent}%, drawn sitting {festival.Crowd.Sitters}");
    }
}
