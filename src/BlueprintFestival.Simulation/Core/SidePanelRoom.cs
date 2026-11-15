namespace BlueprintFestival.Simulation.Core;

/// <summary>
/// How tall the scrolling middle of a panel down the right of the screen may be. The panel's top
/// and its buttons at the bottom always show; the lines in between scroll when the screen is too
/// short for them, so the buttons never drop under the view buttons or off the bottom.
/// </summary>
public static class SidePanelRoom
{
    /// <summary>The scrolling part is never squeezed shorter than this, even on a tiny screen.</summary>
    public const float Smallest = 120;

    /// <summary>The gap left between the panel and whatever is under it.</summary>
    public const float Gap = 10;

    /// <summary>
    /// The height for the scrolling middle: all of its lines if they fit between
    /// <paramref name="top"/> and <paramref name="bottom"/> (after the frame and the buttons),
    /// otherwise what room there is, but never less than <see cref="Smallest"/>.
    /// </summary>
    public static float ScrollHeight(float content, float top, float bottom, float frame, float footer)
    {
        var room = bottom - top - frame - footer;
        return System.Math.Clamp(content, 0, System.Math.Max(Smallest, room));
    }

    /// <summary>
    /// The lowest the panel may reach: the screen's bottom edge, or higher where the view buttons
    /// or the build bar (null when hidden) come up under it.
    /// </summary>
    public static float Bottom(float screenHeight, float? viewButtonsTop, float? buildBarTop)
    {
        var bottom = screenHeight - 22;
        if (viewButtonsTop is { } view)
        {
            bottom = System.Math.Min(bottom, view - Gap);
        }

        if (buildBarTop is { } bar)
        {
            bottom = System.Math.Min(bottom, bar - Gap);
        }

        return bottom;
    }
}
