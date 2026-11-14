using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// A picture of the volume sliders in the pause menu, run with:
/// godot --path game -- --sound-shots=DIR (needs a display).
/// </summary>
public partial class SoundShots : Node
{
    private readonly string _directory;
    private int _frame = -1;

    public SoundShots(string directory)
    {
        _directory = directory;
    }

    public SoundShots() : this("shots")
    {
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        DirAccess.MakeDirRecursiveAbsolute(_directory);
        Callable.From(() => GameManager.Instance.StartNewFestival(null)).CallDeferred();
    }

    public override void _Process(double delta)
    {
        if (_frame < 0)
        {
            if (GetTree().CurrentScene is FestivalScene f && f.IsNodeReady() && f.Game == GameManager.Instance.Game)
            {
                f.Year.StartBuilding();
                f.Camera.Snap();
                f.Sky.Apply(f.Game, f.Camera.Yaw, settle: true);
                f.OpenMenu();
                _frame = 0;
            }

            return;
        }

        if (++_frame == 30)
        {
            var path = $"{_directory}/1_volume_sliders_in_the_pause_menu.png";
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"[sound] {path}");
            GD.Print("[sound] Done.");
            GetTree().Paused = false;
            GetTree().Quit(0);
        }
    }
}
