using System;
using System.Linq;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Pictures of the event day panel, "The day so far", from a real game, run with:
/// godot --path game -- --day-shots=DIR (needs a display). The day is played to late afternoon.
/// </summary>
public partial class DayShots : Node
{
    private static readonly string[] Scenes =
    {
        "1_overview", "2_acts", "3_people", "4_incidents", "5_money",
    };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;

    public DayShots(string directory)
    {
        _directory = directory;
    }

    public DayShots() : this("shots")
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
                f.Sky.Apply(f.Game, f.StageCam.Yaw, settle: true);
                _ready = true;
                _frame = 0;
            }

            return;
        }

        if (_frame == 2 && GetTree().CurrentScene is FestivalScene again)
        {
            again.Crowd.Snap();
        }

        if (_frame == 30)
        {
            var path = $"{_directory}/{Scenes[_scene]}.png";
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"[day] {path}");
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
            GD.Print("[day] Done.");
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

    private void Arrange(FestivalScene festival)
    {
        var game = festival.Game;
        var name = Scenes[_scene];
        festival.Camera.ShowWholeField();
        var f = game.Field.Field;
        var gap = game.Field.Gap;
        festival.Year.PickHeadliner(game.Headliners.ToList().FindIndex(h => h.Stars == game.Headliners.Min(o => o.Stars)));
        festival.Year.StartBuilding();
        game.BuildStage(StageKind.Bandstand, f.X + 70, f.Y + 10, Facing.South);
        game.BuildFence(FenceKind.Paling, new TilePoint(gap.X, gap.Y + 1), new TilePoint(gap.Right - 1, gap.Y + 1));
        game.BuildGate(GateKind.VillageGate, gap.X + 2, gap.Y + 1, false);
        game.BuildStall(StallKind.Burgers, f.X + 7, f.Y + 30, Facing.East);
        game.BuildStall(StallKind.Bar, f.X + 7, f.Y + 48, Facing.East);
        game.BuildStall(StallKind.Toilets, f.X + 26, f.Y + 5, Facing.South);
        game.BuildStall(StallKind.WaterPoint, f.X + 46, f.Y + 5, Facing.South);
        game.BuildStall(StallKind.FirstAidTent, f.X + 30, f.Y + 30, Facing.South);
        game.HireStaff(StaffKind.TicketStaff, new TilePoint(gap.X + 4, gap.Y + 1));
        game.HireStaff(StaffKind.FirstAider, new TilePoint(f.X + 34, f.Y + 40));
        game.OpenGates();
        WeatherShots.RunTo(game, 17.2);
        festival.ToggleDayBoard();
        festival.DayBoard.Show(name switch
        {
            "2_acts" => UI.DayPanel.Tab.Acts,
            "3_people" => UI.DayPanel.Tab.People,
            "4_incidents" => UI.DayPanel.Tab.Incidents,
            "5_money" => UI.DayPanel.Tab.Money,
            _ => UI.DayPanel.Tab.Overview,
        });
        game.Clock.SetSpeed(GameSpeed.Paused);
        GD.Print($"[day] {name}: {game.Now:HH:mm}, log {game.DayLog.Count}, mood points {game.MoodLine.Count}, through {game.Crowd.ThroughGates}, unhappy {game.Crowd.WentHomeUnhappy}, staff {game.Staff.Count}, money {game.Money}");
    }
}
