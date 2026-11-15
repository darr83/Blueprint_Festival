using System;
using BlueprintFestival.Audio;
using BlueprintFestival.Simulation.Core;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Always loaded (an autoload). Puts the game full screen when it starts, swaps full screen and
/// window on F11 or Alt+Enter from anywhere (menus and pause included), and remembers the choice
/// in the settings file the volume sliders use.
/// </summary>
public partial class ScreenMode : Node
{
    private const string Section = "screen";
    private const string Key = "full_screen";

    public static ScreenMode? Instance { get; private set; }

    /// <summary>True while the game fills the screen.</summary>
    public static bool FullScreen { get; private set; } = ScreenSettings.DefaultFullScreen;

    /// <summary>Told when the game goes full screen or back to a window, so buttons can change their words.</summary>
    public static event Action? Changed;

    /// <summary>
    /// False in the headless launch test and the picture scripts: there the window is left at the
    /// size it was given, but the choice still swaps so it can be checked.
    /// </summary>
    private bool _applies;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        _applies = DisplayServer.GetName() != "headless";
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg.Contains("shots", StringComparison.Ordinal) || arg == "--smoke-test")
            {
                _applies = false;
            }
        }

        var settings = new ConfigFile();
        if (settings.Load(SoundBoard.SettingsPath) == Error.Ok)
        {
            FullScreen = settings.GetValue(Section, Key, ScreenSettings.DefaultFullScreen).AsBool();
        }

        Apply();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false } key &&
            ScreenSettings.IsToggleKey(key.Keycode.ToString(), key.AltPressed))
        {
            GetViewport()?.SetInputAsHandled();
            Toggle();
        }
    }

    public void Toggle() => Set(!FullScreen);

    public void Set(bool fullScreen)
    {
        FullScreen = fullScreen;
        Apply();
        var settings = new ConfigFile();
        settings.Load(SoundBoard.SettingsPath);
        settings.SetValue(Section, Key, FullScreen);
        settings.Save(SoundBoard.SettingsPath);
        Changed?.Invoke();
    }

    private void Apply()
    {
        if (!_applies)
        {
            return;
        }

        if (FullScreen)
        {
            // Borderless full screen: fills the screen, and Alt+Tab to other windows works smoothly.
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
            return;
        }

        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
        var screen = DisplayServer.WindowGetCurrentScreen();
        var usable = DisplayServer.ScreenGetUsableRect(screen);
        var (width, height) = ScreenSettings.WindowSize(usable.Size.X, usable.Size.Y);
        var size = new Vector2I(width, height);
        DisplayServer.WindowSetSize(size);
        DisplayServer.WindowSetPosition(usable.Position + ((usable.Size - size) / 2));
    }

    /// <summary>A button for the menus that swaps full screen and window, and keeps its words up to date.</summary>
    public static Button MakeButton()
    {
        var button = BlueprintFestival.UI.UiStyle.SecondaryButton(ScreenSettings.ButtonText(FullScreen), () => Instance?.Toggle());
        button.Name = "ScreenButton";
        void Update() => button.Text = ScreenSettings.ButtonText(FullScreen);
        Changed += Update;
        button.TreeExiting += () => Changed -= Update;
        return button;
    }
}
