using System;
using System.Linq;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Pictures of a laid-out camping field on build day, run with:
/// godot --path game -- --camp-layout-shots=DIR (needs a display). A festival of 5,000 on the
/// estate takes the showground for camping, marks out family, quiet and accessible camping and
/// fire lanes, and puts down showers, the info point, the camp shop, toilets and taps.
/// </summary>
public partial class CampLayoutShots : Node
{
    private static readonly string[] Scenes = { "1_the_camping_tab", "2_the_camp_stalls_close_up", "3_fire_lanes_with_another_tool" };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;

    public CampLayoutShots(string directory)
    {
        _directory = directory;
    }

    public CampLayoutShots() : this("shots")
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
                f.Camera.Snap();
                f.Sky.Apply(f.Game, f.Camera.Yaw, settle: true);
                _ready = true;
                _frame = 0;
            }

            return;
        }

        if (_frame == 30)
        {
            var path = $"{_directory}/{Scenes[_scene]}.png";
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"[camplayout] {path}");
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
            GD.Print("[camplayout] Done.");
            GetTree().Quit(0);
            return;
        }

        var fields = new[] { new FieldChoice("home", FieldUse.Music), new FieldChoice("ten", FieldUse.Food), new FieldChoice("show", FieldUse.Camping), new FieldChoice("glebe", FieldUse.CarPark) };
        var game = FestivalGame.StartYear(null, new YearSetup(6, 5000, 300000, false, 82, fields));
        game.HireBuses(FestivalGame.MostBuses);
        game.ConfirmPlan();
        GameManager.Instance.Open(game);
    }

    /// <summary>Somewhere a stall fits, near a spot in the camp.</summary>
    private static void Put(FestivalGame game, StallKind kind, int x, int y)
    {
        for (var r = 0; r < 40; r += 2)
        {
            for (var dy = -r; dy <= r; dy += 2)
            {
                for (var dx = -r; dx <= r; dx += 2)
                {
                    if (game.BuildStall(kind, x + dx, y + dy, Facing.South).Ok)
                    {
                        return;
                    }
                }
            }
        }
    }

    private void Arrange(FestivalScene festival)
    {
        var game = festival.Game;
        var camp = game.Field.CampFields.First();
        for (var x = camp.X + 80; x < camp.Right - 40; x += 80)
        {
            game.MarkCamp(new TilePoint(x, camp.Y), new TilePoint(x + 5, camp.Bottom - 1), CampZone.FireLane);
        }

        var mid = camp.Y + (camp.Height / 2);
        game.MarkCamp(new TilePoint(camp.X, mid), new TilePoint(camp.Right - 1, mid + 5), CampZone.FireLane);
        game.MarkCamp(new TilePoint(camp.X, camp.Y), new TilePoint(camp.X + 79, mid - 1), CampZone.Family);
        game.MarkCamp(new TilePoint(camp.Right - 70, camp.Y), new TilePoint(camp.Right - 1, mid - 1), CampZone.Quiet);
        game.MarkCamp(new TilePoint(camp.X, mid + 6), new TilePoint(camp.X + 50, camp.Bottom - 1), CampZone.Accessible);
        Put(game, StallKind.ShowerBlock, camp.X + 90, mid - 14);
        Put(game, StallKind.CampInfo, camp.X + 110, mid + 10);
        Put(game, StallKind.CampShop, camp.X + 130, mid - 14);
        Put(game, StallKind.ToiletBlock, camp.X + 60, mid + 10);
        Put(game, StallKind.WaterPoint, camp.X + 70, mid - 10);
        var (cx, cz) = ((camp.X + (camp.Width / 2f)) * 0.5f, (camp.Y + (camp.Height / 2f)) * 0.5f);
        switch (Scenes[_scene])
        {
            case "1_the_camping_tab":
                festival.Camera.LookAt(cx, cz, 150f);
                festival.BuildBar.Toggle(BuildMode.Camp);
                festival.BuildBar.Choose(UI.BuildBar.CampKey(CampZone.Family));
                break;
            case "2_the_camp_stalls_close_up":
                festival.Camera.LookAt((camp.X + 110) * 0.5f, mid * 0.5f, 26f);
                break;
            default:
                festival.Camera.LookAt(cx, cz, 110f);
                break;
        }

        GD.Print($"[camplayout] {Scenes[_scene]}: checks {string.Join(" | ", game.CampChecks().Select(c => $"{c.State}: {c.Text}"))}");
    }
}
