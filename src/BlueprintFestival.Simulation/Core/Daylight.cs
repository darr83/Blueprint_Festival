namespace BlueprintFestival.Simulation.Core;

/// <summary>
/// How light it is outdoors on a British summer day. The festival is outside, so unlike a hospital
/// ward the night really is dark, and later on that matters for lighting, safety and mood.
/// </summary>
public static class Daylight
{
    /// <summary>First light: the sky starts to brighten.</summary>
    public const double DawnStarts = 4.25;

    /// <summary>Full daylight from here.</summary>
    public const double DawnEnds = 6.0;

    /// <summary>The light starts to go.</summary>
    public const double DuskStarts = 20.0;

    /// <summary>Fully dark from here.</summary>
    public const double DuskEnds = 22.0;

    /// <summary>How light it is at a time of day: 0 in full night, 1 in full day, easing across dawn and dusk.</summary>
    public static double At(TimeSpan timeOfDay)
    {
        var hour = timeOfDay.TotalHours % 24;
        var rising = (hour - DawnStarts) / (DawnEnds - DawnStarts);
        var falling = (DuskEnds - hour) / (DuskEnds - DuskStarts);
        var light = Math.Clamp(Math.Min(rising, falling), 0, 1);
        return light * light * (3 - (2 * light));
    }

    public static double At(DateTime now) => At(now.TimeOfDay);

    /// <summary>
    /// How low and golden the sun is: 1 halfway through dawn or dusk, 0 at midday and at night.
    /// </summary>
    public static double GoldenHour(TimeSpan timeOfDay)
    {
        var light = At(timeOfDay);
        return 1 - Math.Abs((light * 2) - 1);
    }

    public static bool IsDark(TimeSpan timeOfDay) => At(timeOfDay) < 0.25;
}
