using System.Collections.Generic;
using System.Linq;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Sound;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Audio;

/// <summary>
/// Plays the acts' own music. Each artist has a folder under res://Music with their name on it;
/// the sound files in it are their songs, played in the order of the numbers at the start of the
/// file names and round again until the set ends. An artist with no songs plays in silence.
/// Every stage plays its own act at once, and what is heard depends on where the player looks:
/// zoomed right out all of them faintly, closer in the nearest stage over the others, and on a
/// stage camera the stage being filmed (see <see cref="StageMix"/>).
/// </summary>
public partial class MusicPlayer : Node
{
    public const string Folder = "res://Music";
    public const string Bus = "Music";

    private static readonly Dictionary<string, IReadOnlyList<SongFile>> Library = [];

    private FestivalGame _game = null!;
    private readonly List<StageMusic> _stages = [];
    private BeatLibrary _beats = null!;
    private CameraRig? _rig;
    private StageCam? _cam;
    private StageMusic? _heard;

    /// <summary>Each stage's music: the main stage first, then the second stage.</summary>
    public IReadOnlyList<StageMusic> Stages => _stages;

    /// <summary>The music of one stage's running order, or null if it is not one of the game's.</summary>
    public StageMusic? For(RunningOrder show) => _stages.FirstOrDefault(s => s.Show == show);

    /// <summary>The stage whose act is loudest from where the player is looking, or null with nothing on.</summary>
    public RunningOrder? Heard => _heard?.Show;

    /// <summary>True in the few seconds between one song and the next, on the stage heard most.</summary>
    public bool BetweenSongs => _heard?.BetweenSongs ?? false;

    /// <summary>How loud the singing is on the stage heard most, from 0 to 1.</summary>
    public float Voice => _heard?.Voice ?? 0;

    /// <summary>How many songs have ended with the crowd cheering, on every stage. Used by the pictures.</summary>
    public int SongsEnded => _stages.Sum(s => s.SongsEnded);

    /// <summary>The act heard most, or an empty string.</summary>
    public string Artist => _heard?.Artist ?? "";

    /// <summary>The title of the song heard most, or an empty string if nothing is on.</summary>
    public string SongTitle => _heard?.SongTitle ?? "";

    /// <summary>How many songs the act heard most has.</summary>
    public int SongCount => _heard?.SongCount ?? 0;

    /// <summary>The sort of music heard most.</summary>
    public Genre Genre => _heard?.Genre ?? Genre.Pop;

    /// <summary>The beat of the song heard most, or null.</summary>
    public BeatInfo? Beat => _heard?.Beat;

    /// <summary>True while a song is playing out loud on the stage heard most.</summary>
    public bool SongPlaying => _heard?.SongPlaying ?? false;

    /// <summary>How far into the song heard most the listener is, in seconds.</summary>
    public double SongSeconds => _heard?.SongSeconds ?? 0;

    /// <summary>The songs still waiting to have their beat worked out.</summary>
    public int BeatsWaiting => _beats.Waiting;

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
        _beats = new BeatLibrary { Name = "Beats" };
        AddChild(_beats);
        AddStage(() => _game.Show, "Music 1");
        AddStage(() => _game.SecondShow, "Music 2");
    }

    /// <summary>Tells the music where the player is looking from: the site view, and the stage cameras.</summary>
    public void Follow(CameraRig rig, StageCam? cam)
    {
        _rig = rig;
        _cam = cam;
    }

    private void AddStage(System.Func<RunningOrder> show, string bus)
    {
        var stage = new StageMusic { Name = bus.Replace(" ", "") };
        AddChild(stage);
        stage.Initialise(_game, show, bus, _beats);
        _stages.Add(stage);
    }

    public override void _Process(double delta)
    {
        // Where each stage's crowd stands, and how loud each one is from where the player is looking.
        var at = new List<(double X, double Y)>(_stages.Count);
        foreach (var stage in _stages)
        {
            var (x, y, _, _) = stage.Show.Stage is { } placed ? RunningOrder.Front(placed) : (0.0, 0.0, 0, 0);
            at.Add((x, y));
        }

        double[] levels;
        var filmed = _cam?.FilmingStage is { } film ? _stages.FindIndex(s => s.Show.Stage == film) : -1;
        if (_cam?.Showing is not null)
        {
            levels = StageMix.Filming(filmed, _stages.Count);
        }
        else if (_rig is not null)
        {
            levels = StageMix.Levels(_rig.Target.X, _rig.Target.Z, _rig.Size, at);
        }
        else
        {
            levels = _stages.Select(_ => 1.0).ToArray();
        }

        for (var i = 0; i < _stages.Count; i++)
        {
            _stages[i].Wanted = (float)levels[i];
        }

        // The stage heard most is the loudest of those with an act on.
        StageMusic? heard = null;
        foreach (var stage in _stages)
        {
            if (stage.On && stage.Show.Stage is not null && (heard is null || stage.Wanted > heard.Wanted))
            {
                heard = stage;
            }
        }

        _heard = heard;
    }

    /// <summary>Jumps to a point in the song heard most, in seconds. Used for pictures.</summary>
    public void SkipTo(double seconds) => _heard?.SkipTo(seconds);

    /// <summary>Jumps to a little before the end of the song heard most, so the end of a song can be filmed.</summary>
    public void SkipToEnd(double secondsBefore) => _heard?.SkipToEnd(secondsBefore);
}
