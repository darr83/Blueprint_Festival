using System;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Pictures of the show, run with: godot --path game -- --show-shots=DIR (needs a display).
/// Each one starts a real festival, puts up a stage, runs the day on to a set time and takes a
/// picture, so what is shown is what the game does.
/// </summary>
public partial class ShowShots : Node
{
    private static readonly (string Name, int Seed, StageKind Stage, double Hour, float Size, bool Hud)[] Scenes =
    {
        ("1_duo_on_the_trailer", 9, StageKind.FarmTrailer, 12.4, 22, true),
        ("2_folk_band_on_the_bandstand", 9, StageKind.Bandstand, 14.9, 0, false),
        ("3_bandstand_closer", 9, StageKind.Bandstand, 14.9, 22, false),
        ("4_between_acts", 9, StageKind.Bandstand, 15.6, 30, true),
        ("5_band_on_the_hired_stage", 6, StageKind.HiredStage, 17.9, 26, true),
        ("6_headline_set_at_dusk", 9, StageKind.HiredStage, 21.1, 0, false),
        ("7_headline_set_at_night", 9, StageKind.HiredStage, 21.8, 24, true),
        ("8_dj_headline_set", 12, StageKind.HiredStage, 21.8, 24, false),
    };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;

    public ShowShots(string directory)
    {
        _directory = directory;
    }

    public ShowShots() : this("shots")
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
                _ready = true;
                _frame = 0;
            }

            return;
        }

        // A second or so of the game running slowly, so arms and lights settle, then the picture.
        if (_frame == 70)
        {
            var path = $"{_directory}/{Scenes[_scene].Name}.png";
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"[show] {path}");
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
            GD.Print("[show] Done.");
            GetTree().Quit(0);
            return;
        }

        GameManager.Instance.StartNewGame(null, null, Scenes[_scene].Seed);
    }

    private void Arrange(FestivalScene festival)
    {
        var scene = Scenes[_scene];
        var game = festival.Game;
        var f = game.Field.Field;
        var info = Catalogue.Of(scene.Stage);
        var x = f.X + ((f.Width - info.Width) / 2) + 8;
        game.BuildStage(scene.Stage, x, f.Y + 12, Facing.South);
        var until = game.Now.Date.AddHours(scene.Hour);
        while (game.Now < until)
        {
            game.Clock.Step();
        }

        game.Clock.SetSpeed(GameSpeed.Normal);
        festival.Crowd.Snap();
        festival.SetHudShown(scene.Hud);
        var at = SiteView.Middle(new TileRect(x, f.Y + 12, info.Width, info.Depth));
        if (scene.Size <= 0)
        {
            festival.Camera.ShowWholeField();
        }
        else
        {
            festival.Camera.LookAt(at.X + 1.5f, at.Z + 5.5f, scene.Size);
        }

        festival.Camera.Snap();
        GD.Print($"[show] {scene.Name}: {festival.TopBar.ShowText}, {game.Crowd.Watching} watching of {game.Crowd.OnSite}");
    }
}
