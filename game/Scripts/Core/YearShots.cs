using System;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.UI;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Mock-up pictures for step 6, run with: godot --path game -- --year-shots=DIR (needs a display).
/// The planning screen, the build day, the morning after with litter, and two results screens.
/// The numbers are made up; nothing here is wired to the rules yet.
/// </summary>
public partial class YearShots : Node
{
    private static readonly (string Name, double Hour)[] Scenes =
    {
        ("1_plan_the_year", 8), ("2_build_day", 8), ("3_the_morning_after", 6.2), ("4_litter_closer", 6.2), ("5_results_good_year", 6.2), ("6_results_poor_year", 6.2), ("7_plan_year_two", 8),
    };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;

    public YearShots(string directory)
    {
        _directory = directory;
    }

    public YearShots() : this("shots")
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
                f.Game.Clock.SetSpeed(GameSpeed.Paused);
                Arrange(f);
                f.Camera.Snap();
                _ready = true;
                _frame = 0;
            }

            return;
        }

        if (_frame == 20)
        {
            var path = $"{_directory}/{Scenes[_scene].Name}.png";
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"[year] {path}");
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
            GD.Print("[year] Done.");
            GetTree().Quit(0);
            return;
        }

        var day = _scene is >= 2 and <= 5 ? new DateTime(2027, 7, 18) : new DateTime(2027, 7, 17);
        GameManager.Instance.StartNewGame(null, day.AddHours(Scenes[_scene].Hour), 9);
    }

    private static void Centre(FestivalScene festival, Control panel, bool dim = true)
    {
        var hud = festival.GetNode<CanvasLayer>("Hud");
        if (dim)
        {
            hud.AddChild(new ColorRect { Color = new Color(0.11f, 0.17f, 0.21f, 0.45f), AnchorRight = 1, AnchorBottom = 1, MouseFilter = Control.MouseFilterEnum.Stop });
        }

        var centre = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
        hud.AddChild(centre);
        centre.AddChild(panel);
    }

    private void Arrange(FestivalScene festival)
    {
        var game = festival.Game;
        var f = game.Field.Field;
        var gap = game.Field.Gap;
        festival.Camera.ShowWholeField();
        if (_scene is 1 or 2 or 3 or 4 or 5)
        {
            game.BuildStage(StageKind.Bandstand, f.X + 74, f.Y + 10, Facing.South);
            game.BuildFence(FenceKind.Paling, new TilePoint(gap.X, gap.Y + 1), new TilePoint(gap.Right - 1, gap.Y + 1));
            game.BuildGate(GateKind.VillageGate, gap.X + 2, gap.Y + 1, false);
            game.BuildStall(StallKind.TeaAndCake, f.X + 7, f.Y + 21, Facing.East);
            game.BuildStall(StallKind.FoodVan, f.X + 7, f.Y + 34, Facing.East);
            game.BuildStall(StallKind.Bar, f.X + 7, f.Y + 52, Facing.East);
            game.BuildStall(StallKind.Toilets, f.X + 26, f.Y + 5, Facing.South);
            game.BuildStall(StallKind.Toilets, f.X + 36, f.Y + 5, Facing.South);
            festival.Site.Rebuild();
        }

        switch (_scene)
        {
            case 0:
                Centre(festival, YearPanels.Plan(new PlanView(1, "Larkmoor Festival", "Saturday 17 July 2027", 200, 2000,
                    new[] { new TicketChoice(3, 200), new TicketChoice(5, 180), new TicketChoice(8, 130), new TicketChoice(12, 80) }, 1,
                    new[] { new ActOffer("The Paper Turnips", "Four-piece band", 180, 3), new ActOffer("DJ Skylark", "DJ", 120, 2), new ActOffer("The Velvet Otters", "Five-piece band", 350, 5) }, 0,
                    new[] { "Salt & Tractors", "Bramble & Magpies", "Muddy Weathervanes", "Wren Hallows", "Lantern Gateposts", "Odell Ashby" }, 25)));
                break;

            case 1:
            {
                // The day before: nobody here yet, build freely, then open the gates.
                var hud = festival.GetNode<CanvasLayer>("Hud");
                var panel = new PanelContainer { Theme = UiStyle.Theme };
                panel.AddThemeStyleboxOverride("panel", UiStyle.Card(margin: 16, radius: 16));
                var box = new VBoxContainer();
                box.AddThemeConstantOverride("separation", 6);
                panel.AddChild(box);
                box.AddChild(UiStyle.Heading("Build day", 20));
                var words = UiStyle.Body("Friday. Nobody is here yet and the clock is stopped. Put up what you need, then open the gates.", muted: true, size: 14);
                words.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                words.CustomMinimumSize = new Vector2(280, 0);
                box.AddChild(words);
                box.AddChild(UiStyle.Body("Expected tomorrow: about 180", size: 15));
                box.AddChild(UiStyle.Button("Open the gates", () => { }));
                hud.AddChild(panel);
                panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight, Control.LayoutPresetMode.Minsize, 18);
                panel.GrowHorizontal = Control.GrowDirection.Begin;
                panel.Position = new Vector2(panel.Position.X, 62);
                break;
            }

            case 2:
            case 3:
            {
                Litter(festival, 260);
                var hud = festival.GetNode<CanvasLayer>("Hud");
                var panel = YearPanels.Tidy(_scene == 2 ? 260 : 171, 260, 60);
                hud.AddChild(panel);
                panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight, Control.LayoutPresetMode.Minsize, 18);
                panel.GrowHorizontal = Control.GrowDirection.Begin;
                panel.Position = new Vector2(panel.Position.X, 62);
                if (_scene == 3)
                {
                    festival.Camera.LookAt(((f.X + 74) * 0.5f) + 1, ((f.Y + 30) * 0.5f) + 3, 22f);
                }

                break;
            }

            case 4:
                Centre(festival, YearPanels.Results(new ResultsView(1, "Larkmoor Festival, Saturday 17 July 2027", 180, 180, 81, 0,
                    new[] { ("Tickets", 900), ("Food van", 1010), ("Tea and cake table", 320), ("Bar tent", 1130) },
                    new[] { ("Act fees", 330), ("Stage", 400), ("Stalls and toilets", 430), ("Fences and gates", 46), ("Litter pick", 60) },
                    100, 6, "A grand day. Everyone fed, nobody hurt, and the field handed back clean. We would be glad to see it grow.", 200, 400)));
                break;

            case 5:
                Centre(festival, YearPanels.Results(new ResultsView(1, "Larkmoor Festival, Saturday 17 July 2027", 180, 180, 34, 96,
                    new[] { ("Tickets", 900), ("Tea and cake table", 240) },
                    new[] { ("Act fees", 500), ("Stage", 1500), ("Stalls and toilets", 70), ("Fences and gates", 46) },
                    40, 9, "Half the village went home hungry and the top field is still full of cans. Put that right and we will talk about more people.", 200, 200)));
                break;

            default:
                Centre(festival, YearPanels.Plan(new PlanView(2, "Larkmoor Festival", "Saturday 15 July 2028", 400, 4164,
                    new[] { new TicketChoice(5, 400), new TicketChoice(8, 340), new TicketChoice(12, 250), new TicketChoice(18, 140) }, 1,
                    new[] { new ActOffer("The Velvet Otters", "Five-piece band", 380, 5), new ActOffer("The Tin Skylarks", "Four-piece band", 260, 4), new ActOffer("DJ Hedgerow", "DJ", 150, 2) }, 1,
                    new[] { "The Paper Turnips", "Salt & Tractors", "Crooked Wellies", "Betsy Lark", "Copper Haybales", "Jem Tolliver" }, 30)));
                break;
        }
    }

    /// <summary>Litter where people stood and queued, and grass worn brown in front of the stage and the stalls.</summary>
    private static void Litter(FestivalScene festival, int pieces)
    {
        var f = festival.Game.Field.Field;
        var random = new Random(5);
        var b = new MeshBuilder();
        var colours = new[] { new Color("#F5F5F5"), new Color("#D9483B"), new Color("#4FA8D8"), new Color("#F6D84A"), new Color("#C9C2B0"), new Color("#7A4A2A") };
        (float X, float Z, float Spread, int Share)[] heaps =
        {
            ((f.X + 80) * 0.5f, (f.Y + 30) * 0.5f, 6f, 40), ((f.X + 20) * 0.5f, (f.Y + 40) * 0.5f, 3.5f, 22), ((f.X + 22) * 0.5f, (f.Y + 58) * 0.5f, 3.5f, 20),
            ((f.X + 60) * 0.5f, (f.Y + 45) * 0.5f, 14f, 18),
        };
        for (var i = 0; i < pieces; i++)
        {
            var roll = random.Next(100);
            var heap = heaps[0];
            foreach (var h in heaps)
            {
                if (roll < h.Share)
                {
                    heap = h;
                    break;
                }

                roll -= h.Share;
            }

            var angle = (float)(random.NextDouble() * Math.Tau);
            var far = (float)Math.Sqrt(random.NextDouble()) * heap.Spread;
            var at = new Vector3(heap.X + (MathF.Cos(angle) * far), 0.05f, heap.Z + (MathF.Sin(angle) * far * 0.8f));
            var colour = colours[random.Next(colours.Length)];
            var turn = new Vector3(0, (float)random.NextDouble() * 180, 0);
            switch (random.Next(3))
            {
                case 0:
                    b.Tube(at + new Vector3(0, 0.03f, 0), new Vector3(0.12f, 0.2f, 0.12f), colour, new Vector3(90, turn.Y, 0));
                    break;
                case 1:
                    b.Box(at, new Vector3(0.3f, 0.04f, 0.22f), colour, turn);
                    break;
                default:
                    b.Ball(at + new Vector3(0, 0.04f, 0), new Vector3(0.2f, 0.14f, 0.18f), colour, turn);
                    break;
            }
        }

        festival.AddChild(b.BuildNode("Litter"));

        // Worn grass: flat brown patches where the most feet went.
        var worn = new StandardMaterial3D { AlbedoColor = new Color(0.48f, 0.4f, 0.27f, 0.55f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        foreach (var (x, z, size) in new[] { (heaps[0].X, heaps[0].Z - 2.5f, 9f), (heaps[1].X - 2, heaps[1].Z, 4f), (heaps[2].X - 2, heaps[2].Z, 4.5f), ((f.X + 32) * 0.5f, (f.Y + 12) * 0.5f, 4f) })
        {
            festival.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = size / 2, BottomRadius = size / 2, Height = 0.01f, RadialSegments = 24 },
                MaterialOverride = worn,
                Position = new Vector3(x, 0.02f, z),
                Scale = new Vector3(1.3f, 1, 0.8f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }
    }
}
