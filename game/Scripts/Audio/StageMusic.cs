using System.Collections.Generic;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Sound;
using Godot;

namespace BlueprintFestival.Audio;

/// <summary>
/// One stage's music: the act's songs played in order and round again until the set ends, a few
/// seconds between songs while the crowd claps and cheers, and the last song fading away as the
/// set comes to its end. Each stage plays through its own bus, which feeds the Music bus, so the
/// singing can be listened for on each stage by itself. How loud it is depends on where the player
/// is looking, and is set by the <see cref="MusicPlayer"/> every frame.
/// </summary>
public partial class StageMusic : Node
{
    private const float FadePerSecond = 1.2f;

    /// <summary>How quickly the stage's level follows the camera, a share of the way a second.</summary>
    private const float PlaceFollow = 3f;

    private FestivalGame _game = null!;
    private System.Func<RunningOrder> _show = null!;
    private AudioStreamPlayer _player = null!;
    private IReadOnlyList<SongFile> _songs = [];
    private int _slot = -1;
    private int _song;
    private float _level;
    private BeatLibrary _beats = null!;
    private double _gapLeft;
    private float _fade = 1;
    private int _bus = -1;
    private int _analyser = -1;
    private float _voicePeak = 0.02f;
    private float _place = -1;

    /// <summary>The running order this stage plays.</summary>
    public RunningOrder Show => _show();

    /// <summary>True while an act is on at this stage.</summary>
    public bool On => _slot >= 0;

    /// <summary>True in the few seconds between one song and the next.</summary>
    public bool BetweenSongs => _slot >= 0 && _gapLeft > 0;

    /// <summary>How loud the singing (the middle of the sound) is right now, from 0 to 1, for the singer's mouth.</summary>
    public float Voice { get; private set; }

    /// <summary>How many songs have ended with the crowd cheering. Used by the pictures.</summary>
    public int SongsEnded { get; private set; }

    /// <summary>The act whose music is on, or an empty string.</summary>
    public string Artist { get; private set; } = "";

    /// <summary>The title of the song that is on, or an empty string if nothing is.</summary>
    public string SongTitle { get; private set; } = "";

    /// <summary>How many songs the act that is on has.</summary>
    public int SongCount => _songs.Count;

    /// <summary>The sort of music the act plays.</summary>
    public Genre Genre { get; private set; }

    /// <summary>How loud this stage is from where the player is looking, from 0 to 1 (eased, not jumping).</summary>
    public float Place => Mathf.Max(_place, 0);

    /// <summary>How loud this stage should be from where the player is looking. Set every frame.</summary>
    public float Wanted { get; set; } = 1;

    /// <summary>The beat of the song that is on, or null if it has none yet (or nothing is on).</summary>
    public BeatInfo? Beat => _song >= 0 && _song < _songs.Count ? BeatLibrary.Of(Artist, _songs[_song]) : null;

    /// <summary>True while a song is playing out loud.</summary>
    public bool SongPlaying => _slot >= 0 && _player.Playing;

    /// <summary>
    /// How far into the song that is on the listener is, in seconds: where the player has got to,
    /// plus the sound mixed since, less the time it takes to come out of the speakers.
    /// </summary>
    public double SongSeconds => !_player.Playing ? 0
        : _player.GetPlaybackPosition() + (_player.StreamPaused ? 0 : AudioServer.GetTimeSinceLastMix()) - AudioServer.GetOutputLatency();

    public void Initialise(FestivalGame game, System.Func<RunningOrder> show, string bus, BeatLibrary beats)
    {
        _game = game;
        _show = show;
        _beats = beats;
        _bus = BusFor(bus);
        _player = new AudioStreamPlayer { Name = "Song", Bus = _bus >= 0 ? bus : "Master", VolumeDb = -80 };
        AddChild(_player);
        _player.Finished += SongOver;
        if (_bus >= 0)
        {
            // Listens to the music for the singing, so the singer's mouth moves with it.
            for (var i = 0; i < AudioServer.GetBusEffectCount(_bus); i++)
            {
                if (AudioServer.GetBusEffect(_bus, i) is AudioEffectSpectrumAnalyzer)
                {
                    _analyser = i;
                }
            }

            if (_analyser < 0)
            {
                AudioServer.AddBusEffect(_bus, new AudioEffectSpectrumAnalyzer { BufferLength = 0.1f, FftSize = AudioEffectSpectrumAnalyzer.FftSizeEnum.Size1024 });
                _analyser = AudioServer.GetBusEffectCount(_bus) - 1;
            }
        }
    }

    /// <summary>A stage's own bus, made if it is not there yet, feeding the Music bus (so the music slider turns every stage up or down).</summary>
    private static int BusFor(string bus)
    {
        var index = AudioServer.GetBusIndex(bus);
        if (index >= 0)
        {
            return index;
        }

        if (AudioServer.GetBusIndex(MusicPlayer.Bus) < 0)
        {
            return -1;
        }

        AudioServer.AddBus();
        index = AudioServer.BusCount - 1;
        AudioServer.SetBusName(index, bus);
        AudioServer.SetBusSend(index, MusicPlayer.Bus);
        return index;
    }

