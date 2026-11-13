using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Pictures of the stalls, run with: godot --path game -- --stall-shots=DIR (needs a display).
/// Each one starts a real festival, builds it through the game's rules, runs the day on to a set
/// time and takes a picture, so what is shown is what the game does.
/// </summary>
public partial class StallShots : Node
{
    /// <summary>Layout 0 has everything, 1 has one tea table and one toilet block, 2 has a stage and nothing else.</summary>
    private static readonly (string Name, int Layout, double Hour, int View, bool Money)[] Scenes =
    {
        ("1_village_festival_with_stalls", 0, 13.4, 0, false),
        ("2_food_van_and_tea_table", 0, 13.4, 1, false),
        ("3_the_bar", 0, 14.2, 2, false),
        ("4_toilets_and_water", 0, 14.2, 3, false),
        ("5_money_panel_on_a_good_day", 0, 16.5, 0, true),
        ("6_too_few_stalls", 1, 14.5, 4, false),
        ("7_nothing_to_eat_or_drink", 2, 13.5, 4, false),
        ("8_money_panel_on_a_bad_day", 2, 16.5, 0, true),
    };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;

    public StallShots(string directory)
    {
        _directory = directory;
    }

    public StallShots() : this("shots")
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
            GD.Print($"[stalls] {path}");
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
            GD.Print("[stalls] Done.");
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
        game.BuildStage(StageKind.Bandstand, f.X + 74, f.Y + 10, Facing.South);
        game.BuildFence(FenceKind.Paling, new TilePoint(gap.X, gap.Y + 1), new TilePoint(gap.Right - 1, gap.Y + 1));
        game.BuildGate(GateKind.VillageGate, gap.X + 2, gap.Y + 1, false);

        // Stalls down the west hedge facing into the field, toilets and water along the north hedge.
        var tea = new TilePoint(f.X + 7, f.Y + 21);
        var van = new TilePoint(f.X + 7, f.Y + 34);
        var bar = new TilePoint(f.X + 7, f.Y + 52);
        var loos = new TilePoint(f.X + 26, f.Y + 5);
        if (scene.Layout == 0)
        {
            game.BuildStall(StallKind.TeaAndCake, tea.X, tea.Y, Facing.East);
            game.BuildStall(StallKind.FoodVan, van.X, van.Y, Facing.East);
            game.BuildStall(StallKind.Bar, bar.X, bar.Y, Facing.East);
            game.BuildStall(StallKind.Toilets, loos.X, loos.Y, Facing.South);
            game.BuildStall(StallKind.Toilets, loos.X + 10, loos.Y, Facing.South);
            game.BuildStall(StallKind.WaterPoint, loos.X + 22, loos.Y, Facing.South);
        }
        else if (scene.Layout == 1)
        {
            game.BuildStall(StallKind.TeaAndCake, tea.X, tea.Y, Facing.East);
            game.BuildStall(StallKind.Toilets, loos.X, loos.Y, Facing.South);
        }

        var until = game.Now.Date.AddHours(scene.Hour);
        while (game.Now < until)
        {
            game.Clock.Step();
        }

        game.Clock.SetSpeed(GameSpeed.Normal);
        festival.Crowd.Snap();
        festival.SetHudShown(true);
        if (scene.Money)
        {
            festival.ToggleMoney();
        }

        const float t = 0.5f;
        switch (scene.View)
        {
            case 1:
                festival.Camera.LookAt((van.X * t) + 6, (van.Y * t) - 1, 17f);
                break;
            case 2:
                festival.Camera.LookAt((bar.X * t) + 6, (bar.Y * t) + 2, 15f);
                break;
            case 3:
                festival.Camera.LookAt((loos.X * t) + 7, (loos.Y * t) + 6, 17f);
                break;
            case 4:
                festival.Camera.LookAt((f.X * t) + 22, (f.Y * t) + 16, 30f);
                break;
            default:
                festival.Camera.ShowWholeField();
                break;
        }

        festival.Camera.Snap();
        var crowd = game.Crowd;
        GD.Print($"[stalls] {scene.Name}: £{game.Money}, happy {crowd.Happiness}, at stalls {crowd.AtStalls}, on site {crowd.OnSite}, unhappy {crowd.WentHomeUnhappy}, gave up {crowd.GaveUpQueuing}, bubbles {festival.Crowd.BubbleCount}");
    }
}
