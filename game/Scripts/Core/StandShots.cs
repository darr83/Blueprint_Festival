using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using System.Linq;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Pictures of the stands, run with: godot --path game -- --stand-shots=DIR (needs a display).
/// Each starts a real festival, puts up a seated stand, a terrace, a raised deck and an accessible
/// deck through the game's rules, runs the day on to a set time and takes a picture.
/// </summary>
public partial class StandShots : Node
{
    private static readonly (string Name, double Hour, int View)[] Scenes =
    {
        ("1_dragging_a_stand", 8.2, 1),
        ("2_the_stands_built", 8.2, 2),
        ("3_the_stands_on_the_day", 15.0, 3),
        ("4_close_up_on_the_day", 15.0, 4),
    };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;

    public StandShots(string directory)
    {
        _directory = directory;
    }

    public StandShots() : this("shots")
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

        if (_frame == 60)
        {
            var path = $"{_directory}/{Scenes[_scene].Name}.png";
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"[stands] {path}");
            NextScene();
        }
    }

    private void NextScene()
    {
        _scene++;
        while (_scene < Scenes.Length && System.Environment.GetEnvironmentVariable("SHOTS_ONLY") is { Length: > 0 } only && !Scenes[_scene].Name.Contains(only))
        {
            _scene++;
        }

        _ready = false;
        _frame = 0;
        if (_scene >= Scenes.Length)
        {
            GD.Print("[stands] Done.");
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
        var gap = game.Field.Gap;
        game.SetWeather(DayWeather.Of(WeatherKind.Fine, 3));
        game.BuildStage(StageKind.Bandstand, f.X + 34, f.Y + 14, Facing.South);
        game.BuildFence(FenceKind.Paling, new TilePoint(gap.X, gap.Y + 1), new TilePoint(gap.Right - 1, gap.Y + 1));
        game.BuildGate(GateKind.VillageGate, gap.X + 2, gap.Y + 1, false);
        var stage = game.Site.Stages[0].Rect;
        const float t = 0.5f;
        var middle = new Vector3((stage.X + (stage.Width / 2f)) * t, 0, (stage.Bottom + 14) * t);
        var (seatFrom, seatTo) = (new TilePoint(stage.X - 6, stage.Bottom + 26), new TilePoint(stage.Right + 2, stage.Bottom + 26));
        if (scene.View == 1)
        {
            festival.Build.PickStand(StandKind.Seated);
            festival.Build.SetStandRows(5);
            festival.Build.SetStandRoof(StandRoof.Full);
            festival.Build.HoldDrag(seatFrom, seatTo);
        }
        else
        {
            game.BuildStand(StandKind.Seated, seatFrom, seatTo, 5, StandRoof.Full);
            game.BuildStand(StandKind.Terrace, new TilePoint(stage.X - 16, stage.Bottom + 2), new TilePoint(stage.X - 16, stage.Bottom + 20), 4, StandRoof.BackHalf);
            game.BuildStand(StandKind.Deck, new TilePoint(stage.Right + 12, stage.Bottom + 2), new TilePoint(stage.Right + 12, stage.Bottom + 13), 4, StandRoof.None);
            game.BuildStand(StandKind.Accessible, new TilePoint(stage.Right + 6, stage.Bottom + 24), new TilePoint(stage.Right + 16, stage.Bottom + 24), 3, StandRoof.None);
            for (var i = 0; i < game.Site.Stands.Count; i++)
            {
                var step = game.Site.Stands[i].Steps[0];
                var (lx, ly) = game.Site.Stands[i].Looking;
                var outward = game.Site.Stands[i].IsEdge(step) && game.Site.Stands[i].LocalOf(step).V < 0 ? (lx, ly) : (-lx, -ly);
                game.HireStaff(StaffKind.Security, new TilePoint(step.X + (outward.Item1 * 2), step.Y + (outward.Item2 * 2)));
            }
        }

        var until = game.Now.Date.AddHours(scene.Hour);
        while (game.Now < until)
        {
            game.Clock.Step();
        }

        game.Clock.SetSpeed(GameSpeed.Normal);
        festival.Crowd.Snap();
        festival.Camera.LookAt(middle.X + (scene.View == 4 ? -2 : 1), middle.Z + (scene.View == 4 ? 4 : 0), scene.View == 4 ? 10f : 19f);
        festival.Camera.Snap();
        var crowd = game.Crowd;
        GD.Print($"[stands] {scene.Name}: on site {crowd.OnSite}, stands {game.Site.Stands.Count}, on them {string.Join(",", System.Linq.Enumerable.Range(0, game.Site.Stands.Count).Select(i => crowd.OnStand(i) + "/" + crowd.Places(i)))}, status '{festival.Build.Status}'");
    }
}
