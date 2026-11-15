using BlueprintFestival.Audio;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Show;
using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// What is going on at one stage, worked out once a frame: whether a song is playing, the stage
/// is between songs or the act is taking its applause at the end; and the beat. The song's own beat
/// is used when the act has songs to play (every stage plays its own at once), otherwise a steady
/// beat suited to the act, counted from when it came on. The crowd dances to this, and the band plays to it.
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

    /// <summary>True when this stage's own music is playing, a real song.</summary>
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

        var mine = music?.For(show);
        var heard = mine is not null && mine.On && mine.SongCount > 0;
        Heard = heard && mine!.SongPlaying;
        if (bowing is not null)
        {
            moment = StageMoment.Applause;
        }
        else if (heard && !mine!.SongPlaying)
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
            ? Dancing.BeatNow(mine!.Beat, mine.SongSeconds, Genre, mine.SongSeconds)
            : Dancing.BeatNow(null, 0, Genre, _clock);
        Voice = Heard ? mine!.Voice : 0;
    }

    /// <summary>
    /// The beat for one of the entertainers in the fields rather than a stage: playing or not, their
    /// own song and its beat if one is on, otherwise a steady beat for their sort of music. A few
    /// seconds of applause follow the end of each set.
    /// </summary>
    public void Busk(bool playing, Genre genre, BeatInfo? beat, double songSeconds, bool songPlaying, bool betweenSongs, bool held, double delta)
    {
        Genre = genre;
        Slot = null;
        Voice = 0;
        if (!held)
        {
            _clock += delta;
        }

        StageMoment moment;
        if (playing)
        {
            moment = betweenSongs ? StageMoment.BetweenSongs : StageMoment.Song;
            _slot = 1;
        }
        else if (_slot == 1 || (Moment == StageMoment.Applause && InMoment < 4))
        {
            // The set has just ended: a bow.
            moment = StageMoment.Applause;
            _slot = -1;
        }
        else
        {
            moment = StageMoment.Quiet;
        }

        Set(moment, held ? 0 : delta);
        Heard = songPlaying;
        Beat = songPlaying ? Dancing.BeatNow(beat, songSeconds, genre, songSeconds) : Dancing.BeatNow(null, 0, genre, _clock);
    }

    public double DistanceSquared(double x, double y) => ((x - _frontX) * (x - _frontX)) + ((y - _frontY) * (y - _frontY));

    private void Set(StageMoment moment, double delta)
    {
        InMoment = moment == Moment ? InMoment + delta : 0;
        Moment = moment;
    }
}
