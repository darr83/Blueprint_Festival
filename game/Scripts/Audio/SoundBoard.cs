using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace BlueprintFestival.Audio;

/// <summary>
/// Always loaded (an autoload). Plays every sound effect in the game. The list of sounds is
/// res://Audio/sounds.json; each one plays res://Audio/&lt;name&gt;.ogg (or .wav or .mp3) if the file
/// is there, and is silent if it is not, so the game runs the same with or without the files.
/// Background sounds fade up and down; one-off sounds have a shortest gap so a busy bar does not
/// ring the till forty times a second. The acts' music is not played here.
/// </summary>
public partial class SoundBoard : Node
{
    public const string ListPath = "res://Audio/sounds.json";
    public const string SettingsPath = "user://settings.cfg";

    /// <summary>The volume sliders, in the order they are shown: the bus and what the player calls it.</summary>
    public static readonly (string Bus, string Label)[] Sliders =
    [
        ("Master", "Everything"),
        ("Music", "The acts' music"),
        ("Ambience", "Field and weather"),
        ("Crowd", "Crowd"),
        ("Effects", "Stalls, building and radio"),
        ("Interface", "Buttons"),
    ];

    private const int Voices = 12;
    private const float FadePerSecond = 0.6f;

    private sealed record Sound(string Id, string Bus, bool Loop, float Volume, double Gap, AudioStream? Stream);

    private readonly Dictionary<string, Sound> _sounds = [];
    private readonly Dictionary<string, int> _asked = [];
    private readonly Dictionary<string, ulong> _last = [];
    private readonly Dictionary<string, float> _wanted = [];
    private readonly Dictionary<string, float> _level = [];
    private readonly Dictionary<string, AudioStreamPlayer> _loops = [];
    private readonly List<AudioStreamPlayer> _voices = [];
    private readonly RandomNumberGenerator _random = new();

    public static SoundBoard? Instance { get; private set; }

    /// <summary>How many sounds are on the list.</summary>
    public int Listed => _sounds.Count;

    /// <summary>How many of them have a sound file to play.</summary>
    public int WithFiles => _sounds.Values.Count(s => s.Stream is not null);

    /// <summary>The sounds on the list with no file yet.</summary>
    public IEnumerable<string> Missing => _sounds.Values.Where(s => s.Stream is null).Select(s => s.Id);

    /// <summary>Plays a one-off sound from anywhere, if the sound board is loaded.</summary>
    public static void Cue(string id) => Instance?.Play(id);

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        foreach (var (bus, _) in Sliders.Skip(1))
        {
            if (AudioServer.GetBusIndex(bus) < 0)
            {
                AudioServer.AddBus();
                var index = AudioServer.BusCount - 1;
                AudioServer.SetBusName(index, bus);
                AudioServer.SetBusSend(index, "Master");
            }
        }

