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
/// Pictures of the lights from a real game, run with: godot --path game -- --light-shots=DIR
/// (needs a display). Nothing is posed: the lights are put up, and the festival day is played until
/// the time the picture is of.
/// </summary>
public partial class LightShots : Node
{
    private static readonly string[] Scenes =
    {
        "1_choosing_lights_on_build_day", "2_the_field_lit_at_dusk", "3_the_field_lit_at_night", "4_fairy_lights_and_path_lights_close_up", "5_the_same_field_with_no_lights",
    };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;

    public LightShots(string directory)
    {
        _directory = directory;
    }

    public LightShots() : this("shots")
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
            GD.Print($"[lights] {path}");
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
            GD.Print("[lights] Done.");
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

    internal static void RunTo(FestivalGame game, double hour)
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
        festival.Camera.ShowWholeField();
        festival.Year.PickHeadliner(game.Headliners.ToList().FindIndex(h => h.Stars == game.Headliners.Min(o => o.Stars)));
        festival.Year.StartBuilding();
        game.BuildStage(StageKind.Bandstand, f.X + 70, f.Y + 10, Facing.South);
        game.BuildFence(FenceKind.Paling, new TilePoint(gap.X, gap.Y + 1), new TilePoint(gap.Right - 1, gap.Y + 1));
        game.BuildGate(GateKind.VillageGate, gap.X + 2, gap.Y + 1, false);
        game.BuildStall(StallKind.Burgers, f.X + 7, f.Y + 30, Facing.East);
        game.BuildStall(StallKind.Bar, f.X + 7, f.Y + 48, Facing.East);
        game.BuildStall(StallKind.Toilets, f.X + 26, f.Y + 5, Facing.South);
        game.BuildStall(StallKind.WaterPoint, f.X + 46, f.Y + 5, Facing.South);
        game.HireStaff(StaffKind.TicketStaff, new TilePoint(gap.X + 4, gap.Y + 1));
        if (name != "5_the_same_field_with_no_lights")
        {
            // A floodlight in each far corner, fairy lights by the stalls, and a line of path lights across the middle.
            game.BuildLight(LightKind.Floodlight, new TilePoint(f.X + 100, f.Y + 20));
            game.BuildLight(LightKind.Floodlight, new TilePoint(f.X + 40, f.Y + 62));
            var corner = new TilePoint(f.X + 24, f.Y + 40);
            game.BuildFairyLights(new TilePoint(f.X + 24, f.Y + 24), corner);
            game.BuildFairyLights(corner, new TilePoint(f.X + 24, f.Y + 60));
            game.BuildFairyLights(corner, new TilePoint(f.X + 48, f.Y + 30));
            for (var step = 0; step < 5; step++)
            {
                game.BuildLight(LightKind.PathLight, new TilePoint(f.X + 34 + (step * 8), f.Y + 50 + (step * 4)));
            }
        }

        var bank = game.Money;
        if (name == "1_choosing_lights_on_build_day")
        {
            festival.BuildBar.Toggle(BuildMode.Light);
            festival.BuildBar.Choose(UI.BuildBar.LightKey(LightKind.FairyLights));
            festival.Build.HoldDrag(new TilePoint(f.X + 60, f.Y + 50), new TilePoint(f.X + 90, f.Y + 62));
            festival.Camera.LookAt((f.X + 60) * 0.5f, (f.Y + 45) * 0.5f, 30f);
            GD.Print($"[lights] {name}: bank £{bank}, '{festival.BuildBar.StatusText}'");
            return;
        }

        game.OpenGates();
        RunTo(game, name == "2_the_field_lit_at_dusk" ? 21.0 : 22.2);
        if (name == "4_fairy_lights_and_path_lights_close_up")
        {
            festival.Camera.LookAt((f.X + 30) * 0.5f, (f.Y + 44) * 0.5f, 14f);
        }

        festival.Lights.Rebuild();
        game.Clock.SetSpeed(GameSpeed.Paused);
        GD.Print($"[lights] {name}: {game.Now:HH:mm}, bank after building £{bank}, on site {game.Crowd.OnSite}, in the dark {game.InTheDark}, happy {game.Crowd.Happiness}%, lamps {festival.Lights.LampCount}, strength {festival.Lights.Strength:0.00}");
    }
}
