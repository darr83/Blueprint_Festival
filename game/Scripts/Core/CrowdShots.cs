using System;
using System.Linq;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Pictures of crowd safety from a real game, run with: godot --path game -- --crowd-shots=DIR
/// (needs a display). Nothing is posed: each picture plays a festival day until the thing it
/// shows has happened.
/// </summary>
public partial class CrowdShots : Node
{
    private static readonly string[] Scenes =
    {
        "1_crowd_view_on_a_good_day", "2_a_big_name_packs_the_front", "3_people_hurt_in_the_crush", "4_the_act_paused",
        "5_front_barrier_and_security", "6_the_day_called_off", "7_results_after_a_crush",
    };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;

    public CrowdShots(string directory)
    {
        _directory = directory;
    }

    public CrowdShots() : this("shots")
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
                f.Density.Redraw();
                f.CrowdSafety.Refresh();
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
            GD.Print($"[crowd] {path}");
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
            GD.Print("[crowd] Done.");
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

    /// <summary>Runs the day on until something is true, or until a time of day at the latest.</summary>
    private static bool RunUntil(FestivalGame game, Func<bool> when, double latestHour = 23.4)
    {
        var latest = game.Day.AddHours(latestHour);
        while (game.Phase == Phase.Day && game.Now < latest)
        {
            if (when())
            {
                return true;
            }

            game.Clock.Step();
        }

        return false;
    }

    private static void RunFor(FestivalGame game, double minutes)
    {
        var until = game.Now.AddMinutes(minutes);
        RunUntil(game, () => game.Now >= until);
    }

    private void Arrange(FestivalScene festival)
    {
        var game = festival.Game;
        var f = game.Field.Field;
        var gap = game.Field.Gap;
        var name = Scenes[_scene];
        var bigName = name != "1_crowd_view_on_a_good_day";
        var stars = bigName ? game.Headliners.Max(h => h.Stars) : game.Headliners.Min(h => h.Stars);
        festival.Camera.ShowWholeField();
        festival.Year.PickHeadliner(game.Headliners.ToList().FindIndex(h => h.Stars == stars));
        festival.Year.StartBuilding();
        game.BuildStage(bigName ? StageKind.FarmTrailer : StageKind.Bandstand, f.X + 70, f.Y + 10, Facing.South);
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
        if (name == "5_front_barrier_and_security")
        {
            game.BuildFence(FenceKind.CrowdBarrier, new TilePoint(rect.X - 2, rect.Bottom + 4), new TilePoint(rect.Right + 1, rect.Bottom + 4));
            game.HireStaff(StaffKind.Security, new TilePoint(rect.X + 4, rect.Bottom + 2));
            game.HireStaff(StaffKind.Security, new TilePoint(rect.Right - 5, rect.Bottom + 2));
        }

        var bank = game.Money;
        var (sx, sy, _, _) = RunningOrder.Front(stage);
        var warned = false;
        game.CrushWarning += (_, _) => warned = true;
        game.OpenGates();
        switch (name)
        {
            case "1_crowd_view_on_a_good_day":
                RunUntil(game, () => false, 14.6);
                festival.SetCrowdView(true);
                festival.Camera.LookAt((float)sx, (float)sy + 9, 24f);
                break;

            case "2_a_big_name_packs_the_front":
                RunUntil(game, () => warned);
                RunFor(game, 2);
                festival.Camera.LookAt((float)sx, (float)sy + 6, 16f);
                break;

            case "3_people_hurt_in_the_crush":
                RunUntil(game, () => game.CrushInjuries >= 2 && game.Staff.Any(s => s.Kind == StaffKind.FirstAider && s.State == StaffState.Busy));
                festival.Camera.LookAt((float)sx, (float)sy + 4, 12f);
                break;

            case "4_the_act_paused":
                RunUntil(game, () => warned);
                festival.CrowdSafety.Pause();
                RunFor(game, 3);
                festival.Camera.LookAt((float)sx, (float)sy + 8, 18f);
                break;

            case "5_front_barrier_and_security":
                RunUntil(game, () => game.Show.Playing(game.Now) is { Headliner: true });
                RunFor(game, 30);
                festival.SetCrowdView(true);
                festival.Camera.LookAt((float)sx, (float)sy + 5, 14f);
                break;

            case "6_the_day_called_off":
                RunUntil(game, () => game.SeriousIncident);
                RunFor(game, 4);
                festival.Camera.LookAt((float)sx - 6, (float)sy + 12, 30f);
                break;

            case "7_results_after_a_crush":
                RunUntil(game, () => false, 30);
                festival.Year.PayScouts();
                festival.Year.HandBack();
                break;
        }

        game.Clock.SetSpeed(GameSpeed.Paused);
        GD.Print($"[crowd] {name}: {game.Now:HH:mm}, bank after building £{bank}, on site {game.Crowd.OnSite}, worst {game.Density.Worst:0.0}, red {game.Density.RedSquares}, hurt {game.CrushInjuries}, lifted out {game.PulledOut}, paused {game.ActsPaused}, called off {game.SeriousIncident}, card '{festival.CrowdSafety.WarningText}'");
    }
}
