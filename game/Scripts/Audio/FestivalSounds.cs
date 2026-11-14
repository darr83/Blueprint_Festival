using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Sound;
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

    public void Initialise(FestivalGame game)
    {
        _script = new SoundScript(game);
        _script.Cue += SoundBoard.Cue;
        if (game.Phase == Phase.Plan)
        {
            // The year opens on the planning notebook.
            SoundBoard.Cue(Sounds.Notebook);
        }

        Pass();
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
        _script.Poll();
        Pass();
    }

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
        _script.Cue -= SoundBoard.Cue;
        _script.Dispose();
        SoundBoard.Instance?.QuietLoops();
    }
}
