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
/// Pictures of the fireworks from a real game, run with: godot --path game -- --firework-shots=DIR
/// (needs a display). A village festival with a launch pad and the example show at ten o'clock, the
/// day played up to the show.
/// </summary>
public partial class FireworkShots : Node
{
    private static readonly string[] Scenes =
    {
        "1_the_mixer_on_build_day", "2_the_show_at_night", "3_the_finale", "4_no_fence_round_the_safe_zone",
    };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;

    public FireworkShots(string directory)
    {
        _directory = directory;
    }

    public FireworkShots() : this("shots")
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
            GD.Print($"[fireworks] {path}");
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
            GD.Print("[fireworks] Done.");
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
        var f = game.Field.Field;
        var gap = game.Field.Gap;
        var name = Scenes[_scene];
        festival.Camera.ShowWholeField();
        festival.Year.PickHeadliner(game.Headliners.ToList().FindIndex(h => h.Stars == game.Headliners.Min(o => o.Stars)));
        festival.Year.StartBuilding();
        game.BuildStage(StageKind.Bandstand, f.X + 40, f.Y + 8, Facing.South);
        game.BuildFence(FenceKind.Paling, new TilePoint(gap.X, gap.Y + 1), new TilePoint(gap.Right - 1, gap.Y + 1));
        game.BuildGate(GateKind.VillageGate, gap.X + 2, gap.Y + 1, false);
        game.BuildStall(StallKind.Burgers, f.X + 7, f.Y + 30, Facing.East);
        game.BuildStall(StallKind.Bar, f.X + 7, f.Y + 48, Facing.East);
        game.BuildStall(StallKind.Toilets, f.X + 20, f.Y + 66, Facing.North);
        game.HireStaff(StaffKind.TicketStaff, new TilePoint(gap.X + 4, gap.Y + 1));
        game.BuildLight(LightKind.Floodlight, new TilePoint(f.X + 30, f.Y + 40));
        var (px, py) = (f.X + 92, f.Y + 40);
        var built = game.BuildLaunchPad(LaunchPadKind.Small, px, py);
        if (name != "4_no_fence_round_the_safe_zone")
        {
            game.FenceSafeZone(true);
        }

        var show = game.AddFireworkShow(22 * 60);
        game.UseExampleShow(show);
        var middle = SiteView.Middle(new TileRect(px, py, 6, 6));
        GD.Print($"[fireworks] {name}: pad {built.Ok} {built.Problem}, bank £{game.Money}");
        if (name == "1_the_mixer_on_build_day")
        {
            festival.Mixer.Open();
            festival.Mixer.Refresh(force: true);
            festival.Camera.LookAt(middle.X, middle.Z, 45f);
            return;
        }

        game.OpenGates();
        if (name == "4_no_fence_round_the_safe_zone")
        {
            festival.Mixer.Open();
            festival.Mixer.Refresh(force: true);
            LightShots.RunTo(game, 21.9);
            festival.Camera.LookAt(middle.X, middle.Z, 45f);
            game.Clock.SetSpeed(GameSpeed.Paused);
            return;
        }

        LightShots.RunTo(game, 22.0 + (1 / 3600.0));
        festival.Fireworks.Rebuild();
        festival.Fireworks.RunFor(name == "3_the_finale" ? 27.5 : 11.5);
        festival.LookAtPad();
        game.Clock.SetSpeed(GameSpeed.Paused);
        GD.Print($"[fireworks] {name}: {game.Now:HH:mm}, on site {game.Crowd.OnSite}, happy {game.Crowd.Happiness}%, sparks {festival.Fireworks.SparkCount}, results {string.Join("; ", game.FireworkResults.Select(r => $"{r.Outcome} {r.Stars} {r.Why}"))}");
    }
}
