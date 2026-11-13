using System;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Review screenshots, run with: godot --path game -- --shots=DIR (needs a display).
/// Photographs the main menu and the field from each side and at different times of day.
/// </summary>
public partial class Shots : Node
{
    private static readonly (string Name, double Hour, int Quarter, bool Hud, bool CloseUp)[] Scenes =
    {
        ("2_field_morning", 8, 0, true, false),
        ("3_field_turned_east", 13, 1, false, false),
        ("4_field_turned_north", 13, 2, false, false),
        ("5_field_turned_west", 13, 3, false, false),
        ("6_way_in_close_up", 10.5, 0, false, true),
        ("7_field_dusk", 21, 0, true, false),
        ("8_field_night", 23, 0, true, false),
    };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;

    public Shots(string directory)
    {
        _directory = directory;
    }

    public Shots() : this("shots")
    {
    }

    public override void _Ready() => DirAccess.MakeDirRecursiveAbsolute(_directory);

    public override void _Process(double delta)
    {
        _frame++;
        if (_scene < 0)
        {
            if (_frame == 30)
            {
                Shot("1_menu");
                NextScene();
            }

            return;
        }

        if (!_ready)
        {
            if (GetTree().CurrentScene is FestivalScene f && f.IsNodeReady() && f.Game == GameManager.Instance.Game)
            {
                var s = Scenes[_scene];
                f.Game.Clock.SetSpeed(s.Hud ? Simulation.Core.GameSpeed.Normal : Simulation.Core.GameSpeed.Paused);
                f.SetHudShown(s.Hud);
                f.Camera.Turn(s.Quarter);
                if (s.CloseUp)
                {
                    var gap = f.Game.Field.Gap;
                    f.Camera.LookAt((gap.X + 12) * 0.5f, (gap.Y + 14) * 0.5f, 20f);
                }

                f.Camera.Snap();
                _ready = true;
                _frame = 0;
            }

            return;
        }

        if (_frame == 20)
        {
            Shot(Scenes[_scene].Name);
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
            GD.Print("[shots] Done.");
            GetTree().Quit(0);
            return;
        }

        var start = new DateTime(2027, 7, 17).AddHours(Scenes[_scene].Hour);
        GameManager.Instance.StartNewGame(null, start);
    }

    private void Shot(string name)
    {
        var path = $"{_directory}/{name}.png";
        GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"[shots] {path}");
    }
}