        LoadList();
        LoadSettings();
        for (var i = 0; i < Voices; i++)
        {
            var voice = new AudioStreamPlayer { Name = $"Voice{i}" };
            AddChild(voice);
            _voices.Add(voice);
        }
    }

    private void LoadList()
    {
        if (!FileAccess.FileExists(ListPath))
        {
            GD.PushWarning($"The list of sounds is missing: {ListPath}");
            return;
        }

        using var file = FileAccess.Open(ListPath, FileAccess.ModeFlags.Read);
        if (Json.ParseString(file.GetAsText()).Obj is not Godot.Collections.Dictionary list
            || list["sounds"].Obj is not Godot.Collections.Array sounds)
        {
            GD.PushWarning("The list of sounds could not be read.");
            return;
        }

        foreach (var entry in sounds)
        {
            var d = entry.AsGodotDictionary();
            var id = d["id"].AsString();
            var loop = d["loop"].AsBool();
            _sounds[id] = new Sound(id, d["bus"].AsString(), loop, (float)d["volume"].AsDouble(), d["gap"].AsDouble(), StreamFor(id, loop));
        }
    }

    private static AudioStream? StreamFor(string id, bool loop)
    {
        foreach (var ending in new[] { "ogg", "wav", "mp3" })
        {
            var path = $"res://Audio/{id}.{ending}";
            if (!ResourceLoader.Exists(path) || ResourceLoader.Load(path) is not AudioStream stream)
            {
                continue;
            }

            switch (stream)
            {
                case AudioStreamOggVorbis ogg:
                    ogg.Loop = loop;
                    break;
                case AudioStreamMP3 mp3:
                    mp3.Loop = loop;
                    break;
                case AudioStreamWav wav:
                    wav.LoopMode = loop ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled;
                    if (loop)
                    {
                        wav.LoopEnd = (int)(wav.GetLength() * wav.MixRate);
                    }

                    break;
            }

            return stream;
        }

        return null;
    }

    /// <summary>Whether a sound of this name is on the list.</summary>
    public bool Knows(string id) => _sounds.ContainsKey(id);

    /// <summary>How many times a sound has been played (or would have been, if its file is not there yet).</summary>
    public int Asked(string id) => _asked.GetValueOrDefault(id);

    /// <summary>How loud a background sound is being asked to be, from 0 to 1.</summary>
    public float Wanted(string id) => _wanted.GetValueOrDefault(id);

    /// <summary>Plays a one-off sound, at a level from 0 to 1. Returns false if it was skipped for coming too soon after the last one.</summary>
    public bool Play(string id, float level = 1f)
    {
        if (!_sounds.TryGetValue(id, out var sound))
        {
            GD.PushWarning($"No sound called {id} is on the list.");
            return false;
        }

        var now = Time.GetTicksMsec();
        if (sound.Gap > 0 && _last.TryGetValue(id, out var last) && now - last < sound.Gap * 1000)
        {
            return false;
        }

        _last[id] = now;
        _asked[id] = Asked(id) + 1;
        if (sound.Stream is null)
        {
            return true;
        }

        var voice = _voices.FirstOrDefault(v => !v.Playing) ?? _voices.MinBy(v => v.Stream is null ? 0 : v.Stream.GetLength() - v.GetPlaybackPosition())!;
        voice.Stream = sound.Stream;
        voice.Bus = sound.Bus;
        voice.VolumeDb = Mathf.LinearToDb(sound.Volume * Mathf.Clamp(level, 0.001f, 1f));
        // A touch of difference each time, so a row of tills does not sound like one till.
        voice.PitchScale = sound.Bus == "Effects" ? _random.RandfRange(0.94f, 1.06f) : 1f;
        voice.Play();
        return true;
    }

    /// <summary>Sets how loud a background sound should be, from 0 to 1. It fades there.</summary>
    public void SetLoop(string id, double level)
    {
        if (_sounds.TryGetValue(id, out var sound) && sound.Loop)
        {
            _wanted[id] = (float)Math.Clamp(level, 0, 1);
        }
    }

    /// <summary>Fades every background sound away, as when going back to the main menu.</summary>
    public void QuietLoops()
    {
        foreach (var id in _wanted.Keys.ToList())
        {
            _wanted[id] = 0;
        }
    }

    public override void _Process(double delta)
    {
        foreach (var (id, wanted) in _wanted)
        {
            var level = Mathf.MoveToward(_level.GetValueOrDefault(id), wanted, FadePerSecond * (float)delta);
            _level[id] = level;
            var sound = _sounds[id];
            if (sound.Stream is null)
            {
                continue;
            }

            if (!_loops.TryGetValue(id, out var player))
            {
                if (level <= 0)
                {
                    continue;
                }

                player = new AudioStreamPlayer { Name = id, Stream = sound.Stream, Bus = sound.Bus };
                AddChild(player);
                _loops[id] = player;
            }

            if (level <= 0.001f)
            {
                player.Stop();
                continue;
            }

            player.VolumeDb = Mathf.LinearToDb(level * sound.Volume);
            if (!player.Playing)
            {
                // Start somewhere different each time so two visits do not open on the same bird.
                player.Play(_random.Randf() * (float)sound.Stream.GetLength());
            }
        }
    }

    /// <summary>A volume slider's setting, from 0 to 1.</summary>
    public static float Volume(string bus)
    {
        var index = AudioServer.GetBusIndex(bus);
        return index < 0 || AudioServer.IsBusMute(index) ? 0 : Mathf.DbToLinear(AudioServer.GetBusVolumeDb(index));
    }

    /// <summary>Moves a volume slider. All the way down is silent.</summary>
    public void SetVolume(string bus, float volume, bool save = true)
    {
        var index = AudioServer.GetBusIndex(bus);
        if (index < 0)
        {
            return;
        }

        volume = Mathf.Clamp(volume, 0, 1);
        AudioServer.SetBusMute(index, volume <= 0.001f);
        AudioServer.SetBusVolumeDb(index, Mathf.LinearToDb(Mathf.Max(volume, 0.001f)));
        if (save)
        {
            SaveSettings();
        }
    }

    private void LoadSettings()
    {
        var settings = new ConfigFile();
        if (settings.Load(SettingsPath) != Error.Ok)
        {
            return;
        }

        foreach (var (bus, _) in Sliders)
        {
            SetVolume(bus, (float)settings.GetValue("sound", bus, 1.0).AsDouble(), save: false);
        }
    }

    private static void SaveSettings()
    {
        var settings = new ConfigFile();
        settings.Load(SettingsPath);
        foreach (var (bus, _) in Sliders)
        {
            settings.SetValue("sound", bus, Volume(bus));
        }

        settings.Save(SettingsPath);
    }
}
