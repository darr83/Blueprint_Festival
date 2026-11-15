using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Pictures of the crowd areas, run with: godot --path game -- --area-shots=DIR (needs a display).
/// Each starts a real festival, draws a front pen and a VIP area through the game's rules, runs
/// the day on to a set time and takes a picture.
/// </summary>
public partial class AreaShots : Node
{
    private static readonly (string Name, double Hour, int View)[] Scenes =
    {
        ("1_drawing_a_front_pen", 8.2, 1),
        ("2_setting_the_limit", 8.2, 2),
        ("3_the_pen_full_on_the_day", 15.4, 3),
        ("4_crowd_view_with_areas", 15.4, 4),
    };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;

    public AreaShots(string directory)
    {
        _directory = directory;
    }

    public AreaShots() : this("shots")
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
            GD.Print($"[areas] {path}");
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
            GD.Print("[areas] Done.");
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
        var (penFrom, penTo) = (new TilePoint(stage.X - 2, stage.Bottom + 2), new TilePoint(stage.Right + 1, stage.Bottom + 15));
        const float t = 0.5f;
        var middle = new Vector3((stage.X + (stage.Width / 2f)) * t, 0, (stage.Bottom + 9) * t);

        if (scene.View == 1)
        {
            festival.Build.PickArea(AreaKind.FrontPen);
            festival.Build.HoldDrag(penFrom, penTo);
        }
        else
        {
            game.BuildArea(AreaKind.FrontPen, penFrom, penTo);
            game.BuildArea(AreaKind.Vip, new TilePoint(stage.Right + 4, stage.Bottom), new TilePoint(stage.Right + 17, stage.Bottom + 13));
            game.SetAreaLimit(0, scene.View >= 3 ? 16 : 40);
            foreach (var g in game.Site.Areas[0].Gaps)
            {
                game.HireStaff(StaffKind.Security, new TilePoint(g.X, g.Y + 3));
            }

            game.HireStaff(StaffKind.Security, new TilePoint(game.Site.Areas[1].Gaps[0].X, game.Site.Areas[1].Gaps[0].Y + 3));
            game.Crowd.VipTickets = game.Crowd.Expected / 10;
            if (scene.View == 2)
            {
                festival.Build.PickArea(AreaKind.FrontPen);
                festival.Areas.Open(0);
            }
        }

        var until = game.Now.Date.AddHours(scene.Hour);
        while (game.Now < until)
        {
            game.Clock.Step();
        }

        game.Clock.SetSpeed(GameSpeed.Normal);
        festival.Crowd.Snap();
        if (scene.View == 4)
        {
            festival.SetCrowdView(true);
        }

        festival.Camera.LookAt(middle.X + 2, middle.Z, 17f);
        festival.Camera.Snap();
        var crowd = game.Crowd;
        GD.Print($"[areas] {scene.Name}: on site {crowd.OnSite}, pen {(game.Site.Areas.Count > 0 ? crowd.InArea(0) : 0)}, waiting {(game.Site.Areas.Count > 0 ? crowd.WaitingAt(0) : 0)}, kept {(game.Site.Areas.Count > 0 && crowd.Kept(0))}, status '{festival.Build.Status}', full '{festival.Areas.FullText}'");
    }
}
