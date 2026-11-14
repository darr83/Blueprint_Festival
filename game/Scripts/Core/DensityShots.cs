using System;
using System.Collections.Generic;
using System.Linq;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.UI;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Mock-up pictures for step 8, run with: godot --path game -- --density-shots=DIR (needs a display).
/// The festival behind is a real game. The packed crowds, the barrier, the casualties and the
/// warning cards are posed on top of it; nothing here is wired to the rules yet.
/// </summary>
public partial class DensityShots : Node
{
    private static readonly (string Name, double Hour)[] Scenes =
    {
        ("1_crowd_view_on_a_good_day", 14.6), ("2_headliner_too_big_for_the_stage", 21.2), ("3_a_pinch_point_at_the_gate", 22.6),
        ("4_front_barrier_and_security", 16.2), ("5_a_crush_at_the_front", 21.3),
    };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;

    public DensityShots(string directory)
    {
        _directory = directory;
    }

    public DensityShots() : this("shots")
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

        if (_frame == 30)
        {
            var path = $"{_directory}/{Scenes[_scene].Name}.png";
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"[density] {path}");
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
            GD.Print("[density] Done.");
            GetTree().Quit(0);
            return;
        }

        var only = OS.GetEnvironment("SHOTS_ONLY");
        if (only.Length > 0 && !Scenes[_scene].Name.Contains(only, StringComparison.Ordinal))
        {
            NextScene();
            return;
        }

        GameManager.Instance.StartNewFestival(null);
    }

    /// <summary>
    /// Stands a posed crowd in a fan that starts at a place and runs back from it: shoulder to
    /// shoulder at the front, loosening with depth. Returns where everyone stands, in metres.
    /// </summary>
    private static List<(double X, double Y)> Pack(Node3D holder, double x, double y, int dirX, int dirY, double from, double depth,
        double half, double spread, double tight, double loose, int seed, Func<double, double, bool>? keepClear = null)
    {
        var random = new Random(seed);
        var placed = new List<(double X, double Y)>();
        var turn = Mathf.Atan2(-dirX, -dirY);
        var d = from;
        while (d < from + depth)
        {
            var part = (d - from) / depth;
            var gap = tight + ((loose - tight) * part);
            var width = half + ((d - from) * spread);
            for (var a = -width; a <= width; a += gap)
            {
                var deep = d + ((random.NextDouble() - 0.5) * gap * 0.5);
                var side = a + ((random.NextDouble() - 0.5) * gap * 0.5);
                var px = x + (dirX * deep) - (dirY * side);
                var py = y + (dirY * deep) + (dirX * side);
                if (random.NextDouble() < 0.06 + (part * 0.2) || (keepClear?.Invoke(px, py) ?? false))
                {
                    continue;
                }

                var view = PersonView.Build(LookGenerator.FestivalGoer(seed + (placed.Count * 37)));
                holder.AddChild(view);
                view.Position = new Vector3((float)px, 0, (float)py);
                view.Rotation = new Vector3(0, turn + (float)((random.NextDouble() - 0.5) * 0.3), 0);
                view.SetPose(random.Next(10) < 3 ? Pose.Cheering : Pose.Standing, random.Next(8) / 8f);
                placed.Add((px, py));
            }

            d += gap;
        }

        return placed;
    }

    /// <summary>A card under the top bar with a coloured stripe, a heading, a line or two, and buttons.</summary>
    private static void Card(Node hud, Color stripe, string heading, string body, bool left, params string[] buttons)
    {
        var card = new PanelContainer { Theme = UiStyle.Theme };
        card.AddThemeStyleboxOverride("panel", UiStyle.Card(margin: 14, radius: 14));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        card.AddChild(row);
        row.AddChild(new ColorRect { Color = stripe, CustomMinimumSize = new Vector2(8, 0) });
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 6);
        row.AddChild(column);
        column.AddChild(UiStyle.Heading(heading, 20));
        foreach (var line in body.Split('\n'))
        {
            column.AddChild(UiStyle.Body(line, muted: true, size: 15));
        }

        if (buttons.Length > 0)
        {
            var actions = new HBoxContainer();
            actions.AddThemeConstantOverride("separation", 8);
            column.AddChild(actions);
            for (var i = 0; i < buttons.Length; i++)
            {
                actions.AddChild(i == 0 ? UiStyle.Button(buttons[i], () => { }) : UiStyle.SecondaryButton(buttons[i], () => { }));
            }
        }

        hud.AddChild(card);
        card.SetAnchorsAndOffsetsPreset(left ? Control.LayoutPreset.TopLeft : Control.LayoutPreset.TopRight, Control.LayoutPresetMode.Minsize, 16);
        card.GrowHorizontal = left ? Control.GrowDirection.End : Control.GrowDirection.Begin;
        card.OffsetTop = 66;
        card.OffsetBottom = 66;
    }

    /// <summary>The key to the three colours, top left.</summary>
    private static void Key(Node hud)
    {
        var card = new PanelContainer { Theme = UiStyle.Theme };
        card.AddThemeStyleboxOverride("panel", UiStyle.Card(margin: 14, radius: 14));
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 6);
        card.AddChild(column);
        column.AddChild(UiStyle.Heading("Crowd view", 18));
        foreach (var (colour, text) in new[]
        {
            (DensityView.Comfortable, "Comfortable: under 2 people per m²"),
            (DensityView.BusyAmber, "Busy: 2 to 4 people per m²"),
            (DensityView.CrushRed, "Crush risk: over 4 people per m²"),
        })
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            row.AddChild(new ColorRect { Color = colour, CustomMinimumSize = new Vector2(18, 18), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
            row.AddChild(UiStyle.Body(text, size: 15));
            column.AddChild(row);
        }

        hud.AddChild(card);
        card.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft, Control.LayoutPresetMode.Minsize, 16);
        card.OffsetTop = 66;
        card.OffsetBottom = 66;
    }

    private void Arrange(FestivalScene festival)
    {
        var game = festival.Game;
        var f = game.Field.Field;
        var gap = game.Field.Gap;
        const float t = 0.5f;
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
        game.BuildStall(StallKind.FirstAidTent, f.X + 56, f.Y + 4, Facing.South);
        game.HireStaff(StaffKind.TicketStaff, new TilePoint(gap.X + 4, gap.Y + 1));
        game.HireStaff(StaffKind.FirstAider, new TilePoint(f.X + 60, f.Y + 13));
        game.HireStaff(StaffKind.Security, new TilePoint(f.X + 20, f.Y + 50));
        game.OpenGates();
        var (name, hour) = Scenes[_scene];
        var until = game.Day.AddHours(hour);
        while (game.Phase == Phase.Day && game.Now < until)
        {
            game.Clock.Step();
        }

        game.Clock.SetSpeed(GameSpeed.Paused);
        festival.Site.Rebuild();
        festival.Litter.Refresh();
        festival.Crowd.Snap();
        festival.Staff.Snap();

        var hud = festival.TopBar.GetParent();
        var stage = game.Show.Stage!;
        var (sx, sy, dx, dy) = RunningOrder.Front(stage);
        var stageHalf = stage.Rect.Width * t / 2.0;
        var posed = new Node3D { Name = "Posed" };
        festival.AddChild(posed);
        var density = new DensityView { Name = "Density" };
        festival.AddChild(density);
        var extras = new List<(double X, double Y)>();
        Func<double, double, bool> hidden = (_, _) => false;

        switch (name)
        {
            case "1_crowd_view_on_a_good_day":
                extras = Pack(posed, sx, sy, dx, dy, 1.4, 5, stageHalf * 0.8, 0.5, 0.72, 1.1, 11);
                festival.Camera.LookAt((float)sx, (float)sy + 9, 24f);
                break;

            case "2_headliner_too_big_for_the_stage":
                extras = Pack(posed, sx, sy, dx, dy, 1.2, 15, stageHalf * 0.9, 0.55, 0.43, 0.85, 21);
                festival.Camera.LookAt((float)sx, (float)sy + 6.5f, 17f);
                Card(hud, DensityView.CrushRed, "Crush risk at the Bandstand", "Too many people want to be at the front.\nPeople will start to get hurt if it stays like this.", false,
                    "Pause the act", "Send security");
                break;

            case "3_a_pinch_point_at_the_gate":
            {
                var gx = (gap.X + (gap.Width / 2.0)) * t;
                var gy = gap.Y * t;
                extras = Pack(posed, gx, gy, 0, -1, 0.4, 9, 2.8, 0.75, 0.4, 0.9, 31);
                festival.Camera.LookAt((float)gx, (float)gy - 3, 16f);
                Card(hud, DensityView.CrushRed, "Crush risk at the gate", "Everyone is leaving at once through one narrow gate.\nA wider gate, or a second way out, would spread them.", false,
                    "Open the fence", "Send security");
                break;
            }

            case "4_front_barrier_and_security":
            {
                var barrierLength = (float)((stageHalf * 2) + 5);
                var barrier = SiteProps.CrowdBarrier(barrierLength);
                posed.AddChild(barrier);
                barrier.Position = new Vector3((float)sx - (barrierLength / 2), 0, (float)sy + 2.6f);
                hidden = (x, y) => Math.Abs(x - sx) < (barrierLength / 2) + 0.5 && y > sy - 0.5 && y < sy + 3.5;
                extras = Pack(posed, sx, sy, dx, dy, 3.5, 9, (barrierLength / 2) - 0.4, 0.4, 0.66, 1.0, 41);
                for (var i = 0; i < 3; i++)
                {
                    var guard = SiteProps.Staff(StaffKind.Security, 20 + i);
                    posed.AddChild(guard);
                    guard.Position = new Vector3((float)sx + ((i - 1) * 3.2f), 0, (float)sy + 1.5f);
                }

                festival.Camera.LookAt((float)sx, (float)sy + 4.5f, 13f);
                Card(hud, DensityView.BusyAmber, "Busy at the Bandstand", "The barrier keeps a clear strip in front of the stage.\nSecurity there can lift people out before they are hurt.", false);
                break;
            }

            case "5_a_crush_at_the_front":
            {
                var down = new[] { (X: sx - 1.6, Y: sy + 2.1), (X: sx + 0.9, Y: sy + 2.6), (X: sx + 2.6, Y: sy + 1.9) };
                // The front few rows have been moved back, so the people who are down can be reached.
                hidden = (x, y) => y < sy + 3.4 && y > sy - 0.5 && Math.Abs(x - sx) < 9;
                extras = Pack(posed, sx, sy, dx, dy, 1.2, 14, stageHalf * 0.9, 0.55, 0.43, 0.85, 51, hidden);
                var markers = new List<Node3D>();
                for (var i = 0; i < down.Length; i++)
                {
                    var hurt = PersonView.Build(LookGenerator.FestivalGoer(900 + (i * 13)));
                    posed.AddChild(hurt);
                    hurt.Position = new Vector3((float)down[i].X, 0.14f, (float)down[i].Y);
                    hurt.Rotation = new Vector3(-Mathf.Pi / 2, Mathf.Pi / 2, 0);
                    var marker = SiteProps.HelpMarker(0.5f);
                    posed.AddChild(marker);
                    marker.Position = new Vector3((float)down[i].X, 0, (float)down[i].Y);
                    markers.Add(marker);
                    if (i < 2)
                    {
                        var medic = SiteProps.Staff(StaffKind.FirstAider, 30 + i);
                        posed.AddChild(medic);
                        medic.Position = new Vector3((float)down[i].X + 0.7f, 0, (float)down[i].Y + 0.35f);
                        medic.RotationDegrees = new Vector3(0, -90, 0);
                    }
                }

                festival.Camera.Turn(-1);
                festival.Camera.LookAt((float)sx, (float)sy + 3f, 10f);
                festival.Camera.Snap();
                foreach (var marker in markers)
                {
                    marker.Rotation = new Vector3(0, festival.Camera.Camera.GlobalRotation.Y, 0);
                }

                Card(hud, DensityView.CrushRed, "Three people hurt at the Bandstand", "The act has stopped. First-aiders are with them.\nThis will be on the morning-after results.", false,
                    "Clear the front", "Start the act again");
                break;
            }
        }

        // Real festival-goers standing where the posed scene needs the ground clear are taken out of the picture.
        foreach (var child in festival.Crowd.GetChildren())
        {
            if (child is PersonView view && hidden(view.Position.X, view.Position.Z))
            {
                view.Visible = false;
            }
        }

        var everyone = game.Crowd.People.Where(p => !hidden(p.X, p.Y)).Select(p => (p.X, p.Y)).Concat(extras);
        density.Draw(everyone, f);
        density.Label(density.WorstAt.X, density.WorstAt.Y, $"{DensityView.Word(density.Worst)}  {density.Worst:0.0} per m²");
        Key(hud);
        if (festival.GetNodeOrNull<Control>("Hud/ViewButtons")?.GetChild(0) is Container row)
        {
            var toggle = UiStyle.Button("Crowd view", () => { });
            row.AddChild(toggle);
            row.MoveChild(toggle, 0);
        }

        festival.Camera.Snap();
        GD.Print($"[density] {name}: {game.Now:HH:mm}, on site {game.Crowd.OnSite}, posed {extras.Count}, worst {density.Worst:0.0}");
    }
}
