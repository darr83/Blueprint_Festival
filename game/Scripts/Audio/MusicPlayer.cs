using System.Collections.Generic;
using System.Linq;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Show;
using Godot;

namespace BlueprintFestival.Audio;

/// <summary>
/// Plays the acts' own music. Each artist has a folder under res://Music with their name on it;
/// the sound files in it are their songs, played in the order of the numbers at the start of the
/// file names and round again until the set ends. An artist with no songs plays in silence.
/// The main stage is heard; the second stage is heard while the main stage is between sets.
/// </summary>
public partial class MusicPlayer : Node
{
    public const string Folder = "res://Music";
    public const string Bus = "Music";
    private const float FadePerSecond = 1.2f;

    private static readonly Dictionary<string, IReadOnlyList<SongFile>> Library = [];

    private FestivalGame _game = null!;
    private AudioStreamPlayer _player = null!;
    private IReadOnlyList<SongFile> _songs = [];
    private int _slot = -1;
    private int _song;
    private float _level;

    /// <summary>The act whose music is on, or an empty string.</summary>
    public string Artist { get; private set; } = "";

    /// <summary>The title of the song that is on, or an empty string if nothing is.</summary>
    public string SongTitle { get; private set; } = "";

    /// <summary>How many songs the act that is on has.</summary>
    public int SongCount => _songs.Count;

    /// <summary>An artist's songs in playing order, read from their folder.</summary>
    public static IReadOnlyList<SongFile> SongsOf(string artist)
    {
        if (!Library.TryGetValue(artist, out var songs))
        {
            var path = $"{Folder}/{artist}";
            // A built game lists "song.mp3.import" in place of the file itself.
            var files = DirAccess.DirExistsAbsolute(path)
                ? DirAccess.GetFilesAt(path).Select(f => f.EndsWith(".import", System.StringComparison.Ordinal) ? f[..^7] : f).Distinct()
                : [];
            songs = SongFile.InOrder(files);
            Library[artist] = songs;
        }

        return songs;
    }

    /// <summary>How many of the artists have at least one song, and how many songs there are in all.</summary>
    public static (int Artists, int Songs) Supplied()
    {
        var counts = Roster.Artists.Select(a => SongsOf(a.Name).Count).ToList();
        return (counts.Count(c => c > 0), counts.Sum());
    }

    public void Initialise(FestivalGame game)
    {
        _game = game;
        _player = new AudioStreamPlayer { Name = "Song", Bus = AudioServer.GetBusIndex(Bus) >= 0 ? Bus : "Master", VolumeDb = -80 };
        AddChild(_player);
        _player.Finished += NextSong;
    }

    public override void _Process(double delta)
    {
        var now = _game.Now;
        var on = _game.Phase == Phase.Day ? _game.Show.Playing(now) ?? _game.SecondShow.Playing(now) : null;
        var index = on?.Index ?? -1;
        if (index != _slot)
        {
            _slot = index;
            Artist = on?.Act.Name ?? "";
            _songs = on is null ? [] : SongsOf(on.Act.MusicFolder);
            _song = -1;
            if (on is null)
            {
                SongTitle = "";
            }
            else
            {
                _level = 0;
                NextSong();
            }
        }

        // The music stops while the act is stopped for the crowd's safety, and while the game is paused.
        var held = _game.ActPaused || _game.Clock.Speed == GameSpeed.Paused;
        _player.StreamPaused = held && _slot >= 0;
        _level = Mathf.MoveToward(_level, _slot >= 0 && !held ? 1 : 0, FadePerSecond * (float)delta);
        _player.VolumeDb = Mathf.LinearToDb(Mathf.Max(_level, 0.0001f));
        if (_slot < 0 && _level <= 0 && _player.Playing)
        {
            _player.Stop();
        }
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
        if (ResourceLoader.Load($"{Folder}/{Artist}/{song.File}") is AudioStream stream)
        {
            _player.Stream = stream;
            _player.Play();
        }
    }
}
