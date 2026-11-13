using System;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Pictures of the festival year, run with: godot --path game -- --year-shots=DIR (needs a display).
/// It plays a real game: plans year one, builds, runs the day, tidies up and reads the results,
/// then does a poor year for comparison.
/// </summary>
public partial class YearShots : Node
{
    private static readonly (string Name, bool NewGame)[] Scenes =
    {
        ("1_plan_the_year", true), ("2_build_day", false), ("3_the_morning_after", false), ("4_picking_litter", false), ("5_results_good_year", false),
        ("7_plan_year_two", true), ("6_results_poor_year", true),
    };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;

    public YearShots(string directory)
    {
        _directory = directory;
    }

    public YearShots() : this("shots")
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
                f.Camera.Snap();
                _ready = true;
                _frame = 0;
            }

            return;
        }

        if (_frame == 20)
        {
            var path = $"{_directory}/{Scenes[_scene].Name}.png";
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"[year] {path}");
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
            GD.Print("[year] Done.");
            GetTree().Quit(0);
            return;
        }

        if (Scenes[_scene].Name == "7_plan_year_two")
        {
            GameManager.Instance.StartNextYear();
        }
        else if (Scenes[_scene].NewGame)
        {
            GameManager.Instance.StartNewFestival(null);
        }
    }

    private static void RunTheDay(FestivalGame game)
    {
        game.OpenGates();
        while (game.Phase == Phase.Day)
        {
            game.Clock.Step();
        }
    }

    private void Arrange(FestivalScene festival)
    {
        var game = festival.Game;
        var f = game.Field.Field;
        var gap = game.Field.Gap;
        festival.Camera.ShowWholeField();
        switch (Scenes[_scene].Name)
        {
            case "2_build_day":
                festival.Year.StartBuilding();
                game.BuildStage(StageKind.Bandstand, f.X + 74, f.Y + 10, Facing.South);
                game.BuildFence(FenceKind.Paling, new TilePoint(gap.X, gap.Y + 1), new TilePoint(gap.Right - 1, gap.Y + 1));
                game.BuildGate(GateKind.VillageGate, gap.X + 2, gap.Y + 1, false);
                game.BuildStall(StallKind.TeaAndCake, f.X + 7, f.Y + 21, Facing.East);
                game.BuildStall(StallKind.FoodVan, f.X + 7, f.Y + 34, Facing.East);
                game.BuildStall(StallKind.Bar, f.X + 7, f.Y + 52, Facing.East);
                game.BuildStall(StallKind.Toilets, f.X + 26, f.Y + 5, Facing.South);
                game.BuildStall(StallKind.Toilets, f.X + 36, f.Y + 5, Facing.South);
                game.BuildStall(StallKind.WaterPoint, f.X + 48, f.Y + 5, Facing.South);
                break;

            case "3_the_morning_after":
                RunTheDay(game);
                GD.Print($"[year] Good year: {game.Crowd.Arrived} came, {game.Crowd.Happiness}% happy, {game.LitterAtStart} litter, {game.GrassWorn}% worn, in £{game.TotalIncome}, out £{game.TotalSpending}.");
                break;

            case "4_picking_litter":
            {
                // Sweep the picker over the ground in front of the stage.
                for (var x = (f.X + 56) * 0.5; x < (f.X + 92) * 0.5; x += 1.5)
                {
                    for (var z = (f.Y + 20) * 0.5; z < (f.Y + 44) * 0.5; z += 1.5)
                    {
                        festival.Year.PickAt(x, z);
                    }
                }

                festival.Camera.LookAt(((f.X + 74) * 0.5f) + 1, ((f.Y + 30) * 0.5f) + 3, 22f);
                break;
            }

            case "5_results_good_year":
                festival.Year.PayScouts();
                festival.Year.HandBack();
                break;

            case "6_results_poor_year":
                // A dear ticket, a big stage, one tea table, no toilets, and the litter left where it fell.
                festival.Year.PickTicket(3);
                festival.Year.PickHeadliner(0);
                festival.Year.StartBuilding();
                game.BuildStage(StageKind.Bandstand, f.X + 74, f.Y + 10, Facing.South);
                game.BuildFence(FenceKind.Paling, new TilePoint(gap.X, gap.Y + 1), new TilePoint(gap.Right - 1, gap.Y + 1));
                game.BuildGate(GateKind.VillageGate, gap.X + 2, gap.Y + 1, false);
                game.BuildStall(StallKind.TeaAndCake, f.X + 7, f.Y + 21, Facing.East);
                RunTheDay(game);
                festival.Year.HandBack();
                GD.Print($"[year] Poor year: {game.Crowd.Arrived} came, {game.Crowd.Happiness}% happy, {game.Crowd.WentHomeUnhappy} went home, bank £{game.Money}, {game.Phase}.");
                break;
        }
    }
}
