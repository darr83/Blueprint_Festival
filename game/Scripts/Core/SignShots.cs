using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.UI;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Pictures of the signs, run with: godot --path game -- --sign-shots=DIR (needs a display).
/// Each one starts a real festival, puts the signs up through the game's rules and takes a picture.
/// </summary>
public partial class SignShots : Node
{
    private static readonly string[] Scenes = { "1_the_four_signs", "2_signs_round_the_field", "3_writing_a_sign", "4_the_signs_tab" };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;

    public SignShots(string directory)
    {
        _directory = directory;
    }

    public SignShots() : this("shots")
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
            GD.Print($"[signs] {path}");
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
            GD.Print("[signs] Done.");
            GetTree().Quit(0);
            return;
        }

        GameManager.Instance.StartNewGame(null, null, 9);
    }

    /// <summary>Puts a sign up through the game's rules, with its middle at a place in metres from the field's corner, and writes on it.</summary>
    private static TilePoint Put(FestivalGame game, SignKind kind, float x, float z, Facing facing, string[] words, int colour, int points = 0)
    {
        var f = game.Field.Field;
        var rect = new PlacedSign(kind, 0, 0, facing).Rect;
        var at = new TilePoint(f.X + (int)(x / FieldMap.TileMetres) - (rect.Width / 2), f.Y + (int)(z / FieldMap.TileMetres) - (rect.Height / 2));
        var built = game.BuildSign(kind, at.X, at.Y, facing);
        if (!built.Ok)
        {
            GD.PrintErr($"[signs] {kind} at {x}, {z}: {built.Problem}");
        }

        game.WriteSign(at, words, colour, points);
        return at;
    }

    private void Arrange(FestivalScene festival)
    {
        var game = festival.Game;
        var f = game.Field.Field;
        game.SetWeather(DayWeather.Of(WeatherKind.Fine, 3));
        ComfortShots.Build(game, signs: true);
        const float t = (float)FieldMap.TileMetres;
        var left = f.X * t;
        var top = f.Y * t;
        var post = default(TilePoint);
        if (_scene == 0)
        {
            // The four of them in a row on clear grass.
            Put(game, SignKind.SandwichBoard, 12, 34, Facing.South, new[] { "BURGERS", "2 for £9", "till 3pm" }, 0);
            Put(game, SignKind.FingerPost, 15.5f, 33.5f, Facing.South, new[] { "Main stage", "Toilets", "Food & bar" }, 3, 0b011);
            Put(game, SignKind.Banner, 20.5f, 33, Facing.South, new[] { "WELCOME TO LARKMOOR" }, 1);
            Put(game, SignKind.MapBoard, 26, 33.5f, Facing.South, new[] { "LARKMOOR FESTIVAL" }, 0);
        }
        else
        {
            Put(game, SignKind.SandwichBoard, 16.5f, 8.8f, Facing.South, new[] { "PIZZA", "by the slice", "£4" }, 2);
            Put(game, SignKind.SandwichBoard, 24.5f, 8.8f, Facing.South, new[] { "HAPPY HOUR", "4 till 5" }, 1);
            post = Put(game, SignKind.FingerPost, 20.5f, 17, Facing.South, new[] { "Toilets", "Stage", "Way out" }, 3, 0b011);
            Put(game, SignKind.Banner, 12.5f, 16.8f, Facing.South, new[] { "FOOD COURT" }, 0);
            Put(game, SignKind.MapBoard, 5, 19.5f, Facing.South, new[] { "YOU ARE AT LARKMOOR" }, 0);
            Put(game, SignKind.Banner, 11.5f, 28.5f, Facing.South, new[] { "CHILL OUT LAWN" }, 2);
        }

        var until = game.Now.Date.AddHours(_scene == 3 ? 8.1 : 13.2);
        while (game.Now < until)
        {
            game.Clock.Step();
        }

        game.Clock.SetSpeed(GameSpeed.Normal);
        festival.Crowd.Snap();
        festival.SetHudShown(true);
        switch (_scene)
        {
            case 0:
                festival.Camera.LookCloser(left + 19.5f, top + 34.5f, 9.5f);
                break;
            case 1:
                festival.Camera.LookCloser(left + 14, top + 15.5f, 13f);
                break;
            case 2:
                festival.Camera.LookCloser(left + 15, top + 12, 9f);
                festival.SignBox.Open(post);
                break;
            default:
                festival.Camera.LookAt(left + 14, top + 16, 26f);
                festival.BuildBar.Toggle(BuildMode.Sign);
                festival.BuildBar.Choose(BuildBar.SignKey(SignKind.FingerPost));
                break;
        }

        festival.Camera.Snap();
        GD.Print($"[signs] {Scenes[_scene]}: {game.Site.Signs.Count} signs, {festival.Site.SignCount} drawn, £{game.Money}");
    }
}
