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
/// Pictures of camping at 5,000 from a real game, run with:
/// godot --path game -- --camp-shots=DIR (needs a display). Each one starts a festival of 5,000 with
/// money in the bank, takes the camping field, builds a site, and plays the day until the time the picture is of.
/// </summary>
public partial class CampShots : Node
{
    private static readonly (string Name, int Licence)[] Scenes =
    {
        ("1_camping_on_the_planning_screen", 5000),
        ("2_tents_going_up_in_the_morning", 5000),
        ("3_the_showground_and_the_camping_field", 5000),
        ("4_walking_to_the_tents_after_the_headliner", 5000),
        ("5_the_camping_field_the_morning_after", 5000),
        ("6_the_gate_stays_shut_without_camping", 5000),
    };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;

    public CampShots(string directory)
    {
        _directory = directory;
    }

    public CampShots() : this("shots")
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
            // The camera is where it will be now, so the people nearest the middle can be drawn in full.
            again.Crowd.Snap();
        }

        if (_frame == 30)
        {
            var path = $"{_directory}/{Scenes[_scene].Name}.png";
            GetViewport().GetTexture().GetImage().SavePng(path);
            var crowd = (GetTree().CurrentScene as FestivalScene)?.Crowd;
            GD.Print($"[camp] {path} in full {crowd?.InFull}, simple {crowd?.Simple}, {Engine.GetFramesPerSecond():0} fps");
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
            GD.Print("[camp] Done.");
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
        var year = FestivalGame.Licences.ToList().IndexOf(licence) + 1;
        GameManager.Instance.Open(FestivalGame.StartYear(null, new YearSetup(year, licence, 4000 + (licence * 14), false, 82)));
    }

    private void Arrange(FestivalScene festival)
    {
        var game = festival.Game;
        var name = Scenes[_scene].Name;
        var camp = game.Field.Camp;
        var gap = game.Field.CampGap;
        var (campX, campY) = ((camp.X + (camp.Width / 2f)) * 0.5f, (camp.Y + (camp.Height / 2f)) * 0.5f);
        var (gapX, gapY) = (gap.Right * 0.5f, (gap.Y + (gap.Height / 2f)) * 0.5f);
        festival.Camera.ShowWholeField();
        festival.Year.PickHeadliner(0);
        if (name != "6_the_gate_stays_shut_without_camping")
        {
            festival.Year.ToggleCamping();
        }

        if (name == "1_camping_on_the_planning_screen")
        {
            GD.Print($"[camp] {name}: campers expected {game.CampersExpected}, cost £{FestivalGame.CampingCost}");
            return;
        }

        festival.Year.StartBuilding();
        StageShots.Build(game);
        var bank = game.Money;
        game.OpenGates();
        switch (name)
        {
            case "2_tents_going_up_in_the_morning":
                StageShots.RunTo(game, 10.5);
                festival.Camera.LookAt(gapX + 14, gapY, 30f);
                break;

            case "3_the_showground_and_the_camping_field":
                StageShots.RunTo(game, 15);
                festival.Camera.LookAt(gapX - 20, gapY + 10, 120f);
                break;

            case "4_walking_to_the_tents_after_the_headliner":
                StageShots.RunTo(game, 22.45);
                festival.Camera.LookAt(gapX + 6, gapY, 26f);
                break;

            case "5_the_camping_field_the_morning_after":
                StageShots.RunTo(game, 30);
                festival.Camera.LookAt(campX - 10, campY, 34f);
                break;

            case "6_the_gate_stays_shut_without_camping":
                StageShots.RunTo(game, 15);
                festival.Camera.LookAt(gapX, gapY, 16f);
                break;
        }

        festival.Camp.Refresh();
        game.Clock.SetSpeed(GameSpeed.Paused);
        GD.Print($"[camp] {name}: {game.Now:HH:mm} {game.Phase}, bank after building £{bank}, on site {game.Crowd.OnSite} of {game.Crowd.Expected}, campers in {game.Crowd.CampersIn}, in tents {game.Crowd.InTents}, tents drawn {festival.Camp.TentCount}, litter {game.Litter.Count}, happy {game.Crowd.Happiness}");
    }
}
