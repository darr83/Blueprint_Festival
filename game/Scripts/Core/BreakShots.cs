using System;
using System.Linq;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.UI;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Pictures of a set, the half hour's break after it and skipping to the next act, from a real game, run with:
/// godot --path game -- --break-shots=DIR (needs a display). Nothing is posed: the festival day is
/// played until the time the picture is of.
/// </summary>
public partial class BreakShots : Node
{
    private static readonly string[] Scenes =
    {
        "1_an_act_on_stage_and_the_skip_button", "2_the_set_is_over_and_the_stage_is_empty", "3_off_to_the_bar_and_the_toilets_in_the_break",
        "4_skipped_on_to_the_next_act",
    };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;

    public BreakShots(string directory)
    {
        _directory = directory;
    }

    public BreakShots() : this("shots")
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
            again.Crowd.Snap();
        }

        if (_frame == 30)
        {
            var path = $"{_directory}/{Scenes[_scene]}.png";
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"[break] {path}");
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
            GD.Print("[break] Done.");
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
        game.BuildStall(StallKind.FoodVan, f.X + 7, f.Y + 30, Facing.East);
        game.BuildStall(StallKind.Bar, f.X + 7, f.Y + 48, Facing.East);
        game.BuildStall(StallKind.Toilets, f.X + 26, f.Y + 5, Facing.South);
        game.BuildStall(StallKind.Toilets, f.X + 36, f.Y + 5, Facing.South);
        game.BuildStall(StallKind.WaterPoint, f.X + 46, f.Y + 5, Facing.South);
        game.HireStaff(StaffKind.TicketStaff, new TilePoint(gap.X + 4, gap.Y + 1));
        game.OpenGates();
        switch (name)
        {
            case "1_an_act_on_stage_and_the_skip_button":
                WeatherShots.RunTo(game, 15.4);
                break;

            case "2_the_set_is_over_and_the_stage_is_empty":
                WeatherShots.RunTo(game, 15.62);
                break;

            case "3_off_to_the_bar_and_the_toilets_in_the_break":
                WeatherShots.RunTo(game, 15.6);
                festival.Camera.LookAt((f.X + 30) * 0.5f, (f.Y + 28) * 0.5f, 30f);
                break;

            case "4_skipped_on_to_the_next_act":
                // Half way through the first set, press the button and let the game hurry on.
                WeatherShots.RunTo(game, 14.75);
                festival.TopBar.SkipButton.EmitSignal(BaseButton.SignalName.Pressed);
                while (game.SkippingTo is not null)
                {
                    game.Advance(1 / 60.0);
                }

                WeatherShots.RunTo(game, 16.12);
                break;
        }

        game.Clock.SetSpeed(GameSpeed.Paused);
        GD.Print($"[break] {name}: {game.Now:HH:mm}, on site {game.Crowd.OnSite}, watching {game.Crowd.Watching}, at stalls {game.Crowd.AtStalls}, playing {game.Show.Playing(game.Now)?.Act.Name ?? "nobody"}, bar '{festival.TopBar.ShowText}', happy {game.Crowd.Happiness}%");
    }
}