    public override void _Process(double delta)
    {
        var now = _game.Now;
        var show = Show;
        var on = _game.Phase == Phase.Day ? show.Playing(now) : null;
        var index = on?.Index ?? -1;
        if (index != _slot)
        {
            _slot = index;
            Artist = on?.Act.Name ?? "";
            Genre = on?.Act.Genre ?? Genre.Pop;
            _songs = on is null ? [] : MusicPlayer.SongsOf(on.Act.MusicFolder);
            _song = -1;
            _gapLeft = 0;
            _fade = 1;
            if (on is null)
            {
                SongTitle = "";
            }
            else
            {
                // Work out the beat of this act's songs first, if it is not known yet.
                _beats.Hurry(on.Act.MusicFolder);
                _level = 0;
                NextSong();
            }
        }

        // The music stops while the act is stopped for the crowd's safety, and while the game is paused.
        var held = _game.ActPaused || _game.Clock.Speed == GameSpeed.Paused;
        _player.StreamPaused = held && _slot >= 0;
        if (_gapLeft > 0 && !held)
        {
            _gapLeft -= delta;
            if (_gapLeft <= 0)
            {
                _gapLeft = 0;
                // No new song in the last moments of a set: the act thanks the crowd instead.
                if (on is not null && Performing.RealSeconds(on.End - now, _game.Clock.Speed) > Performing.FadeOutSeconds + 4)
                {
                    NextSong();
                }
                else
                {
                    _gapLeft = 0.0001;
                }
            }
        }

        // The last song fades away as the set comes to an end.
        var left = on is null ? 0 : Performing.RealSeconds(on.End - now, _game.Clock.Speed);
        var fade = on is null ? _fade : (float)Performing.FadeLevel(left);
        _fade = held ? _fade : fade;
        if (on is not null && _fade <= 0.001f && _player.Playing)
        {
            _player.Stop();
            _gapLeft = 0.0001;
        }

        // Where the player is looking: the first frame takes it as it is, then it eases along.
        _place = _place < 0 ? Wanted : Mathf.Lerp(_place, Wanted, Mathf.Min(1f, PlaceFollow * (float)delta));
        _level = Mathf.MoveToward(_level, _slot >= 0 && !held ? 1 : 0, FadePerSecond * (float)delta);
        _player.VolumeDb = Mathf.LinearToDb(Mathf.Max(_level * _fade * Place, 0.0001f));
        Listen(delta);
        if (_slot < 0 && _level <= 0 && _player.Playing)
        {
            _player.Stop();
        }
    }

    /// <summary>Jumps to a point in the song that is on, in seconds. Used for pictures.</summary>
    public void SkipTo(double seconds)
    {
        if (_player.Playing)
        {
            _player.Seek((float)seconds);
        }
    }

    /// <summary>Jumps to a little before the end of the song that is on, so the end of a song can be filmed.</summary>
    public void SkipToEnd(double secondsBefore)
    {
        if (_player.Playing && _player.Stream is { } stream)
        {
            _player.Seek((float)System.Math.Max(0, stream.GetLength() - secondsBefore));
        }
    }

    /// <summary>A song has played to its end: a few seconds for the crowd to cheer, then the next.</summary>
    private void SongOver()
    {
        if (_slot < 0 || _songs.Count == 0)
        {
            SongTitle = "";
            return;
        }

        _gapLeft = Performing.SongGapSeconds;
        SongsEnded++;

        // The cheer is as near or far as the stage's music.
        if (Place > 0.12f)
        {
            SoundBoard.Instance?.Play(Sounds.SongEndFor(_game.Crowd.Watching), Mathf.Clamp(0.45f + (_game.Crowd.Watching / 400f), 0.45f, 1f) * Mathf.Min(1f, Place * 1.4f));
        }
    }

    /// <summary>How loud the middle of the sound is, where voices are, against the loudest it has been lately.</summary>
    private void Listen(double delta)
    {
        var playing = SongPlaying && !_player.StreamPaused;
        var level = 0f;
        if (playing && _bus >= 0 && _analyser >= 0 && AudioServer.GetBusEffectInstance(_bus, _analyser) is AudioEffectSpectrumAnalyzerInstance analyser)
        {
            var voice = analyser.GetMagnitudeForFrequencyRange(300, 3000).Length();
            var low = analyser.GetMagnitudeForFrequencyRange(30, 250).Length();
            // Voices sit in the middle; take away some of the drums and bass so the mouth is not just keeping time.
            var mid = Mathf.Max(0, voice - (0.15f * low));
            _voicePeak = Mathf.Max(Mathf.Max(mid, 0.0005f), _voicePeak * Mathf.Pow(0.6f, (float)delta));
            level = Mathf.Clamp(((mid / _voicePeak) - 0.25f) / 0.6f, 0, 1);
        }

        Voice = Mathf.MoveToward(Voice, level, (float)delta * 14f);
    }

    private void NextSong()
    {
        if (_slot < 0 || _songs.Count == 0)
        {
            SongTitle = "";
            return;
        }

        _song = (_song + 1) % _songs.Count;
        var song = _songs[_song];
        SongTitle = song.Title;
        if (ResourceLoader.Load($"{MusicPlayer.Folder}/{Artist}/{song.File}") is AudioStream stream)
        {
            _player.Stream = stream;
            _player.Play();
        }
    }
}
