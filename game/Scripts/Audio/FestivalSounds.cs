using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Sound;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Audio;

/// <summary>
/// Sits in the festival scene and passes what the festival should sound like on to the sound
/// board: the field, the weather and the crowd in the background, and each cheer, till and
/// radio call as it happens.
/// </summary>
public partial class FestivalSounds : Node
{
    private const double Every = 0.25;

    private SoundScript _script = null!;
    private double _wait;
    private CameraRig? _rig;
    private StageCam? _cam;

    public void Initialise(FestivalGame game)
    {
        _script = new SoundScript(game);
        _script.Heard += Play;
        if (game.Phase == Phase.Plan)
        {
            // The year opens on the planning notebook.
            SoundBoard.Cue(Sounds.Notebook);
        }

        Pass();
    }

    /// <summary>Listens from where the player is looking, so the fairground, and the stalls are only heard near where the player is looking.</summary>
    public void Follow(CameraRig rig, StageCam? cam)
    {
        _rig = rig;
        _cam = cam;
        Look();
    }

    public override void _Process(double delta)
    {
        _wait -= delta;
        if (_wait <= 0)
        {
            Look();
        }
    }

    /// <summary>Looks at the festival now, without waiting for the next quarter second.</summary>
    public void Look()
    {
        _wait = Every;
        if (_rig is not null)
        {
            _script.Listen(_rig.Target.X, _rig.Target.Z, _rig.Size, _cam?.Showing is not null);
        }

        _script.Poll();
        Pass();
    }

    /// <summary>Plays a one-off sound at how loud it is from where the player is looking.</summary>
    private static void Play(string id, double level) => SoundBoard.Instance?.Play(id, (float)level);

    private void Pass()
    {
        if (SoundBoard.Instance is { } board)
        {
            foreach (var (id, level) in _script.Levels)
            {
                board.SetLoop(id, level);
            }
        }
    }

    public override void _ExitTree()
    {
        _script.Heard -= Play;
        _script.Dispose();
        SoundBoard.Instance?.QuietLoops();
    }
}
