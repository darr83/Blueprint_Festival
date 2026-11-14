using BlueprintFestival.Audio;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Show;
using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// What is going on at one stage, worked out once a frame: whether a song is playing, the stage
/// is between songs or the act is taking its applause at the end; and the beat. The song's own beat
/// is used when its music is the music being heard, otherwise a steady beat suited to the act,
/// counted from when it came on. The crowd dances to this, and the band plays to it.
/// </summary>
public sealed class StageBeat
{
    private int _slot = -1;
    private double _clock;
    private double _frontX;
    private double _frontY;

    /// <summary>True while an act is on at this stage and a song is playing: the time to dance.</summary>
    public bool On => Moment == StageMoment.Song;

    /// <summary>What is happening on the stage.</summary>
    public StageMoment Moment { get; private set; }

    /// <summary>The act on stage (playing, or taking its applause), or null.</summary>
    public Slot? Slot { get; private set; }

    public Genre Genre { get; private set; }

    /// <summary>How many beats into the song (or set) it is.</summary>
    public double Beat { get; private set; }

    /// <summary>True when this stage's music is the music being heard, with a real song playing.</summary>
    public bool Heard { get; private set; }

    /// <summary>How loud the singing is right now, from 0 to 1, for the singer's mouth.</summary>
    public float Voice { get; private set; }

    /// <summary>How long the stage has been in its moment, in real seconds.</summary>
    public double InMoment { get; private set; }

    public void Update(FestivalGame game, RunningOrder show, MusicPlayer? music, bool held, double delta)
    {
        var day = game.Phase == Phase.Day;
        var playing = day ? show.Playing(game.Now) : null;
        var bowing = day && playing is null ? show.Bowing(game.Now) : null;
        Slot = playing ?? bowing;
        var moment = StageMoment.Quiet;
        if (show.Stage is not { } stage || Slot is null || game.ActPaused)
        {
            _slot = -1;
            Heard = false;
            Voice = 0;
            Set(StageMoment.Quiet, delta);
            Slot = game.ActPaused ? null : Slot;
            return;
        }

        if (playing is not null && playing.Index != _slot)
        {
            _slot = playing.Index;
            _clock = 0;
        }

        (_frontX, _frontY, _, _) = RunningOrder.Front(stage);
        Genre = Slot.Act.Genre;
        if (!held)
        {
            _clock += delta;
        }

        var heard = music is not null && music.Heard == show && music.SongCount > 0;
        Heard = heard && music!.SongPlaying;
        if (bowing is not null)
        {
            moment = StageMoment.Applause;
        }
        else if (heard && !music!.SongPlaying)
        {
            // Between two songs, or once the last song of the set has faded away.
            moment = StageMoment.BetweenSongs;
        }
        else
        {
            moment = StageMoment.Song;
        }

        Set(moment, held ? 0 : delta);
        Beat = Heard
            ? Dancing.BeatNow(music!.Beat, music.SongSeconds, Genre, music.SongSeconds)
            : Dancing.BeatNow(null, 0, Genre, _clock);
        Voice = Heard ? music!.Voice : 0;
    }

    public double DistanceSquared(double x, double y) => ((x - _frontX) * (x - _frontX)) + ((y - _frontY) * (y - _frontY));

    private void Set(StageMoment moment, double delta)
    {
        InMoment = moment == Moment ? InMoment + delta : 0;
        Moment = moment;
    }
}
