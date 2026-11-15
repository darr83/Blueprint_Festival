using System;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Pictures of the light through one festival day, run with: godot --path game -- --lighting-shots=DIR
/// (needs a display). One real game with a hired stage, stalls and lights is played from the
/// morning to the headline set, stopping for a picture at each time. Set SHADOWS=off for the same
/// pictures with shadows switched off.
/// </summary>
public partial class LightingShots : Node
{
    private static readonly (string Name, double Hour, int View)[] Scenes =
    {
        ("1_morning_0900", 9.0, 0),
        ("2_midday_1300", 13.0, 0),
        ("3_afternoon_close_up_1600", 16.0, 1),
        ("4_golden_hour_2030", 20.5, 0),
        ("5_dusk_2115", 21.25, 0),
        ("6_dusk_stage_close_up", 21.3, 3),
        ("7_dusk_stalls_close_up", 21.35, 2),
        ("8_night_2200", 22.0, 0),
        ("9_night_fairy_lights_close_up", 22.1, 2),
    };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _built;
    private Vector3 _stage;

    public LightingShots(string directory)
    {
        _directory = directory;
    }

    public LightingShots() : this("shots")
    {
    }

    public override void _Ready()
    {
        DirAccess.MakeDirRecursiveAbsolute(_directory);
        Callable.From(() => GameManager.Instance.StartNewGame(null, null, 9)).CallDeferred();
    }

    public override void _Process(double delta)
    {
        if (GetTree().CurrentScene is not FestivalScene f || !f.IsNodeReady() || f.Game != GameManager.Instance.Game)
        {
            return;
        }

        if (!_built)
        {
            Build(f);
            _built = true;
            Next(f);
            return;
        }

        _frame++;
        if (_frame == 40)
        {
            var path = $"{_directory}/{Scenes[_scene].Name}.png";
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"[lighting] {path} at {f.Game.Now:HH:mm}, {f.Game.Crowd.OnSite} on site, {f.Game.Crowd.Watching} watching");
            Next(f);
        }
    }

    private void Next(FestivalScene f)
    {
        _scene++;
        var only = OS.GetEnvironment("SHOTS_ONLY");
        while (only.Length > 0 && _scene < Scenes.Length && !Scenes[_scene].Name.Contains(only, StringComparison.Ordinal))
        {
            _scene++;
        }

        if (_scene >= Scenes.Length)
        {
            GD.Print("[lighting] Done.");
            GetTree().Quit(0);
            return;
        }

        var game = f.Game;
        var (_, hour, view) = Scenes[_scene];
        var until = game.Now.Date.AddHours(hour);
        while (game.Now < until && game.Clock.Speed != GameSpeed.Paused)
        {
            game.Clock.Step();
        }

        game.Clock.SetSpeed(GameSpeed.Normal);
        var field = game.Field.Field;
        switch (view)
        {
            case 1:
                f.Camera.LookAt((field.X + 22) * 0.5f, (field.Y + 40) * 0.5f, 26f);
                break;
            case 2:
                f.Camera.LookAt((field.X + 28) * 0.5f, (field.Y + 44) * 0.5f, 22f);
                break;
            case 3:
                f.Camera.LookAt(_stage.X, _stage.Z + 5f, 30f);
                break;
            default:
                f.Camera.ShowWholeField();
                break;
        }

        f.Crowd.Snap();
        f.Staff.Snap();
        f.Weather.Settle();
        f.Lights.Rebuild();
        f.Sky.Apply(game, f.Camera.Yaw, settle: true);
        f.Camera.Snap();
        f.SetHudShown(false);
        _frame = 0;
    }

    private void Build(FestivalScene festival)
    {
        var game = festival.Game;
        // The pictures are of the light, not the bank balance: enough money for every lamp.
        typeof(FestivalGame).GetProperty(nameof(FestivalGame.Money))!.SetValue(game, 20000);
        var f = game.Field.Field;
        GD.Print($"[lighting] field {f.X},{f.Y} {f.Width}x{f.Height}");
        var info = Catalogue.Of(StageKind.HiredStage);
        var x = f.X + ((f.Width - info.Width) / 2) + 8;
        game.BuildStage(StageKind.HiredStage, x, f.Y + 12, Facing.South);
        _stage = SiteView.Middle(new TileRect(x, f.Y + 12, info.Width, info.Depth));
        game.BuildStall(StallKind.FoodVan, f.X + 7, f.Y + 30, Facing.East);
        game.BuildStall(StallKind.Bar, f.X + 7, f.Y + 48, Facing.East);
        game.BuildStall(StallKind.Toilets, f.X + 26, f.Y + 5, Facing.South);
        game.BuildStall(StallKind.Toilets, f.X + 36, f.Y + 5, Facing.South);
        game.BuildStall(StallKind.WaterPoint, f.X + 46, f.Y + 5, Facing.South);
        game.BuildLight(LightKind.Floodlight, new TilePoint(f.X + f.Width - 30, f.Y + f.Height - 14));
        game.BuildLight(LightKind.Floodlight, new TilePoint(f.X + 44, f.Y + f.Height - 10));
        var corner = new TilePoint(f.X + 24, f.Y + 40);
        game.BuildFairyLights(new TilePoint(f.X + 24, f.Y + 24), corner);
        game.BuildFairyLights(corner, new TilePoint(f.X + 24, f.Y + 60));
        game.BuildFairyLights(corner, new TilePoint(f.X + 48, f.Y + 30));
        for (var step = 0; step < 6; step++)
        {
            game.BuildLight(LightKind.PathLight, new TilePoint(f.X + 34 + (step * 8), f.Y + 50 + (step * 3)));
        }

        game.OpenGates();
    }
}
