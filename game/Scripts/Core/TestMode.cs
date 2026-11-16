using BlueprintFestival.Audio;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// The cheat for trying the game at any size. F9 on the planning screen turns it on or off, and
/// the choice is remembered in the settings file. While it is on, the January field screen shows
/// minus and plus buttons that move the licence down or up the ladder and top up the bank.
/// </summary>
public static class TestMode
{
    private const string Section = "test";
    private const string Key = "on";

    private static bool? _on;

    /// <summary>True while the size buttons show.</summary>
    public static bool On
    {
        get
        {
            if (_on is null)
            {
                var settings = new ConfigFile();
                _on = settings.Load(SoundBoard.SettingsPath) == Error.Ok && settings.GetValue(Section, Key, false).AsBool();
            }

            return _on.Value;
        }
    }

    public static void Toggle()
    {
        _on = !On;
        var settings = new ConfigFile();
        settings.Load(SoundBoard.SettingsPath);
        settings.SetValue(Section, Key, _on.Value);
        settings.Save(SoundBoard.SettingsPath);
    }
}
