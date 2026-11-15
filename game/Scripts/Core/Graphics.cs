using System;
using BlueprintFestival.Audio;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>The picture settings the player can change. They are remembered between games, with the sound.</summary>
public static class Graphics
{
    private static bool? _shadows;

    /// <summary>Raised when a setting is changed, so whatever is on screen can follow it.</summary>
    public static event Action? Changed;

    /// <summary>Whether things cast shadows. Off is quicker to draw on a slower computer.</summary>
    public static bool Shadows
    {
        get => _shadows ??= Load();
        set
        {
            if (Shadows == value)
            {
                return;
            }

            _shadows = value;
            var settings = new ConfigFile();
            settings.Load(SoundBoard.SettingsPath);
            settings.SetValue("picture", "shadows", value);
            settings.Save(SoundBoard.SettingsPath);
            Changed?.Invoke();
        }
    }

    private static bool Load()
    {
        var wanted = OS.GetEnvironment("SHADOWS");
        if (wanted.Length > 0)
        {
            return wanted != "off";
        }

        var settings = new ConfigFile();
        return settings.Load(SoundBoard.SettingsPath) != Error.Ok || settings.GetValue("picture", "shadows", true).AsBool();
    }
}
