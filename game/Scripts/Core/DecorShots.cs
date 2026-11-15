using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Pictures of the decorations, run with: godot --path game -- --decor-shots=DIR (needs
/// a display). Each one starts a real festival, dresses it
/// through the game's rules and takes a picture, so what is shown is what the game does.
/// </summary>
public partial class DecorShots : Node
{
    private static readonly string[] Scenes = { "1_the_decorations_shelf", "2_round_the_field", "3_the_fire_pit_at_dusk" };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;

    public DecorShots(string directory)
    {
        _directory = directory;
    }

    public DecorShots() : this("shots")
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
            var path = $"{_directory}/{Scenes[_scene]}.png";
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"[decor] {path}");
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
            GD.Print("[decor] Done.");
            GetTree().Quit(0);
            return;
        }

        GameManager.Instance.StartNewGame(null, null, 9);
    }

    private static void Put(FestivalGame game, DecorKind kind, double x, double y, Facing facing = Facing.South)
    {
        var f = game.Field.Field;
        var check = game.BuildDecor(kind, f.X + (int)(x * 2), f.Y + (int)(y * 2), facing);
        if (!check.Ok)
        {
            GD.Print($"[decor] {kind} at {x},{y}: {check.Problem}");
        }
    }

    private static void String(FestivalGame game, double x, double y, double toX, double toY)
    {
        var f = game.Field.Field;
        var check = game.BuildBunting(new TilePoint(f.X + (int)(x * 2), f.Y + (int)(y * 2)), new TilePoint(f.X + (int)(toX * 2), f.Y + (int)(toY * 2)));
        if (!check.Ok)
        {
            GD.Print($"[decor] bunting at {x},{y}: {check.Problem}");
        }
    }

    private void Arrange(FestivalScene festival)
    {
        var game = festival.Game;
        var f = game.Field.Field;
        game.SetWeather(DayWeather.Of(WeatherKind.Fine, 3));
        // A small festival, to leave money for the decorations.
        var gap = game.Field.Gap;
        game.BuildStage(StageKind.Bandstand, f.X + 74, f.Y + 62, Facing.North);
        game.BuildFence(FenceKind.Paling, new TilePoint(gap.X, gap.Y + 1), new TilePoint(gap.Right - 1, gap.Y + 1));
        game.BuildGate(GateKind.VillageGate, gap.X + 2, gap.Y + 1, false);
        game.BuildStall(StallKind.Burgers, f.X + 8, f.Y + 8, Facing.South);
        game.BuildStall(StallKind.Bar, f.X + 40, f.Y + 6, Facing.South);
        game.BuildStall(StallKind.Toilets, f.X + 62, f.Y + 8, Facing.South);
        game.BuildComfort(ComfortKind.PicnicTable, f.X + 12, f.Y + 26, Facing.South);
        game.BuildComfort(ComfortKind.PicnicTable, f.X + 28, f.Y + 26, Facing.South);
        GD.Print($"[decor] £{game.Money} left for decorations");

        // Dressed through the game's own rules (positions in metres from the field's corner).
        Put(game, DecorKind.GiantLetters, 8, 18.6);
        Put(game, DecorKind.GiantSunflower, 3, 12);
        Put(game, DecorKind.GiantSunflower, 3, 16);
        Put(game, DecorKind.FlagPole, 3, 9.5);
        Put(game, DecorKind.FlagPole, 20.5, 9.5);
        Put(game, DecorKind.FlowerPlanter, 8, 9.5);
        Put(game, DecorKind.FlowerPlanter, 12, 9.5);
        Put(game, DecorKind.FlowerPlanter, 16, 9.5);
        String(game, 4, 24, 12, 24);
        String(game, 12, 24, 20, 24);
        Put(game, DecorKind.Scarecrow, 6, 29.5);
        Put(game, DecorKind.FirePit, 15, 30.5);
        foreach (var (x, y, facing) in new[] { (12.5, 30.5, Facing.East), (17.5, 30.5, Facing.West), (15, 33.0, Facing.North) })
        {
            game.BuildComfort(ComfortKind.StrawBale, f.X + (int)(x * 2), f.Y + (int)(y * 2), facing);
        }

        var until = game.Now.Date.AddHours(_scene == 2 ? 21.3 : 13.2);
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
        switch (_scene)
        {
            case 0:
                festival.Camera.LookAt(left + 14, top + 16, 26f);
                festival.BuildBar.Toggle(BuildMode.Sign);
                festival.BuildBar.ShowDecorShelf(true);
                festival.BuildBar.Choose(UI.BuildBar.BuntingKey);
                break;
            case 1:
                festival.Camera.LookCloser(left + 12.5f, top + 17.5f, 15f);
                break;
            default:
                festival.Camera.LookCloser(left + 15.5f, top + 31.5f, 6.5f);
                break;
        }

        var crowd = game.Crowd;
        GD.Print($"[decor] {Scenes[_scene]}: £{game.Money}, happy {crowd.Happiness}, decorations {game.Site.Decorations.Count}, bunting {game.Site.Bunting.Count}, drawn {festival.Site.DecorCount}");
        festival.Camera.Snap();
    }
}
