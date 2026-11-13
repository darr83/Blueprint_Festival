using System;
using System.Linq;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Pictures of the staff, run with: godot --path game -- --staff-shots=DIR (needs a display).
/// Each one plans and builds a real festival, hires staff through the game's rules and runs the
/// day on until the moment wanted, so what is shown is what the game does. Only the first
/// picture, the three uniforms side by side, is posed.
/// </summary>
public partial class StaffShots : Node
{
    private static readonly string[] Scenes =
    {
        "1_the_three_uniforms", "2_the_staff_tab", "3_ticket_staff_at_the_gate", "4_security_by_the_stage", "5_someone_taken_ill",
        "6_first_aider_at_work", "7_resting_in_the_first_aid_tent", "8_trouble_for_security", "9_nobody_on_the_gate",
    };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;

    public StaffShots(string directory)
    {
        _directory = directory;
    }

    public StaffShots() : this("shots")
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
                f.Crowd.Snap();
                f.Staff.Snap();
                f.Camera.Snap();
                _ready = true;
                _frame = 0;
            }

            return;
        }

        if (_frame == 30)
        {
            var path = $"{_directory}/{Scenes[_scene]}.png";
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"[staff] {path}");
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
            GD.Print("[staff] Done.");
            GetTree().Quit(0);
            return;
        }

        GameManager.Instance.StartNewFestival(null);
    }

    /// <summary>Runs the day on until something is true, or until a time of day at the latest.</summary>
    private static bool RunUntil(FestivalGame game, Func<bool> when, double latestHour = 23)
    {
        var latest = game.Day.AddHours(latestHour);
        while (game.Phase == Phase.Day && game.Now < latest)
        {
            if (when())
            {
                return true;
            }

            game.Clock.Step();
        }

        return false;
    }

    private void Arrange(FestivalScene festival)
    {
        var game = festival.Game;
        var f = game.Field.Field;
        var gap = game.Field.Gap;
        const float t = 0.5f;
        festival.Camera.ShowWholeField();
        festival.Year.PickHeadliner(0);
        festival.Year.StartBuilding();
        game.BuildStage(StageKind.Bandstand, f.X + 74, f.Y + 10, Facing.South);
        game.BuildFence(FenceKind.Paling, new TilePoint(gap.X, gap.Y + 1), new TilePoint(gap.Right - 1, gap.Y + 1));
        game.BuildGate(GateKind.VillageGate, gap.X + 2, gap.Y + 1, false);
        game.BuildStall(StallKind.TeaAndCake, f.X + 7, f.Y + 21, Facing.East);
        game.BuildStall(StallKind.FoodVan, f.X + 7, f.Y + 34, Facing.East);
        game.BuildStall(StallKind.Bar, f.X + 7, f.Y + 52, Facing.East);
        game.BuildStall(StallKind.Toilets, f.X + 26, f.Y + 5, Facing.South);
        game.BuildStall(StallKind.Toilets, f.X + 36, f.Y + 5, Facing.South);
        game.BuildStall(StallKind.WaterPoint, f.X + 48, f.Y + 5, Facing.South);
        var tent = new TilePoint(f.X + 56, f.Y + 4);
        game.BuildStall(StallKind.FirstAidTent, tent.X, tent.Y, Facing.South);
        var name = Scenes[_scene];
        if (name != "9_nobody_on_the_gate")
        {
            game.HireStaff(StaffKind.TicketStaff, new TilePoint(gap.X + 4, gap.Y + 1));
        }

        game.HireStaff(StaffKind.FirstAider, new TilePoint(f.X + 60, f.Y + 13));
        game.HireStaff(StaffKind.FirstAider, new TilePoint(f.X + 40, f.Y + 44));
        game.HireStaff(StaffKind.Security, new TilePoint(f.X + 72, f.Y + 24));
        game.HireStaff(StaffKind.Security, new TilePoint(f.X + 88, f.Y + 24));
        game.HireStaff(StaffKind.Security, new TilePoint(f.X + 20, f.Y + 50));

        switch (name)
        {
            case "1_the_three_uniforms":
            {
                var at = new Vector3((f.X + 60) * t, 0, (f.Y + 46) * t);
                var kinds = new[] { StaffKind.TicketStaff, StaffKind.Security, StaffKind.FirstAider };
                for (var i = 0; i < kinds.Length; i++)
                {
                    var figure = SiteProps.Staff(kinds[i], i + 1);
                    festival.AddChild(figure);
                    figure.Position = at + new Vector3((i - 1) * 1.1f, 0, (1 - i) * 1.1f);
                    figure.RotationDegrees = new Vector3(0, 45, 0);
                }

                festival.Camera.LookAt(at.X, at.Z, 8f);
                festival.SetHudShown(false);
                break;
            }

            case "2_the_staff_tab":
                festival.BuildBar.Toggle(BuildMode.Staff);
                festival.BuildBar.Choose("staffSecurity");
                festival.Build.ShowGhost(new TilePoint(f.X + 52, f.Y + 36));
                break;

            case "3_ticket_staff_at_the_gate":
                game.OpenGates();
                RunUntil(game, () => false, 9.7);
                festival.Camera.LookAt((gap.X + 7) * t, (gap.Y + 1) * t, 13f);
                break;

            case "4_security_by_the_stage":
                game.OpenGates();
                RunUntil(game, () => false, 14.7);
                festival.Camera.LookAt((f.X + 80) * t, (f.Y + 27) * t, 15f);
                break;

            case "5_someone_taken_ill":
            {
                game.OpenGates();
                RunUntil(game, () => game.Incidents.Any(i => i.Kind == IncidentKind.Ill && InTheOpen(game, i.X, i.Y) && i.Staff >= 0 && game.Staff.First(s => s.Id == i.Staff).State == StaffState.Going));
                // A few more steps so the first-aider is seen on the way.
                for (var i = 0; i < 6; i++)
                {
                    game.Clock.Step();
                }

                var ill = game.Incidents.FirstOrDefault(i => i.Kind == IncidentKind.Ill && InTheOpen(game, i.X, i.Y));
                if (ill is not null)
                {
                    var medic = game.Staff.FirstOrDefault(s => s.Id == ill.Staff);
                    festival.Camera.LookAt((float)((ill.X * 2) + (medic?.X ?? ill.X)) / 3, (float)((ill.Y * 2) + (medic?.Y ?? ill.Y)) / 3, 10f);
                }

                break;
            }

            case "6_first_aider_at_work":
            {
                game.OpenGates();
                RunUntil(game, () => game.Staff.Any(s => s.Kind == StaffKind.FirstAider && s.State == StaffState.Busy && InTheOpen(game, s.X, s.Y)));
                if (game.Staff.FirstOrDefault(s => s.Kind == StaffKind.FirstAider && s.State == StaffState.Busy && InTheOpen(game, s.X, s.Y)) is { } busy)
                {
                    festival.Camera.LookAt((float)busy.X, (float)busy.Y, 8f);
                }

                break;
            }

            case "7_resting_in_the_first_aid_tent":
                game.OpenGates();
                RunUntil(game, () => game.Incidents.Any(i => i.Resting));
                festival.Camera.LookAt((tent.X + 4) * t, (tent.Y + 6) * t, 8f);
                break;

            case "8_trouble_for_security":
            {
                game.OpenGates();
                RunUntil(game, () => game.Incidents.Any(i => i.Kind != IncidentKind.Ill));
                if (game.Incidents.FirstOrDefault(i => i.Kind != IncidentKind.Ill) is { } trouble)
                {
                    festival.Camera.LookAt((float)trouble.X, (float)trouble.Y, 12f);
                }

                break;
            }

            default:
                game.OpenGates();
                RunUntil(game, () => false, 9.9);
                festival.Camera.LookAt((gap.X + 7) * t, (gap.Y + 6) * t, 16f);
                break;
        }

        game.Clock.SetSpeed(GameSpeed.Paused);
        GD.Print($"[staff] {name}: {game.Now:HH:mm}, incidents {game.Incidents.Count}, treated {game.Treated}, untreated {game.Untreated}, sorted {game.TroubleSorted}, ignored {game.TroubleIgnored}, bank £{game.Money}");
    }

    /// <summary>True when a place is well clear of every stall, so nothing hides it in a picture.</summary>
    private static bool InTheOpen(FestivalGame game, double x, double y)
    {
        var tx = x / FieldMap.TileMetres;
        var ty = y / FieldMap.TileMetres;
        return game.Site.Stalls.All(s => tx < s.Rect.X - 12 || tx > s.Rect.X + s.Rect.Width + 12 || ty < s.Rect.Y - 12 || ty > s.Rect.Y + s.Rect.Height + 12);
    }
}
