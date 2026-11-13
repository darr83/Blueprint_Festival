using System;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Review screenshots, run with: godot --path game -- --shots=DIR (needs a display).
/// Photographs the main menu and the festival at different times of day and from different sides.
/// </summary>
public partial class Shots : Node
{
    private static readonly (string Name, double Hour, int Quarter, bool Hud, bool CloseUp)[] Scenes =
    {
        ("2_first_arrivals", 8.6, 0, true, true),
        ("3_field_mid_morning", 9.75, 0, true, false),
        ("4_field_afternoon", 14, 0, true, false),
        ("5_afternoon_closer", 14, 1, false, true),
        ("6_field_turned_north", 14, 2, false, false),
        ("7_going_home_at_dusk", 21.2, 0, true, false),
        ("8_empty_at_night", 23.75, 0, true, false),
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
                // Play the day forward to the time of the picture.
                var until = f.Game.Now.Date.AddHours(s.Hour);
                while (f.Game.Now < until)
                {
                    f.Game.Clock.Step();
                }

                f.Crowd.Snap();
                if (s.CloseUp && s.Hour < 9)
                {
                    var gap = f.Game.Field.Gap;
                    f.Camera.LookAt((gap.X + 4) * 0.5f, (gap.Y + 12) * 0.5f, 24f);
                }
                else if (s.CloseUp)
                {
                    var party = System.Linq.Enumerable.First(f.Game.Crowd.Parties, p => p.IsOnSite && p.Members.Count >= 3);
                    f.Camera.LookAt((float)party.X, (float)party.Y, 16f);
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

        GameManager.Instance.StartNewGame(null);
    }

    private void Shot(string name)
    {
        var path = $"{_directory}/{name}.png";
        GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"[shots] {path}");
    }
}
