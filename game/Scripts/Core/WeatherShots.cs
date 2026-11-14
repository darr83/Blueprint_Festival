using System;
using System.Linq;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Pictures of weather and mud from a real game, run with: godot --path game -- --weather-shots=DIR
/// (needs a display). Nothing is posed: the weather is set, and the festival day is played until
/// the time the picture is of.
/// </summary>
public partial class WeatherShots : Node
{
    private static readonly string[] Scenes =
    {
        "1_planning_with_a_wet_forecast", "2_build_day_forecast_and_ground_cover", "3_rain_over_the_field", "4_mud_in_front_of_the_stage",
        "5_straw_and_trackway_hold_up", "6_laying_straw_on_the_day", "7_a_hot_day", "8_results_after_a_wet_day",
    };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;

    public WeatherShots(string directory)
    {
        _directory = directory;
    }

    public WeatherShots() : this("shots")
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
                f.Litter.Refresh();
                f.Crowd.Snap();
                f.Staff.Snap();
                f.Weather.Settle();
                f.Sky.Apply(f.Game, f.Camera.Yaw, settle: true);
                f.Camera.Snap();
                _ready = true;
                _frame = 0;
            }

            return;
        }

        if (_frame == 30)
        {
            var path = $"{_directory}/{Scenes[_scene]}.png";
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"[weather] {path}");
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
            GD.Print("[weather] Done.");
            GetTree().Quit(0);
            return;
        }

        var only = OS.GetEnvironment("SHOTS_ONLY");
        if (only.Length > 0 && !Scenes[_scene].Contains(only, StringComparison.Ordinal))
        {
            NextScene();
            return;
        }

        GameManager.Instance.StartNewFestival(null);
    }

    private static void RunTo(FestivalGame game, double hour)
    {
        var until = game.Day.AddHours(hour);
        while (game.Phase == Phase.Day && game.Now < until)
        {
            game.Clock.Step();
        }
    }

    private void Arrange(FestivalScene festival)
    {
        var game = festival.Game;
        var f = game.Field.Field;
        var gap = game.Field.Gap;
        var name = Scenes[_scene];
        var hot = name == "7_a_hot_day";
        game.SetWeather(DayWeather.Of(hot ? WeatherKind.Hot : WeatherKind.Rain, 3));
        festival.Camera.ShowWholeField();
        festival.Year.PickHeadliner(game.Headliners.ToList().FindIndex(h => h.Stars == game.Headliners.Min(o => o.Stars)));
        var ponchos = name is "1_planning_with_a_wet_forecast" or "5_straw_and_trackway_hold_up";
        if (ponchos)
        {
            festival.Year.TogglePonchos();
        }

        if (name == "1_planning_with_a_wet_forecast")
        {
            GD.Print($"[weather] {name}: '{game.EarlyForecast}', expected {game.Crowd.Expected}, ponchos £{game.PonchoCost}");
            return;
        }

        festival.Year.StartBuilding();
        game.BuildStage(StageKind.Bandstand, f.X + 70, f.Y + 10, Facing.South);
        game.BuildFence(FenceKind.Paling, new TilePoint(gap.X, gap.Y + 1), new TilePoint(gap.Right - 1, gap.Y + 1));
        game.BuildGate(GateKind.VillageGate, gap.X + 2, gap.Y + 1, false);
        game.BuildStall(StallKind.FoodVan, f.X + 7, f.Y + 30, Facing.East);
        game.BuildStall(StallKind.Bar, f.X + 7, f.Y + 48, Facing.East);
        game.BuildStall(StallKind.Toilets, f.X + 26, f.Y + 5, Facing.South);
        game.BuildStall(StallKind.Toilets, f.X + 36, f.Y + 5, Facing.South);
        game.BuildStall(StallKind.WaterPoint, f.X + 46, f.Y + 5, Facing.South);
        game.BuildStall(StallKind.FirstAidTent, f.X + 54, f.Y + 4, Facing.South);
        game.HireStaff(StaffKind.TicketStaff, new TilePoint(gap.X + 4, gap.Y + 1));
        game.HireStaff(StaffKind.FirstAider, new TilePoint(f.X + 60, f.Y + 16));
        game.HireStaff(StaffKind.FirstAider, new TilePoint(f.X + 96, f.Y + 30));
        var stage = game.Show.Stage!;
        var rect = stage.Rect;
        var (sx, sy, _, _) = RunningOrder.Front(stage);
        var covered = name is "2_build_day_forecast_and_ground_cover" or "5_straw_and_trackway_hold_up";
        if (covered)
        {
            // Trackway across the front of the stage, straw behind it where the audience stands.
            festival.Build.PickGround(GroundKind.Trackway);
            festival.Build.DragGround(new TilePoint(rect.X - 4, rect.Bottom + 1), new TilePoint(rect.Right + 3, rect.Bottom + 7));
            festival.Build.PickGround(GroundKind.Straw);
            festival.Build.DragGround(new TilePoint(rect.X - 4, rect.Bottom + 9), new TilePoint(rect.Right + 3, rect.Bottom + 22));
            festival.Build.Stop();
        }

        var bank = game.Money;
        if (name == "2_build_day_forecast_and_ground_cover")
        {
            festival.BuildBar.Toggle(BuildMode.Ground);
            festival.BuildBar.Choose($"ground{GroundKind.Straw}");
            festival.Build.ShowGhost(new TilePoint(rect.X - 12, rect.Bottom + 14));
            festival.Camera.LookAt((float)sx, (float)sy + 8, 26f);
            GD.Print($"[weather] {name}: '{game.Weather.Summary}', bank £{bank}, covered {game.GroundCovered}");
            return;
        }

        game.OpenGates();
        switch (name)
        {
            case "3_rain_over_the_field":
                RunTo(game, 15.5);
                break;

            case "4_mud_in_front_of_the_stage":
            case "5_straw_and_trackway_hold_up":
                RunTo(game, 19.7);
                festival.Camera.LookAt((float)sx, (float)sy + 7, 17f);
                break;

            case "6_laying_straw_on_the_day":
                RunTo(game, 17.5);
                festival.BuildBar.Toggle(BuildMode.Ground);
                festival.BuildBar.Choose($"ground{GroundKind.Straw}");
                festival.Build.DragGround(new TilePoint(rect.X - 2, rect.Bottom + 2), new TilePoint(rect.Right + 1, rect.Bottom + 13));
                festival.Build.ShowGhost(new TilePoint(rect.Right + 8, rect.Bottom + 8));
                festival.Camera.LookAt((float)sx, (float)sy + 8, 20f);
                break;

            case "7_a_hot_day":
                RunTo(game, 15.0);
                festival.Camera.LookAt((float)sx - 8, (float)sy + 10, 26f);
                break;

            case "8_results_after_a_wet_day":
                RunTo(game, 30);
                festival.Year.PayScouts();
                festival.Year.HandBack();
                break;
        }

        game.Clock.SetSpeed(GameSpeed.Paused);
        GD.Print($"[weather] {name}: {game.Now:HH:mm}, bank after building £{bank}, on site {game.Crowd.OnSite}, rain {game.RainNow:0.00}, wet {game.Wetness:0.00}, muddy {game.MuddyPatches}, worn {game.GrassWorn}%, repair £{game.FieldRepair}, happy {game.Crowd.Happiness}%, went home {game.Crowd.WentHomeUnhappy}, top bar '{festival.TopBar.WeatherText}'");
    }
}
