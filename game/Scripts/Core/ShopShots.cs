using System.Linq;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Pictures of the food, drink and merchandise stalls, run with: godot --path game -- --shop-shots=DIR
/// (needs a display). Each one starts a real festival, builds it through the game's rules, runs
/// the day on to a set time and takes a picture, so what is shown is what the game does.
/// </summary>
public partial class ShopShots : Node
{
    private static readonly (string Name, double Hour, int View, WeatherKind Weather)[] Scenes =
    {
        ("1_the_food_stalls", 8.1, 1, WeatherKind.Fine),
        ("2_the_drink_stalls", 8.1, 2, WeatherKind.Fine),
        ("3_ponchos_hats_and_merchandise", 8.1, 3, WeatherKind.Fine),
        ("4_lunchtime_with_food_in_hand", 13.3, 4, WeatherKind.Fine),
        ("5_drinks_in_hand", 15.2, 5, WeatherKind.Hot),
        ("6_setting_the_prices", 8.1, 6, WeatherKind.Fine),
        ("7_ponchos_in_the_rain", 15.5, 7, WeatherKind.Rain),
        ("8_hats_and_bags", 16.5, 8, WeatherKind.Hot),
    };

    private static readonly StallKind[] Food = { StallKind.HotDogs, StallKind.Burgers, StallKind.Fries, StallKind.Pizza, StallKind.Noodles, StallKind.IceCream };
    private static readonly StallKind[] Others = { StallKind.Bar, StallKind.Coffee, StallKind.Slush, StallKind.Cocktails, StallKind.Ponchos, StallKind.BucketHats, StallKind.Merchandise };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;

    public ShopShots(string directory)
    {
        _directory = directory;
    }

    public ShopShots() : this("shots")
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
            GD.Print($"[shops] {path}");
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
            GD.Print("[shops] Done.");
            GetTree().Quit(0);
            return;
        }

        GameManager.Instance.StartNewGame(null, null, 9);
    }

    private void Arrange(FestivalScene festival)
    {
        var scene = Scenes[_scene];
        var game = festival.Game;
        var f = game.Field.Field;
        var gap = game.Field.Gap;
        game.SetWeather(DayWeather.Of(scene.Weather, 3));
        game.BuildStage(StageKind.Bandstand, f.X + 74, f.Y + 62, Facing.North);
        game.BuildFence(FenceKind.Paling, new TilePoint(gap.X, gap.Y + 1), new TilePoint(gap.Right - 1, gap.Y + 1));
        game.BuildGate(GateKind.VillageGate, gap.X + 2, gap.Y + 1, false);

        // Two rows of stalls facing south: food along the back, drink and the shops in front.
        var x = f.X + 8;
        foreach (var kind in Food)
        {
            game.BuildStall(kind, x, f.Y + 8, Facing.South);
            x += Catalogue.Of(kind).Width + 5;
        }

        game.BuildStall(StallKind.Toilets, x + 2, f.Y + 8, Facing.South);
        game.BuildStall(StallKind.Toilets, x + 12, f.Y + 8, Facing.South);
        x = f.X + 8;
        foreach (var kind in Others)
        {
            game.BuildStall(kind, x, f.Y + 32, Facing.South);
            x += Catalogue.Of(kind).Width + 5;
        }

        game.BuildStall(StallKind.Bin, f.X + 40, f.Y + 24, Facing.South);
        var until = game.Now.Date.AddHours(scene.Hour);
        while (game.Now < until)
        {
            game.Clock.Step();
        }

        game.Clock.SetSpeed(GameSpeed.Normal);
        festival.Crowd.Snap();
        festival.SetHudShown(true);
        const float t = 0.5f;
        var left = f.X * t;
        var top = f.Y * t;
        switch (scene.View)
        {
            case 1:
                festival.Camera.LookAt(left + 20, top + 6, 20f);
                break;
            case 2:
                festival.Camera.LookAt(left + 14, top + 18, 17f);
                break;
            case 3:
                festival.Camera.LookAt(left + 32, top + 18, 17f);
                break;
            case 4:
                festival.Camera.LookAt(left + 18, top + 10, 16f);
                break;
            case 5:
                festival.Camera.LookAt(left + 14, top + 22, 16f);
                break;
            case 6:
                festival.Camera.LookAt(left + 12, top + 6, 20f);
                game.Site.SetPrice(1, 0, 8);
                game.Site.SetPrice(1, 2, 4);
                festival.StallMenu.Open(1);
                break;
            case 7:
                festival.Camera.LookAt(left + 28, top + 21, 15f);
                break;
            default:
                festival.Camera.LookAt(left + 34, top + 22, 15f);
                break;
        }

        festival.Camera.Snap();
        var crowd = game.Crowd;
        var sold = string.Join(", ", System.Linq.Enumerable.Range(0, game.Site.Stalls.Count).Where(i => Catalogue.Sells(game.Site.Stalls[i].Kind)).Select(i => $"{game.Site.Stalls[i].Kind} {game.SalesOf(i).Items}/£{game.SalesOf(i).Takings}"));
        GD.Print($"[shops] {scene.Name}: £{game.Money}, happy {crowd.Happiness}, on site {crowd.OnSite}, carrying {festival.Crowd.Carrying}, priced out {game.PricedOut}; {sold}");
    }
}
