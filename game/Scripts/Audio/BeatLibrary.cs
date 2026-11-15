using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BlueprintFestival.Simulation.Show;
using Godot;

namespace BlueprintFestival.Audio;

/// <summary>
/// Knows the beat of every song, so the crowd can dance in time. A song's beat is read from the
/// ".beat" file beside it. A song with no beat file is listened to once, a little at a time while
/// the game runs (the act on stage first), and its beat file is written beside it in the music
/// folder, or in the player's own save folder if the music folder cannot be written to.
/// </summary>
public partial class BeatLibrary : Node
{
    /// <summary>The most time spent listening to songs in one frame, so the game never stutters.</summary>
    private const double MillisecondsAFrame = 4;

    /// <summary>Frames of sound taken from a song at a time.</summary>
    private const int Chunk = 4096;

    private const string SaveFolder = "user://beats";

    private static readonly ConcurrentDictionary<string, BeatInfo?> Known = new();
    private static readonly HashSet<string> Tried = [];

    private readonly Queue<(string Artist, SongFile Song)> _waiting = new();
    private readonly HashSet<string> _queued = [];
    private Listening? _now;
    private Task? _finding;

    /// <summary>How many songs have had their beat worked out since the game started.</summary>
    public static int WorkedOut { get; private set; }

    /// <summary>How many songs are waiting to be listened to.</summary>
    public int Waiting => _waiting.Count + (_now is null ? 0 : 1) + (_finding is { IsCompleted: false } ? 1 : 0);

    /// <summary>The beat of a song, or null if it is not known yet.</summary>
    public static BeatInfo? Of(string artist, SongFile song)
    {
        var key = Key(artist, song.File);
        if (Known.TryGetValue(key, out var beat))
        {
            return beat;
        }

        // Remember a song with no beat file too, so the folders are not searched every frame.
        beat = ReadSaved(artist, song.File);
        Known[key] = beat;
        return beat;
    }

    /// <summary>Forgets every beat read so far, so beat files are read again (after one is edited, say).</summary>
    public static void Forget()
    {
        Known.Clear();
        lock (Tried)
        {
            Tried.Clear();
        }
    }

    /// <summary>Puts an artist's songs at the front of the queue to be listened to.</summary>
    public void Hurry(string artist)
    {
        foreach (var song in MusicPlayer.SongsOf(artist).Reverse())
        {
            Queue(artist, song, first: true);
        }
    }

    /// <summary>Queues every song by every artist that has no beat yet.</summary>
    public void QueueAll()
    {
        foreach (var artist in Roster.Artists)
        {
            foreach (var song in MusicPlayer.SongsOf(artist.Name))
            {
                Queue(artist.Name, song, first: false);
            }
        }

        foreach (var kind in Busking.Kinds)
        {
            var folder = Busking.MusicFolder(kind);
            foreach (var song in MusicPlayer.SongsOf(folder))
            {
                Queue(folder, song, first: false);
            }
        }
    }

    public override void _Ready() => QueueAll();

    public override void _Process(double delta)
    {
        if (_finding is { IsCompleted: false })
        {
            return;
        }

        var clock = Stopwatch.StartNew();
        while (clock.Elapsed.TotalMilliseconds < MillisecondsAFrame)
        {
            if (_now is null)
            {
                if (_waiting.Count == 0)
                {
                    return;
                }

                var (artist, song) = _waiting.Dequeue();
                _queued.Remove(Key(artist, song.File));
                if (Of(artist, song) is not null)
                {
                    continue;
                }

                _now = Listening.Start(artist, song);
                continue;
            }

            if (!_now.Step())
            {
                continue;
            }

            // The whole song is in: find the beat away from the game, so the game keeps running.
            var done = _now;
            _now = null;
            _finding = Task.Run(() => Finish(done));
            return;
        }
    }

    private void Queue(string artist, SongFile song, bool first)
    {
        var key = Key(artist, song.File);
        if (Of(artist, song) is not null)
        {
            return;
        }

        lock (Tried)
        {
            if (Tried.Contains(key))
            {
                return;
            }
        }

        if (first)
        {
            var rest = _waiting.Where(w => Key(w.Artist, w.Song.File) != key).ToList();
            _waiting.Clear();
            _waiting.Enqueue((artist, song));
            foreach (var w in rest)
            {
                _waiting.Enqueue(w);
            }

            _queued.Add(key);
        }
        else if (_queued.Add(key))
        {
            _waiting.Enqueue((artist, song));
        }
    }

