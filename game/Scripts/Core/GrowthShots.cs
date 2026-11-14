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
/// Pictures of bigger festivals from a real game, run with: godot --path game -- --growth-shots=DIR
/// (needs a display). Each one starts a later year with money in the bank, builds a site to
/// match its licence, and plays the day until the time the picture is of.
/// </summary>
public partial class GrowthShots : Node
{
    private static readonly (string Name, int Licence)[] Scenes =
    {
        ("1_planning_a_bigger_year", 800),
        ("2_home_farm_meadow_with_1500", 1500),
        ("3_the_new_kit_close_up", 1500),
        ("4_turnstiles_at_the_showground", 5000),
        ("5_the_showground_with_5000", 5000),
        ("6_main_stage_headliner", 5000),
        ("7_close_up_in_a_big_crowd", 5000),
        ("8_results_moving_to_a_bigger_field", 1500),
    };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;

    public GrowthShots(string directory)
    {
        _directory = directory;
    }

    public GrowthShots() : this("shots")
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
            // The camera is where it will be now, so the people nearest the middle can be drawn in full.
            again.Crowd.Snap();
        }

        if (_frame == 30)
        {
            var path = $"{_directory}/{Scenes[_scene].Name}.png";
            GetViewport().GetTexture().GetImage().SavePng(path);
            var crowd = (GetTree().CurrentScene as FestivalScene)?.Crowd;
            GD.Print($"[growth] {path} in full {crowd?.InFull}, simple {crowd?.Simple}, {Engine.GetFramesPerSecond():0} fps");
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
            GD.Print("[growth] Done.");
            GetTree().Quit(0);
            return;
        }

        var only = OS.GetEnvironment("SHOTS_ONLY");
        if (only.Length > 0 && !Scenes[_scene].Name.Contains(only, StringComparison.Ordinal))
        {
            NextScene();
            return;
        }

        var licence = Scenes[_scene].Licence;
        var year = FestivalGame.Licences.ToList().IndexOf(licence) + 1;
        GameManager.Instance.Open(FestivalGame.StartYear(null, new YearSetup(year, licence, 4000 + (licence * 14), false, 82)));
    }

    private static void RunTo(FestivalGame game, double hour)
    {
        var until = game.Day.AddHours(hour);
        while (game.Phase == Phase.Day && game.Now < until)
        {
            game.Clock.Step();
        }
    }

    /// <summary>Builds a site to match the licence: turnstiles, a main stage with a barrier, and stalls down both sides.</summary>
    private static void Build(FestivalGame game)
    {
        var licence = game.Licence;
        var gap = game.Field.Gap;
        var y = gap.Y + 1;
        var f = game.Field.Field;
        game.BuildFence(FenceKind.SteelMesh, new TilePoint(gap.X, y), new TilePoint(gap.Right - 1, y));
        var row = Catalogue.Of(GateKind.TurnstileRow);
        for (var x = gap.X; x + row.Width <= gap.Right; x += row.Width)
        {
            game.BuildGate(GateKind.TurnstileRow, x, y, false);
            for (var lane = 0; lane < row.Lanes; lane++)
            {
                game.HireStaff(StaffKind.TicketStaff, new TilePoint(x + (lane * row.Width / row.Lanes) + 1, y));
            }
        }

        game.BuildStage(StageKind.MainStage, f.X + (f.Width / 2) - 20, f.Y + 6, Facing.South);
        var stage = game.Site.Stages[0].Rect;
        game.BuildFence(FenceKind.CrowdBarrier, new TilePoint(stage.X - 2, stage.Bottom + 4), new TilePoint(stage.Right + 1, stage.Bottom + 4));

        // Each of the pit crew holds another eight metres of crowd, so a bigger crowd wants more of them.
        var crew = Math.Clamp(licence / 1200, 2, 4);
        for (var i = 0; i < crew; i++)
        {
            game.HireStaff(StaffKind.Security, new TilePoint(stage.X + 4 + (i * 32 / (crew - 1)), stage.Bottom + 2));
        }

        // And more out in the field, so trouble never has to wait for the pit crew.
        for (var i = 0; i < licence / 600; i++)
        {
            game.HireStaff(StaffKind.Security, new TilePoint(f.X + 50 + (i * 61 % (f.Width - 100)), f.Y + 70 + (i * 47 % (f.Height - 110))));
        }

        var kinds = new[] { StallKind.FoodVan, StallKind.Bar, StallKind.ToiletBlock, StallKind.WaterPoint };
        var wanted = Math.Max(8, licence / 110);
        var perSide = (f.Height - 50) / 30;
        for (var i = 0; i < wanted; i++)
        {
            var east = i % 2 == 0;
            var column = i / 2 / perSide;
            var along = i / 2 % perSide;
            var x = east ? f.Right - 16 - (column * 40) : f.X + 4 + (column * 40);
            game.BuildStall(kinds[i % kinds.Length], x, f.Y + 44 + (along * 30), east ? Facing.West : Facing.East);
        }

        game.BuildStall(StallKind.FirstAidTent, f.X + 20, f.Y + 6, Facing.South);
        game.BuildStall(StallKind.FirstAidTent, f.X + 40, f.Y + 6, Facing.South);
        for (var i = 0; i < Math.Max(2, licence / 300); i++)
        {
            game.HireStaff(StaffKind.FirstAider, new TilePoint(f.X + 30 + (i * 37 % (f.Width - 60)), f.Y + 40 + (i * 53 % (f.Height - 80))));
        }
    }

    private void Arrange(FestivalScene festival)
    {
        var game = festival.Game;
        var f = game.Field.Field;
        var gap = game.Field.Gap;
        var name = Scenes[_scene].Name;
        festival.Camera.ShowWholeField();
        if (name == "1_planning_a_bigger_year")
        {
            GD.Print($"[growth] {name}: {game.Ground.Name}, rent £{game.FieldRent}, expected {game.Crowd.Expected}");
            return;
        }

        // The biggest name on offer for the headliner pictures, the middle one otherwise.
        var stars = name is "6_main_stage_headliner" or "7_close_up_in_a_big_crowd" ? game.Headliners.Max(h => h.Stars) : game.Headliners.Min(h => h.Stars);
        festival.Year.PickHeadliner(game.Headliners.ToList().FindIndex(h => h.Stars == stars));
        festival.Year.StartBuilding();
        Build(game);
        var bank = game.Money;
        var stage = game.Show.Stage!;
        var (sx, sy, _, _) = RunningOrder.Front(stage);
        game.OpenGates();
        switch (name)
        {
            case "2_home_farm_meadow_with_1500":
                RunTo(game, 15.5);
                break;

            case "3_the_new_kit_close_up":
                RunTo(game, 13.2);
                festival.Camera.LookAt((f.Right - 30) * 0.5f, (f.Y + 80) * 0.5f, 30f);
                break;

            case "4_turnstiles_at_the_showground":
                RunTo(game, 9.4);
                festival.Camera.LookAt((gap.X + (gap.Width / 2)) * 0.5f, (gap.Y + 14) * 0.5f, 26f);
                break;

            case "5_the_showground_with_5000":
                RunTo(game, 15.5);
                break;

            case "6_main_stage_headliner":
                RunTo(game, 21.4);
                festival.Camera.LookAt((float)sx, (float)sy + 16, 62f);
                break;

            case "7_close_up_in_a_big_crowd":
                RunTo(game, 21.4);
                festival.Camera.LookAt((float)sx, (float)sy + 9, 22f);
                festival.SetCrowdView(true);
                break;

            case "8_results_moving_to_a_bigger_field":
                RunTo(game, 30);
                festival.Year.PayScouts();
                festival.Year.HandBack();
                break;
        }

        game.Clock.SetSpeed(GameSpeed.Paused);
        GD.Print($"[growth] {name}: {game.Now:HH:mm}, {game.Ground.Name}, bank after building £{bank}, on site {game.Crowd.OnSite} of {game.Crowd.Expected}, queuing {game.Crowd.Queuing}, watching {game.Crowd.Watching}, worst {game.Density.Worst:0.0}, hurt {game.CrushInjuries}, happy {game.Crowd.Happiness}%, treated {game.Treated}, untreated {game.Untreated}, in £{game.TotalIncome}, out £{game.TotalSpending}, litter {game.Litter.Count}");
    }
}
