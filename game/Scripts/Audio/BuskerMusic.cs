using System.Collections.Generic;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Sound;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Audio;

/// <summary>
/// The entertainers' music. Each entertainer who is playing a set plays the songs in their own
/// folder (Music/Entertainers/Guitarist and so on) in order and round again, with a few seconds
/// between songs, through the Music bus. They are only heard close to: the level follows where the
/// player is looking (see <see cref="StageMix.Busker"/>), and nothing is heard on a stage camera.
/// </summary>
public partial class BuskerMusic : Node
{
    private const float FadePerSecond = 1.5f;

    private readonly Dictionary<int, Busker> _buskers = [];
    private readonly HashSet<int> _seen = [];
    private readonly List<int> _gone = [];
    private FestivalGame _game = null!;
    private BeatLibrary _beats = null!;
    private CameraRig? _rig;
    private StageCam? _cam;

    /// <summary>What one entertainer is playing, as heard.</summary>
    public sealed class Busker
    {
        public required AudioStreamPlayer Player { get; init; }

        public required string Folder { get; init; }

        public IReadOnlyList<SongFile> Songs { get; set; } = [];

        public int Song { get; set; } = -1;

        public bool On { get; set; }

        public double GapLeft { get; set; }

        public float Level { get; set; }

        /// <summary>The beat of the song that is on, or null.</summary>
        public BeatInfo? Beat => Song >= 0 && Song < Songs.Count ? BeatLibrary.Of(Folder, Songs[Song]) : null;

        /// <summary>True while a song is playing out loud.</summary>
        public bool SongPlaying => On && Player.Playing;

        public bool BetweenSongs => On && GapLeft > 0;

        public double SongSeconds => !Player.Playing ? 0
            : Player.GetPlaybackPosition() + (Player.StreamPaused ? 0 : AudioServer.GetTimeSinceLastMix()) - AudioServer.GetOutputLatency();
    }

    public void Initialise(FestivalGame game, BeatLibrary beats)
    {
        _game = game;
        _beats = beats;
    }

    public void Follow(CameraRig rig, StageCam? cam)
    {
        _rig = rig;
        _cam = cam;
    }

    /// <summary>An entertainer's music, or null if they have none going.</summary>
    public Busker? For(int staff) => _buskers.GetValueOrDefault(staff);

    /// <summary>How many entertainers are playing a song out loud just now. Used by the pictures.</summary>
    public int Playing
    {
        get
        {
            var count = 0;
            foreach (var b in _buskers.Values)
            {
                count += b.SongPlaying ? 1 : 0;
            }

            return count;
        }
    }

    public override void _Process(double delta)
    {
        if (_game is null)
        {
            return;
        }

        var held = _game.Clock.Speed == GameSpeed.Paused;
        _seen.Clear();
        foreach (var member in _game.Staff)
        {
            if (!Busking.IsEntertainer(member.Kind))
            {
                continue;
            }

            _seen.Add(member.Id);
            if (!_buskers.TryGetValue(member.Id, out var busker))
            {
                var player = new AudioStreamPlayer { Name = $"Busker{member.Id}", Bus = AudioServer.GetBusIndex(MusicPlayer.Bus) >= 0 ? MusicPlayer.Bus : "Master", VolumeDb = -80 };
                AddChild(player);
                busker = new Busker { Player = player, Folder = Busking.MusicFolder(member.Kind) };
                var me = busker;
                player.Finished += () => SongOver(me);
                _buskers[member.Id] = busker;
            }

            var on = _game.IsBusking(member);
            if (on != busker.On)
            {
                busker.On = on;
                busker.GapLeft = 0;
                if (on)
                {
                    busker.Songs = MusicPlayer.SongsOf(busker.Folder);
                    _beats.Hurry(busker.Folder);
                    Next(busker);
                }
            }

            busker.Player.StreamPaused = held && busker.Player.Playing;
            if (busker.GapLeft > 0 && !held)
            {
                busker.GapLeft -= delta;
                if (busker.GapLeft <= 0)
                {
                    busker.GapLeft = 0;
                    Next(busker);
                }
            }

            var place = _cam?.Showing is not null ? 0
                : _rig is null ? 1
                : StageMix.Busker(_rig.Target.X, _rig.Target.Z, _rig.Size, member.PostX, member.PostY);
            busker.Level = Mathf.MoveToward(busker.Level, on && !held ? 1 : 0, FadePerSecond * (float)delta);
            busker.Player.VolumeDb = Mathf.LinearToDb(Mathf.Max(busker.Level * (float)place, 0.0001f));
            if (!on && busker.Level <= 0 && busker.Player.Playing)
            {
                busker.Player.Stop();
            }
        }

        if (_seen.Count != _buskers.Count)
        {
            _gone.Clear();
            foreach (var id in _buskers.Keys)
            {
                if (!_seen.Contains(id))
                {
                    _gone.Add(id);
                }
            }

            foreach (var id in _gone)
            {
                _buskers[id].Player.QueueFree();
                _buskers.Remove(id);
            }
        }
    }

    private void SongOver(Busker busker)
    {
        if (busker.On && busker.Songs.Count > 0)
        {
            busker.GapLeft = Performing.SongGapSeconds;
        }
    }

    private void Next(Busker busker)
    {
        if (!busker.On || busker.Songs.Count == 0)
        {
            return;
        }

        busker.Song = (busker.Song + 1) % busker.Songs.Count;
        if (ResourceLoader.Load($"{MusicPlayer.Folder}/{busker.Folder}/{busker.Songs[busker.Song].File}") is AudioStream stream)
        {
            busker.Player.Stream = stream;
            busker.Player.Play();
        }
    }
}
