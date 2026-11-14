using System;
using System.Linq;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Pictures of people queuing, run with: godot --path game -- --queue-shots=DIR (needs a display).
/// Each one plans and builds a real festival and runs the day on until a queue has formed, so
/// what is shown is what the game does. Set SHOTS_ONLY to part of a name to make just that one.
/// </summary>
public partial class QueueShots : Node
{
    private static readonly (string Name, int Licence)[] Scenes =
    {
        ("1_queuing_at_the_gate", 200), ("2_showing_a_ticket", 200), ("3_the_queue_at_the_bar", 200), ("4_the_queue_at_the_food_van", 200),
        ("5_a_long_queue_folds_back", 200), ("6_two_lanes_at_the_arch_gate", 1500),
    };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;

    public QueueShots(string directory)
    {
        _directory = directory;
    }

    public QueueShots() : this("shots")
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
                f.Camera.Snap();
                _ready = true;
                _frame = 0;
            }

            return;
        }

        if (_frame == 30)
        {
            var path = $"{_directory}/{Scenes[_scene].Name}.png";
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"[queues] {path}");
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
            GD.Print("[queues] Done.");
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
        if (licence == 200)
        {
            GameManager.Instance.StartNewFestival(null);
        }
        else
        {
            var year = FestivalGame.Licences.ToList().IndexOf(licence) + 1;
            GameManager.Instance.Open(FestivalGame.StartYear(null, new YearSetup(year, licence, 4000 + (licence * 14), false, 82)));
        }
    }

    /// <summary>Runs the day on until something is true, or until a time of day at the latest.</summary>
    private static bool RunUntil(FestivalGame game, Func<bool> when, double latestHour)
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

    private void Arrange(FestivalScene festival)
    {
        var game = festival.Game;
        var f = game.Field.Field;
        var gap = game.Field.Gap;
        const float t = 0.5f;
        var name = Scenes[_scene].Name;
        festival.Camera.ShowWholeField();
        festival.Year.PickHeadliner(0);
        festival.Year.StartBuilding();
        if (name == "6_two_lanes_at_the_arch_gate")
        {
            var y = gap.Y + 1;
            game.BuildFence(FenceKind.SteelMesh, new TilePoint(gap.X, y), new TilePoint(gap.Right - 1, y));
            var row = Catalogue.Of(GateKind.ArchGate);
            game.BuildGate(GateKind.ArchGate, gap.X + 4, y, false);
            for (var lane = 0; lane < row.Lanes; lane++)
            {
                game.HireStaff(StaffKind.TicketStaff, new TilePoint(gap.X + 4 + (lane * row.Width / row.Lanes) + 1, y));
            }

            game.BuildStage(StageKind.MainStage, f.X + (f.Width / 2) - 20, f.Y + 6, Facing.South);
            game.OpenGates();
            RunUntil(game, () => Enumerable.Range(0, row.Lanes).All(lane => game.Crowd.GateLine(0, lane)!.Standing >= 7), 10.5);
            var (mx, my) = game.Site.LaneMouth(0, 1, 5);
            festival.Camera.LookAt((float)mx + 1.5f, (float)my + 3f, 20f);
            Report(game, name);
            return;
        }

        game.BuildStage(StageKind.Bandstand, f.X + 74, f.Y + 10, Facing.South);
        game.BuildFence(FenceKind.Paling, new TilePoint(gap.X, gap.Y + 1), new TilePoint(gap.Right - 1, gap.Y + 1));
        game.BuildGate(GateKind.VillageGate, gap.X + 2, gap.Y + 1, false);
        game.HireStaff(StaffKind.TicketStaff, new TilePoint(gap.X + 4, gap.Y + 1));
        game.BuildStall(StallKind.Toilets, f.X + 30, f.Y + 5, Facing.South);
        game.BuildStall(StallKind.Toilets, f.X + 40, f.Y + 5, Facing.South);
        switch (name)
        {
            case "1_queuing_at_the_gate":
            {
                game.OpenGates();
                RunUntil(game, () => game.Crowd.GateLine(0, 0)!.Standing >= 8, 11);
                festival.Camera.Turn(2);
                var (mx, my) = game.Site.LaneMouth(0, 0, 4);
                festival.Camera.LookAt((float)mx, (float)my, 13f);
                break;
            }

            case "2_showing_a_ticket":
            {
                game.OpenGates();
                RunUntil(game, () => game.Crowd.GateLine(0, 0)!.Standing >= 8 && game.Crowd.People.Any(p => p.Ticket)
                    && game.Crowd.Parties.Any(p => p.State == PartyState.Queuing && p.Checked == 1 && p.ServiceLeft is > 8 and < 20), 11);
                var (mx, my) = game.Site.LaneMouth(0, 0, 1);
                festival.Camera.Turn(2);
                festival.Camera.LookAt((float)mx, (float)my, 6.5f);
                break;
            }

            case "3_the_queue_at_the_bar":
            {
                game.BuildStall(StallKind.Bar, f.X + 7, f.Y + 30, Facing.East);
                game.BuildStall(StallKind.FoodVan, f.X + 7, f.Y + 50, Facing.East);
                game.OpenGates();
                RunUntil(game, () => game.Crowd.StallLine(2)!.Standing >= 7 && game.Crowd.BeingServed(2) == 3, 20);
                var bar = game.Site.Stalls[2].Rect;
                festival.Camera.LookAt((bar.Right + 9) * t, (bar.Y + 4) * t, 12f);
                break;
            }

            case "4_the_queue_at_the_food_van":
            {
                game.BuildStall(StallKind.Bar, f.X + 7, f.Y + 30, Facing.East);
                game.BuildStall(StallKind.FoodVan, f.X + 7, f.Y + 50, Facing.East);
                game.OpenGates();
                RunUntil(game, () => game.Crowd.StallLine(3)!.Standing >= 7 && game.Crowd.BeingServed(3) == 2, 20);
                var van = game.Site.Stalls[3].Rect;
                festival.Camera.LookAt((van.Right + 9) * t, (van.Y + 3) * t, 12f);
                break;
            }

            default:
            {
                game.BuildStall(StallKind.TeaAndCake, f.X + 50, f.Y + 30, Facing.South);
                game.BuildStall(StallKind.WaterPoint, f.X + 70, f.Y + 5, Facing.South);
                game.OpenGates();
                RunUntil(game, () => game.Crowd.StallLine(2)!.Standing >= 22, 20);
                var table = game.Site.Stalls[2].Rect;
                festival.Camera.LookAt((table.X + 5) * t, (table.Bottom + 12) * t, 14f);
                break;
            }
        }

        Report(game, name);
    }

    private static void Report(FestivalGame game, string name)
    {
        game.Clock.SetSpeed(GameSpeed.Paused);
        var stalls = string.Join(", ", game.Site.Stalls.Select((s, i) => $"{s.Kind} {game.Crowd.StallQueueLength(i)}+{game.Crowd.BeingServed(i)}"));
        GD.Print($"[queues] {name}: {game.Now:HH:mm}, at the gate {game.Crowd.Queuing}, on site {game.Crowd.OnSite}, stalls: {stalls}, happy {game.Crowd.Happiness}");
    }
}
