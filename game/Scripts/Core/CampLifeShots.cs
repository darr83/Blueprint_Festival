using System;
using System.Linq;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Pictures of camp life, run with: godot --path game -- --camp-life-shots=DIR (needs a display).
/// A festival of 5,000 on the estate with a laid-out camping field runs its day, then the pictures
/// show the camp at night (fires, campers round them, stewards and the panel) and the morning after.
/// </summary>
public partial class CampLifeShots : Node
{
    private static readonly string[] Scenes = { "1_the_camp_at_night", "2_round_a_fire_pit", "3_the_morning_after" };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;

    public CampLifeShots(string directory)
    {
        _directory = directory;
    }

    public CampLifeShots() : this("shots")
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
            GD.Print($"[camplife] {path}");
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
            GD.Print("[camplife] Done.");
            GetTree().Quit(0);
            return;
        }

        var fields = new[] { new FieldChoice("home", FieldUse.Music), new FieldChoice("ten", FieldUse.Food), new FieldChoice("show", FieldUse.Camping), new FieldChoice("glebe", FieldUse.CarPark) };
        var game = FestivalGame.StartYear(null, new YearSetup(6, 5000, 300000, false, 82, fields));
        game.HireBuses(FestivalGame.MostBuses);
        game.ConfirmPlan();
        Lay(game);
        game.OpenGates();
        var until = Scenes[_scene] == "3_the_morning_after" ? game.Day + TimeSpan.FromHours(32.6) : game.Day + TimeSpan.FromHours(24.6);
        for (var i = 0; i < 200000 && game.Phase == Phase.Day && game.Now < until; i++)
        {
            game.Clock.Step();
        }

        GD.Print($"[camplife] {Scenes[_scene]}: {game.Now:HH:mm} {game.CampTime}, up {game.CampersUp}, fires {game.CampFires.Count}, log: {string.Join(" | ", game.CampLog.Select(l => $"{l.At:HH:mm} {l.Text} ({l.Outcome})"))}");
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

    /// <summary>A way in through the gap, with turnstiles and ticket staff.</summary>
    private static void OpenAWayIn(FestivalGame game)
    {
        var gap = game.Field.Gap;
        var vertical = gap.Height > gap.Width;
        var row = Catalogue.Of(GateKind.TurnstileRow);
        var (from, to) = vertical ? (new TilePoint(gap.X + 1, gap.Y), new TilePoint(gap.X + 1, gap.Bottom - 1)) : (new TilePoint(gap.X, gap.Y + 1), new TilePoint(gap.Right - 1, gap.Y + 1));
        game.BuildFence(FenceKind.SteelMesh, from, to);
        var length = vertical ? gap.Height : gap.Width;
        for (var along = 0; along + row.Width <= length; along += row.Width)
        {
            var (x, y) = vertical ? (from.X, gap.Y + along) : (gap.X + along, from.Y);
            game.BuildGate(GateKind.TurnstileRow, x, y, vertical);
            for (var lane = 0; lane < row.Lanes; lane++)
            {
                var step = (lane * row.Width / row.Lanes) + 1;
                game.HireStaff(StaffKind.TicketStaff, vertical ? new TilePoint(x, y + step) : new TilePoint(x + step, y));
            }
        }
    }

    private static void Lay(FestivalGame game)
    {
        OpenAWayIn(game);
        var camp = game.Field.CampFields.First();
        for (var x = camp.X + 80; x < camp.Right - 40; x += 80)
        {
            game.MarkCamp(new TilePoint(x, camp.Y), new TilePoint(x + 5, camp.Bottom - 1), CampZone.FireLane);
        }

        var mid = camp.Y + (camp.Height / 2);
        game.MarkCamp(new TilePoint(camp.X, mid), new TilePoint(camp.Right - 1, mid + 5), CampZone.FireLane);
        Put(game, StallKind.ShowerBlock, camp.X + 90, mid - 14);
        Put(game, StallKind.CampShop, camp.X + 130, mid - 14);
        Put(game, StallKind.ToiletBlock, camp.X + 60, mid + 10);
        Put(game, StallKind.WaterPoint, camp.X + 70, mid - 10);
        Put(game, StallKind.FirePit, camp.X + 110, mid + 14);
        Put(game, StallKind.FirePit, camp.X + 170, mid - 20);
        Put(game, StallKind.FirePit, camp.X + 230, mid + 14);
        game.HireStaff(StaffKind.CampSteward, new TilePoint(camp.X + 120, mid + 8));
        game.HireStaff(StaffKind.CampSteward, new TilePoint(camp.X + 220, mid + 8));
        game.HireStaff(StaffKind.Security, new TilePoint(camp.X + 160, mid + 8));
    }

    private void Arrange(FestivalScene festival)
    {
        var game = festival.Game;
        var camp = game.Field.CampFields.First();
        var mid = camp.Y + (camp.Height / 2);
        var (cx, cz) = ((camp.X + (camp.Width / 2f)) * 0.5f, (camp.Y + (camp.Height / 2f)) * 0.5f);
        switch (Scenes[_scene])
        {
            case "2_round_a_fire_pit":
                var pit = game.CampFires.FirstOrDefault(f => f.InPit);
                festival.Camera.LookAt(pit is null ? (camp.X + 110) * 0.5f : (float)pit.X, pit is null ? mid * 0.5f : (float)pit.Y, 18f);
                break;
            default:
                festival.Camera.LookAt(cx, cz, 110f);
                break;
        }
    }
}
