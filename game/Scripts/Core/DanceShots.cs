using System;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Films the crowd dancing, one sort of music after another, run with:
/// godot --path game --write-movie FILE.avi --fixed-fps 30 -- --dance-shots=DIR (needs a display).
/// Each scene starts a real festival, books an act, puts up a stage and runs the day to the act's
/// set; the crowd then dances as it does in the game. Songs in the act's music folder are played.
/// </summary>
public partial class DanceShots : Node
{
    private static readonly (string Name, string Artist, double SkipTo)[] Scenes =
    {
        ("1_pop", "The Velvet Otters", 40),
        ("2_rock", "Little Ferrets", 40),
        ("3_metal", "Bramble Hammer", 0),
        ("4_electronic", "DJ Foxglove", 0),
        ("5_country", "Tobias Pell", 40),
        ("6_hip_hop", "DJ Magpie", 40),
    };

    /// <summary>How many frames each scene is filmed for.</summary>
    private const int Filmed = 180;

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;

    public DanceShots(string directory)
    {
        _directory = directory;
    }

    public DanceShots() : this("shots")
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

        if (_frame == 5 && GetTree().CurrentScene is FestivalScene festival)
        {
            festival.Music.SkipTo(Scenes[_scene].SkipTo);
            GD.Print($"[dance] {Scenes[_scene].Name} starts at frame {Engine.GetFramesDrawn() + 1}, beat known: {festival.Music.Beat is not null}");
        }

        if (_frame == 5 + (Filmed / 2))
        {
            var path = $"{_directory}/{Scenes[_scene].Name}.png";
            GetViewport().GetTexture().GetImage().SavePng(path);
        }

        if (_frame == 5 + Filmed)
        {
            if (GetTree().CurrentScene is FestivalScene filmed)
            {
                GD.Print($"[dance] {Scenes[_scene].Name}: {filmed.Crowd.Clappers} clapping along, {filmed.Crowd.ClapsPlayed} claps played, {filmed.Crowd.Singers} singing at level {filmed.Crowd.SingingLevel:0.00}");
            }

            NextScene();
        }
    }

    private void NextScene()
    {
        _scene++;
        // SHOTS_ONLY=<part of a name> films just the scenes whose names have it.
        var only = OS.GetEnvironment("SHOTS_ONLY");
        while (only.Length > 0 && _scene < Scenes.Length && !Scenes[_scene].Name.Contains(only, StringComparison.Ordinal))
        {
            _scene++;
        }

        _ready = false;
        _frame = 0;
        if (_scene >= Scenes.Length)
        {
            GD.Print("[dance] Done.");
            GetTree().Quit(0);
            return;
        }

        GameManager.Instance.StartNewGame(null, null, 9);
    }

    private void Arrange(FestivalScene festival)
    {
        var scene = Scenes[_scene];
        var game = festival.Game;
        var f = game.Field.Field;
        var info = Catalogue.Of(StageKind.HiredStage);
        var x = f.X + ((f.Width - info.Width) / 2) + 8;
        game.BuildStage(StageKind.HiredStage, x, f.Y + 12, Facing.South);
        game.Show.Book(1, ActGenerator.Of(Roster.Named(scene.Artist)!));

        // Food, drink and toilets, so the crowd has what it needs and stays to watch.
        game.BuildStall(StallKind.Burgers, f.X + 7, f.Y + 30, Facing.East);
        game.BuildStall(StallKind.Bar, f.X + 7, f.Y + 48, Facing.East);
        game.BuildStall(StallKind.Toilets, f.X + 26, f.Y + 5, Facing.South);
        game.BuildStall(StallKind.Toilets, f.X + 36, f.Y + 5, Facing.South);
        game.BuildStall(StallKind.WaterPoint, f.X + 46, f.Y + 5, Facing.South);
        var until = game.Now.Date.AddHours(16.6);
        while (game.Now < until)
        {
            game.Clock.Step();
        }

        game.Clock.SetSpeed(GameSpeed.Normal);
        festival.Crowd.Snap();
        festival.SetHudShown(true);
        var at = SiteView.Middle(new TileRect(x, f.Y + 12, info.Width, info.Depth));
        festival.Camera.LookAt(at.X + 1f, at.Z + 9f, 13);
        festival.Camera.Snap();
        GD.Print($"[dance] {scene.Name}: {festival.TopBar.ShowText}, {game.Crowd.Watching} watching");
    }
}
