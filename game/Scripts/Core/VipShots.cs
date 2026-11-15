using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Pictures of the VIP things, run with: godot --path game -- --vip-shots=DIR (needs a display).
/// Each starts a real festival, builds the VIP area through the game's rules, runs the day on to a
/// set time and takes a picture.
/// </summary>
public partial class VipShots : Node
{
    private static readonly (string Name, double Hour, int View)[] Scenes =
    {
        ("1_lanyards_in_the_queue", 8.6, 1),
        ("2_the_vip_area", 13.5, 2),
        ("3_marking_vip_ground", 13.5, 3),
        ("4_the_vip_lounge", 13.5, 4),
        ("5_the_viewing_platform", 14.6, 5),
        ("6_a_bar_kept_for_vips", 13.5, 6),
    };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;

    public VipShots(string directory)
    {
        _directory = directory;
    }

    public VipShots() : this("shots")
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
            GD.Print($"[vip] {path}");
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
            GD.Print("[vip] Done.");
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

        // The VIP area to the left of the crowd: the platform looking at the stage, the lounge behind it.
        game.VipGround(new TilePoint(f.X + 4, f.Y + 30), new TilePoint(f.X + 27, f.Y + 61), true);
        game.BuildStall(StallKind.VipPlatform, f.X + 10, f.Y + 32, Facing.North);
        game.BuildStall(StallKind.VipLounge, f.X + 8, f.Y + 48, Facing.North);
        game.BuildStall(StallKind.Toilets, f.X + 4, f.Y + 41, Facing.East);

        // Two bars side by side: one kept for VIPs, one for everyone.
        game.BuildStall(StallKind.Bar, f.X + 36, f.Y + 50, Facing.South);
        game.BuildStall(StallKind.Bar, f.X + 54, f.Y + 50, Facing.South);
        game.Site.SetVipOnly(game.Site.Stalls.Count - 2, true);
        game.BuildStall(StallKind.Toilets, f.X + 70, f.Y + 40, Facing.West);

        // One in ten are VIPs.
        game.Crowd.VipTickets = game.Crowd.Expected / 10;

        var until = game.Now.Date.AddHours(scene.Hour);
        while (game.Now < until)
        {
            game.Clock.Step();
        }

        game.Clock.SetSpeed(GameSpeed.Normal);
        festival.Crowd.Snap();
        festival.SetHudShown(scene.View != 1);
        const float t = 0.5f;
        var left = f.X * t;
        var top = f.Y * t;
        var platform = SiteView.Middle(System.Linq.Enumerable.First(game.Site.Stalls, s => s.Kind == StallKind.VipPlatform).Rect);
        switch (scene.View)
        {
            case 1:
                LineUp(festival, new Vector3(left + 22, 0, top + 21));
                festival.Camera.LookAt(left + 22, top + 21.3f, 3.6f);
                break;
            case 2:
            case 3:
                festival.Camera.LookAt(left + 12, top + 20, 22f);
                if (scene.View == 3)
                {
                    festival.Build.PickVipGround();
                }

                break;
            case 4:
                festival.Camera.Turn(2);
                festival.Camera.LookAt(left + 8, top + 27, 11f);
                break;
            case 5:
                festival.Camera.LookAt(platform.X, platform.Z, 8f);
                break;
            default:
                festival.Camera.LookAt(left + 26, top + 29, 12f);
                break;
        }

        festival.Camera.Snap();
        var crowd = game.Crowd;
        GD.Print($"[vip] {scene.Name}: VIPs {crowd.Vips} of {crowd.Expected}, on site {crowd.OnSite}, VIPs on site {crowd.VipsOnSite}");
    }

    /// <summary>Five VIP guests in a row facing the camera, for a close look at the lanyard.</summary>
    private static void LineUp(FestivalScene festival, Vector3 at)
    {
        var ages = new[] { AgeGroup.Adult, AgeGroup.Older, AgeGroup.Adult, AgeGroup.Teenager, AgeGroup.Adult };
        for (var i = 0; i < ages.Length; i++)
        {
            var look = LookGenerator.FestivalGoer(731 + (i * 3), ages[i]) with { Lanyard = Crowd.LanyardColour, Poncho = null, Hat = Hat.None };
            var person = PersonView.Build(look);
            person.Position = at + new Vector3(-2.2f + (i * 1.1f), 0, 0);
            festival.AddChild(person);
        }
    }
}
