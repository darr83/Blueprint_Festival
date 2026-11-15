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
/// Pictures of the two stage cameras from a real game, run with: godot --path game -- --cam-shots=DIR
/// (needs a display). The festival day is played until the time the picture is of.
/// </summary>
public partial class CamShots : Node
{
    private static readonly string[] Scenes =
    {
        "1_the_two_stage_cam_buttons", "2_front_cam_on_the_bandstand", "3_back_cam_on_the_bandstand",
        "4_front_cam_on_the_hired_stage_at_night", "5_back_cam_on_the_hired_stage_at_night",
    };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;

    public CamShots(string directory)
    {
        _directory = directory;
    }

    public CamShots() : this("shots")
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
            GD.Print($"[cam] {path}");
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
            GD.Print("[cam] Done.");
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
        var night = name.Contains("night", StringComparison.Ordinal);
        festival.Camera.ShowWholeField();
        var f = game.Field.Field;
        var gap = game.Field.Gap;
        festival.Year.PickHeadliner(game.Headliners.ToList().FindIndex(h => h.Stars == game.Headliners.Min(o => o.Stars)));
        festival.Year.StartBuilding();
        if (night)
        {
            game.BuildStage(StageKind.HiredStage, f.X + 64, f.Y + 8, Facing.South);
        }
        else
        {
            game.BuildStage(StageKind.Bandstand, f.X + 70, f.Y + 10, Facing.South);
        }

        game.BuildFence(FenceKind.Paling, new TilePoint(gap.X, gap.Y + 1), new TilePoint(gap.Right - 1, gap.Y + 1));
        game.BuildGate(GateKind.VillageGate, gap.X + 2, gap.Y + 1, false);
        game.BuildStall(StallKind.Burgers, f.X + 7, f.Y + 30, Facing.East);
        game.BuildStall(StallKind.Bar, f.X + 7, f.Y + 48, Facing.East);
        game.BuildStall(StallKind.Toilets, f.X + 26, f.Y + 5, Facing.South);
        game.BuildStall(StallKind.Toilets, f.X + 36, f.Y + 5, Facing.South);
        game.BuildStall(StallKind.WaterPoint, f.X + 46, f.Y + 5, Facing.South);
        game.HireStaff(StaffKind.TicketStaff, new TilePoint(gap.X + 4, gap.Y + 1));
        game.OpenGates();
        WeatherShots.RunTo(game, night ? 21.3 : 14.6);
        if (name.Contains("front", StringComparison.Ordinal))
        {
            festival.ViewButtons.FrontCamButton!.ButtonPressed = true;
        }
        else if (name.Contains("back", StringComparison.Ordinal))
        {
            festival.ViewButtons.BackCamButton!.ButtonPressed = true;
        }

        game.Clock.SetSpeed(GameSpeed.Paused);
        GD.Print($"[cam] {name}: {game.Now:HH:mm}, watching {game.Crowd.Watching}, cam {festival.StageCam.Showing?.ToString() ?? "off"}, playing {game.Show.Playing(game.Now)?.Act.Name ?? "nobody"}");
    }
}