    private static void Finish(Listening song)
    {
        var key = Key(song.Artist, song.Song.File);
        lock (Tried)
        {
            Tried.Add(key);
        }

        if (song.Samples.Count == 0 || song.Rate <= 0)
        {
            return;
        }

        try
        {
            var beat = BeatFinder.Find(song.Samples.ToArray(), song.Rate);
            if (beat is null)
            {
                GD.Print($"No clear beat in {song.Artist}/{song.Song.File}; the crowd will dance to a steady one.");
                return;
            }

            Known[key] = beat;
            WorkedOut++;
            Save(song.Artist, song.Song.File, beat);
        }
        catch (System.Exception e)
        {
            GD.PushWarning($"Could not work out the beat of {song.Artist}/{song.Song.File}: {e.Message}");
        }
    }

    private static string Key(string artist, string file) => $"{artist}/{file}";

    /// <summary>The beat file beside the song, then the copy in the save folder.</summary>
    private static BeatInfo? ReadSaved(string artist, string file)
    {
        foreach (var path in new[] { $"{MusicPlayer.Folder}/{artist}/{BeatInfo.FileFor(file)}", $"{SaveFolder}/{artist}/{BeatInfo.FileFor(file)}" })
        {
            if (FileAccess.FileExists(path) && BeatInfo.Read(FileAccess.GetFileAsString(path)) is { } beat)
            {
                return beat;
            }
        }

        return null;
    }

    private static void Save(string artist, string file, BeatInfo beat)
    {
        var text = beat.Write();

        // Beside the song, when the music folder is a real folder that can be written to (it is when
        // the game is run from Godot); otherwise in the player's own save folder.
        var folder = ProjectSettings.GlobalizePath($"{MusicPlayer.Folder}/{artist}");
        if (!folder.StartsWith("res://", System.StringComparison.Ordinal) && System.IO.Directory.Exists(folder))
        {
            try
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(folder, BeatInfo.FileFor(file)), text);
                return;
            }
            catch (System.Exception e) when (e is System.IO.IOException or System.UnauthorizedAccessException)
            {
                // Fall through to the save folder.
            }
        }

        var saveTo = ProjectSettings.GlobalizePath($"{SaveFolder}/{artist}");
        System.IO.Directory.CreateDirectory(saveTo);
        System.IO.File.WriteAllText(System.IO.Path.Combine(saveTo, BeatInfo.FileFor(file)), text);
    }

    /// <summary>One song being listened to: played silently into memory, a chunk at a time.</summary>
    private sealed class Listening
    {
        private AudioStreamPlayback? _playback;
        private long _framesLeft;
        private bool _halve;
        private float _carry = float.NaN;

        public string Artist { get; private init; } = "";

        public SongFile Song { get; private init; } = null!;

        /// <summary>The song as one channel, at <see cref="Rate"/> samples a second.</summary>
        public List<float> Samples { get; } = [];

        public int Rate { get; private set; }

        public static Listening Start(string artist, SongFile song)
        {
            var listening = new Listening { Artist = artist, Song = song };
            if (ResourceLoader.Load($"{MusicPlayer.Folder}/{artist}/{song.File}") is not AudioStream stream || stream.GetLength() <= 1)
            {
                return listening;
            }

            var rate = AudioServer.GetMixRate();
            listening._playback = stream.InstantiatePlayback();
            listening._playback.Start(0);
            listening._framesLeft = (long)(stream.GetLength() * rate);

            // Above 30,000 a second, every other sample is plenty, and half the memory.
            listening._halve = rate > 30000;
            listening.Rate = (int)(listening._halve ? rate / 2 : rate);
            return listening;
        }

        /// <summary>Takes the next chunk of the song. True once the whole song is in.</summary>
        public bool Step()
        {
            if (_playback is null || _framesLeft <= 0)
            {
                return true;
            }

            var frames = _playback.MixAudio(1f, (int)System.Math.Min(Chunk, _framesLeft));
            if (frames.Length == 0)
            {
                return true;
            }

            _framesLeft -= frames.Length;
            foreach (var frame in frames)
            {
                var mono = (frame.X + frame.Y) * 0.5f;
                if (!_halve)
                {
                    Samples.Add(mono);
                }
                else if (float.IsNaN(_carry))
                {
                    _carry = mono;
                }
                else
                {
                    Samples.Add((_carry + mono) * 0.5f);
                    _carry = float.NaN;
                }
            }

            return _framesLeft <= 0;
        }
    }
}
