using BlueprintFestival.Audio;
using BlueprintFestival.Simulation.Show;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Works out the beat of every song in the music folders and then quits:
/// <c>godot --path game -- --work-out-beats</c>. The game does this by itself while it runs; this is
/// for doing them all in one go, and prints what it found.
/// </summary>
public partial class BeatScan : Node
{
    private BeatLibrary _library = null!;
    private int _frames;

    public override void _Ready()
    {
        _library = new BeatLibrary { Name = "Beats" };
        AddChild(_library);
    }

    public override void _Process(double delta)
    {
        // Give the library a frame to queue the songs before checking whether it is done.
        if (++_frames < 3 || _library.Waiting > 0)
        {
            return;
        }

        foreach (var artist in Roster.Artists)
        {
            foreach (var song in MusicPlayer.SongsOf(artist.Name))
            {
                var beat = BeatLibrary.Of(artist.Name, song);
                GD.Print(beat is null
                    ? $"{artist.Name} / {song.File}: no clear beat"
                    : $"{artist.Name} / {song.File}: {beat.Bpm:0.0} beats a minute, first beat at {beat.FirstBeat:0.00}s, {beat.Beats.Count} beats");
            }
        }

        GD.Print($"Beats worked out: {BeatLibrary.WorkedOut}");
        GetTree().Quit();
    }
}
