using System;
using System.Linq;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.UI;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Pictures of paths and the camping field from a real game, run with:
/// godot --path game -- --path-shots=DIR (needs a display). Nothing is posed: paths are laid with
/// the build tool, and the festival day is played until the time the picture is of.
/// </summary>
public partial class PathShots : Node
{
    private static readonly string[] Scenes =
    {
        "1_the_three_paths_on_the_ground_tab", "2_paths_at_the_village_festival", "3_some_keep_to_the_path_some_cut_across",
        "4_a_wet_afternoon_mud_beside_the_gravel", "5_paths_across_the_showground", "6_tents_dotted_about_the_camping_field",
        "7_among_the_tents_at_dusk", "8_the_camping_field_from_above",
    };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;

    public PathShots(string directory)
    {
        _directory = directory;
    }

    public PathShots() : this("shots")
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
            again.Crowd.Snap();
        }

        if (_frame == 30)
        {
            var path = $"{_directory}/{Scenes[_scene]}.png";
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"[paths] {path}");
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
            GD.Print("[paths] Done.");
            GetTree().Quit(0);
            return;
        }

        var only = OS.GetEnvironment("SHOTS_ONLY");
        if (only.Length > 0 && !Scenes[_scene].Contains(only, StringComparison.Ordinal))
        {
            NextScene();
            return;
        }

        if (_scene >= 4)
        {
            const int licence = 5000;
            var year = FestivalGame.Licences.ToList().IndexOf(licence) + 1;
            GameManager.Instance.Open(FestivalGame.StartYear(null, new YearSetup(year, licence, 4000 + (licence * 14), false, 82)));
        }
        else
        {
            GameManager.Instance.StartNewFestival(null);
        }
    }

    /// <summary>Lays a path with the build tool, from one tile to another.</summary>
    private static void Lay(FestivalScene festival, GroundKind kind, int x0, int y0, int x1, int y1)
    {
        var f = festival.Game.Field.Field;
        festival.Build.PickGround(kind);
        festival.Build.DragGround(new TilePoint(f.X + x0, f.Y + y0), new TilePoint(f.X + x1, f.Y + y1));
    }

    private void Arrange(FestivalScene festival)
    {
        var game = festival.Game;
        var name = Scenes[_scene];
        festival.Camera.ShowWholeField();
        if (_scene >= 4)
        {
            ArrangeShowground(festival, name);
            return;
        }

        var f = game.Field.Field;
        var gap = game.Field.Gap;
        if (name.Contains("wet", StringComparison.Ordinal))
        {
            game.SetWeather(DayWeather.Of(WeatherKind.Rain, 3));
        }

        festival.Year.PickHeadliner(game.Headliners.ToList().FindIndex(h => h.Stars == game.Headliners.Min(o => o.Stars)));
        festival.Year.StartBuilding();
        game.BuildStage(StageKind.Bandstand, f.X + 70, f.Y + 10, Facing.South);
        game.BuildFence(FenceKind.Paling, new TilePoint(gap.X, gap.Y + 1), new TilePoint(gap.Right - 1, gap.Y + 1));
        game.BuildGate(GateKind.VillageGate, gap.X + 2, gap.Y + 1, false);
        game.BuildStall(StallKind.FoodVan, f.X + 7, f.Y + 30, Facing.East);
        game.BuildStall(StallKind.Bar, f.X + 7, f.Y + 48, Facing.East);
        game.BuildStall(StallKind.Toilets, f.X + 26, f.Y + 5, Facing.South);
        game.BuildStall(StallKind.Toilets, f.X + 36, f.Y + 5, Facing.South);
        game.BuildStall(StallKind.WaterPoint, f.X + 46, f.Y + 5, Facing.South);
        game.BuildStall(StallKind.FirstAidTent, f.X + 54, f.Y + 4, Facing.South);
        game.HireStaff(StaffKind.TicketStaff, new TilePoint(gap.X + 4, gap.Y + 1));
        game.HireStaff(StaffKind.FirstAider, new TilePoint(f.X + 60, f.Y + 16));
        game.HireStaff(StaffKind.FirstAider, new TilePoint(f.X + 96, f.Y + 30));

        // A gravel path in from the gate, up to the stage; bark chips off it to the food and the bar;
        // a mown path along the top past the toilets and the tea.
        var gx = gap.X - f.X + (gap.Width / 2);
        Lay(festival, GroundKind.Gravel, gx, f.Height - 2, gx, 30);
        Lay(festival, GroundKind.Gravel, gx, 30, 84, 30);
        Lay(festival, GroundKind.BarkChip, 14, 38, gx, 38);
        Lay(festival, GroundKind.BarkChip, 14, 54, gx, 54);
        Lay(festival, GroundKind.MownPath, 24, 14, 66, 14);
        Lay(festival, GroundKind.MownPath, gx, 14, gx, 26);
        festival.Build.Stop();

        var stage = game.Show.Stage!;
        var (sx, sy, _, _) = RunningOrder.Front(stage);
        var bank = game.Money;
        if (name == "1_the_three_paths_on_the_ground_tab")
        {
            festival.BuildBar.Toggle(BuildMode.Ground);
            festival.BuildBar.Choose($"ground{GroundKind.Gravel}");
            festival.Camera.LookAt((f.X + gx) * 0.5f, (f.Y + 34) * 0.5f, 26f);
            GD.Print($"[paths] {name}: bank £{bank}, path patches {game.PathPatches}, spent on paths £{game.Spending.Where(l => l.What == "Paths").Sum(l => l.Amount)}");
            return;
        }

        game.OpenGates();
        switch (name)
        {
            case "2_paths_at_the_village_festival":
                WeatherShots.RunTo(game, 13.5);
                break;

            case "3_some_keep_to_the_path_some_cut_across":
                WeatherShots.RunTo(game, 11.2);
                festival.Camera.LookAt((f.X + gx) * 0.5f, (f.Y + 44) * 0.5f, 18f);
                break;

            case "4_a_wet_afternoon_mud_beside_the_gravel":
                WeatherShots.RunTo(game, 18.5);
                festival.Camera.LookAt((f.X + gx) * 0.5f, (f.Y + 40) * 0.5f, 20f);
                break;
        }

        game.Clock.SetSpeed(GameSpeed.Paused);
        var walking = game.Crowd.Parties.Where(p => p.IsOnSite && p.State is Simulation.People.PartyState.WalkingIn or Simulation.People.PartyState.Strolling).ToList();
        var onPath = walking.Count(p => game.PatchAt(FieldMap.TileAt(p.X, p.Y)) is var (c, r) && game.PathAt(c, r));
        GD.Print($"[paths] {name}: {game.Now:HH:mm}, on site {game.Crowd.OnSite}, walking {walking.Count} ({onPath} on a path), keep to paths {game.Crowd.PathShare:P0}, wet {game.Wetness:0.00}, muddy {game.MuddyPatches}, worn {game.GrassWorn}%, happy {game.Crowd.Happiness}%");
    }

    private static void ArrangeShowground(FestivalScene festival, string name)
    {
        var game = festival.Game;
        festival.Year.PickHeadliner(0);
        festival.Year.ToggleCamping();
        festival.Year.StartBuilding();
        StageShots.Build(game);

        // A ring of gravel round the middle of the field, with bark chip spokes to the stages and a
        // mown path to the camping field gateway.
        var w = game.WearColumns;
        var h = game.WearRows;
        var f = game.Field.Field;
        var fs = festival.Build;
        void Path(GroundKind kind, int c0, int r0, int c1, int r1)
        {
            fs.PickGround(kind);
            fs.DragGround(new TilePoint(f.X + (c0 * 4) + 1, f.Y + (r0 * 4) + 1), new TilePoint(f.X + (c1 * 4) + 1, f.Y + (r1 * 4) + 1));
        }

        Path(GroundKind.Gravel, 10, 24, w - 11, 24);
        Path(GroundKind.Gravel, 10, 24, 10, h - 16);
        Path(GroundKind.Gravel, w - 11, 24, w - 11, h - 16);
        var gate = game.PatchAt(new TilePoint(game.Field.Gap.X + (game.Field.Gap.Width / 2), f.Bottom - 1))!.Value;
        Path(GroundKind.Gravel, gate.Column, 24, gate.Column, h - 1);
        Path(GroundKind.Gravel, 10, h - 16, w - 11, h - 16);
        Path(GroundKind.BarkChip, w / 2, 12, w / 2, 24);
        Path(GroundKind.BarkChip, 20, 12, 20, 24);
        var campRow = game.PatchAt(new TilePoint(f.Right - 1, game.Field.CampGap.Y + (game.Field.CampGap.Height / 2)))!.Value.Row;
        Path(GroundKind.MownPath, w - 11, campRow, w - 1, campRow);
        fs.Stop();
        var bank = game.Money;
        game.OpenGates();
        var camp = game.Field.Camp;
        var (campX, campY) = ((camp.X + (camp.Width / 2f)) * 0.5f, (camp.Y + (camp.Height / 2f)) * 0.5f);
        switch (name)
        {
            case "5_paths_across_the_showground":
                StageShots.RunTo(game, 15);
                break;

            case "6_tents_dotted_about_the_camping_field":
                StageShots.RunTo(game, 15);
                festival.Camera.LookAt(campX - 8, campY + 6, 60f);
                break;

            case "7_among_the_tents_at_dusk":
                StageShots.RunTo(game, 21.2);
                festival.Camera.LookAt(campX - 20, campY + 14, 34f);
                break;

            case "8_the_camping_field_from_above":
                StageShots.RunTo(game, 30);
                festival.Camera.LookAt(campX, campY, 80f);
                break;
        }

        festival.Camp.Refresh();
        game.Clock.SetSpeed(GameSpeed.Paused);
        GD.Print($"[paths] {name}: {game.Now:HH:mm} {game.Phase}, bank after building £{bank}, path patches {game.PathPatches}, on site {game.Crowd.OnSite}, campers in {game.Crowd.CampersIn}, tents drawn {festival.Camp.TentCount}, pitches {game.Field.Pitches().Count}, worn {game.GrassWorn}%, happy {game.Crowd.Happiness}%");
    }
}
