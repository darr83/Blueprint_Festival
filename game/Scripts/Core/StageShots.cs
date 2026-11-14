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
/// Pictures of a second stage and the medical tent from a real game, run with:
/// godot --path game -- --stage-shots=DIR (needs a display). Each one starts a later year with
/// money in the bank, builds a site with two stages, and plays the day until the time the picture is of.
/// </summary>
public partial class StageShots : Node
{
    private static readonly (string Name, int Licence)[] Scenes =
    {
        ("1_booking_a_second_stage", 1500),
        ("2_two_stages_at_once", 1500),
        ("3_the_second_stage", 1500),
        ("4_the_medical_tent", 3000),
    };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;

    public StageShots(string directory)
    {
        _directory = directory;
    }

    public StageShots() : this("shots")
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
            GD.Print($"[stages] {path} in full {crowd?.InFull}, simple {crowd?.Simple}, {Engine.GetFramesPerSecond():0} fps");
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
            GD.Print("[stages] Done.");
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

    private static void RunTo(FestivalGame game, double hour)
    {
        var until = game.Day.AddHours(hour);
        while (game.Phase == Phase.Day && game.Now < until)
        {
            game.Clock.Step();
        }
    }

    /// <summary>Builds a site with a main stage, a second stage, a medical tent, and stalls along the bottom hedge.</summary>
    private static void Build(FestivalGame game)
    {
        var licence = game.Licence;
        var gap = game.Field.Gap;
        var y = gap.Y + 1;
        var f = game.Field.Field;
        game.BuildFence(FenceKind.SteelMesh, new TilePoint(gap.X, y), new TilePoint(gap.Right - 1, y));
        var row = Catalogue.Of(GateKind.TurnstileRow);
        for (var x = gap.X; x + row.Width <= gap.Right; x += row.Width)
        {
            game.BuildGate(GateKind.TurnstileRow, x, y, false);
            for (var lane = 0; lane < row.Lanes; lane++)
            {
                game.HireStaff(StaffKind.TicketStaff, new TilePoint(x + (lane * row.Width / row.Lanes) + 1, y));
            }
        }

        game.BuildStage(StageKind.HiredStage, f.X + 36, f.Y + 6, Facing.South);
        game.BuildStage(StageKind.MainStage, f.Right - 90, f.Y + 6, Facing.South);
        foreach (var stage in game.Site.Stages)
        {
            var rect = stage.Rect;
            game.BuildFence(FenceKind.CrowdBarrier, new TilePoint(rect.X - 2, rect.Bottom + 4), new TilePoint(rect.Right + 1, rect.Bottom + 4));
            game.HireStaff(StaffKind.Security, new TilePoint(rect.X + 4, rect.Bottom + 2));
            game.HireStaff(StaffKind.Security, new TilePoint(rect.Right - 5, rect.Bottom + 2));
        }

        // Rows of stalls along the bottom of the field, facing up it, leaving the way in clear.
        var kinds = new[] { StallKind.FoodVan, StallKind.Bar, StallKind.ToiletBlock, StallKind.WaterPoint };
        var wanted = Math.Max(8, licence / 110);
        var built = 0;
        for (var back = 0; built < wanted && back < 4; back++)
        {
            for (var x = f.X + 4; x + 26 < f.Right && built < wanted; x += 30)
            {
                if (x + 26 > gap.X - 8 && x < gap.Right + 8)
                {
                    continue;
                }

                if (game.BuildStall(kinds[built % kinds.Length], x, f.Bottom - 14 - (back * 28), Facing.North).Ok)
                {
                    built++;
                }
            }
        }

        var middle = f.X + (f.Width / 2);
        game.BuildStall(StallKind.MedicalTent, middle - 28, f.Y + 6, Facing.South);
        for (var i = 0; i < Math.Max(4, licence / 300); i++)
        {
            game.HireStaff(StaffKind.FirstAider, new TilePoint(f.X + 40 + (i * 37 % (f.Width - 80)), f.Y + 70 + (i * 23 % 40)));
        }

        for (var i = 0; i < Math.Max(3, licence / 500); i++)
        {
            game.HireStaff(StaffKind.Security, new TilePoint(f.X + 60 + (i * 61 % (f.Width - 120)), f.Y + 90 + (i * 17 % 30)));
        }
    }

    private void Arrange(FestivalScene festival)
    {
        var game = festival.Game;
        var name = Scenes[_scene].Name;
        festival.Camera.ShowWholeField();
        festival.Year.PickHeadliner(0);
        festival.Year.ToggleSecondStage();
        if (name == "1_booking_a_second_stage")
        {
            GD.Print($"[stages] {name}: second stage £{game.SecondStageFee}, act fees £{game.ActFees}");
            return;
        }

        festival.Year.StartBuilding();
        Build(game);
        var bank = game.Money;
        game.OpenGates();
        switch (name)
        {
            case "2_two_stages_at_once":
                RunTo(game, 14.72);
                break;

            case "3_the_second_stage":
            {
                RunTo(game, 14.72);
                var (sx, sy, _, _) = RunningOrder.Front(game.SecondShow.Stage!);
                festival.Camera.LookAt((float)sx, (float)sy + 5, 24f);
                break;
            }

            case "4_the_medical_tent":
            {
                // Run on until at least three people are resting in the tent, so the beds are seen in use.
                RunTo(game, 15);
                while (game.Phase == Phase.Day && game.Now.Hour < 21 && game.Incidents.Count(i => i.Resting) < 3)
                {
                    game.Clock.Step();
                }

                var tent = game.Site.Stalls.First(s => s.Kind == StallKind.MedicalTent).Rect;
                festival.Camera.LookAt((tent.X + (tent.Width / 2f)) * 0.5f, (tent.Y + tent.Height) * 0.5f, 13f);
                break;
            }
        }

        game.Clock.SetSpeed(GameSpeed.Paused);
        GD.Print($"[stages] {name}: {game.Now:HH:mm}, {game.Ground.Name}, bank after building £{bank}, on site {game.Crowd.OnSite} of {game.Crowd.Expected}, main {game.Show.Playing(game.Now)?.Act.Name}, second {game.SecondShow.Playing(game.Now)?.Act.Name}, watching {game.Crowd.Watching}, at the second stage {game.Crowd.Parties.Where(p => p.State == Simulation.People.PartyState.Watching && p.Watch >= RunningOrder.SecondBase).Sum(p => p.Members.Count)}, resting {game.Incidents.Count(i => i.Resting)}, worst {game.Density.Worst:0.0}, hurt {game.CrushInjuries}, happy {game.Crowd.Happiness}%, stalls {game.Site.Stalls.Count}");
    }
}
