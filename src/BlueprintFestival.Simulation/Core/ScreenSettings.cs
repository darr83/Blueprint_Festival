namespace BlueprintFestival.Simulation.Core;

/// <summary>
/// Full screen or a window. The game starts full screen (a borderless window covering the whole
/// screen); F11 or Alt+Enter, or the button in the pause menu, swaps between the two, and the
/// choice is remembered between games.
/// </summary>
public static class ScreenSettings
{
    /// <summary>A first game, with nothing saved yet, plays full screen.</summary>
    public const bool DefaultFullScreen = true;

    /// <summary>The size the game is laid out for. Bigger or smaller screens scale the whole picture.</summary>
    public const int DesignWidth = 1600;

    public const int DesignHeight = 900;

    /// <summary>How much of the screen a window may take up, so its title bar and the taskbar still show.</summary>
    public const double MostOfTheScreen = 0.9;

    /// <summary>True for the keys that swap full screen and window: F11, or Alt with either Enter key.</summary>
    public static bool IsToggleKey(string key, bool altHeld) =>
        key == "F11" || (altHeld && key is "Enter" or "KpEnter");

    /// <summary>
    /// The window size when not full screen: the design size, shrunk (keeping its shape) to fit
    /// in most of a smaller screen, such as a laptop's.
    /// </summary>
    public static (int Width, int Height) WindowSize(int screenWidth, int screenHeight)
    {
        if (screenWidth <= 0 || screenHeight <= 0)
        {
            return (DesignWidth, DesignHeight);
        }

        var scale = System.Math.Min(1.0, System.Math.Min(
            screenWidth * MostOfTheScreen / DesignWidth,
            screenHeight * MostOfTheScreen / DesignHeight));
        return ((int)(DesignWidth * scale), (int)(DesignHeight * scale));
    }

    /// <summary>The words on the button that swaps the two.</summary>
    public static string ButtonText(bool fullScreen) =>
        fullScreen ? "Play in a window  (F11)" : "Play full screen  (F11)";
}
