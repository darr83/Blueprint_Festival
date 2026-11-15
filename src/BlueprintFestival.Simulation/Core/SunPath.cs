namespace BlueprintFestival.Simulation.Core;

/// <summary>What is lighting the field from the sky: the sun by day, the moon at night.</summary>
/// <param name="Height">Degrees above the horizon.</param>
/// <param name="Bearing">Compass degrees it shines from: 90 is east, 180 south, 270 west.</param>
/// <param name="Strength">How strong it is, 0 to 1. Both are weak for a while at dusk and dawn.</param>
/// <param name="Moon">True when it is the moon.</param>
public readonly record struct SkyLight(double Height, double Bearing, double Strength, bool Moon)
{
    /// <summary>How long a shadow is, as a multiple of the height of the thing casting it.</summary>
    public double ShadowLength => 1 / Math.Tan(Height * Math.PI / 180);
}

/// <summary>
/// Where the sun is through a British summer day: up in the north-east before five, highest in the
/// south at one o'clock, down in the north-west at half past nine. Shadows are long in the morning,
/// short at lunchtime and long again in the evening, and swing round the field as the day goes on.
/// </summary>
public static class SunPath
{
    public const double Sunrise = 4.5;
    public const double Sunset = 21.5;

    /// <summary>When the sun is due south and highest: one o'clock, on summer time.</summary>
    public const double Noon = (Sunrise + Sunset) / 2;

    /// <summary>Degrees above the horizon at its highest.</summary>
    public const double Highest = 58;

    /// <summary>Shadows are never drawn from lower than this, or they would run right across the field.</summary>
    public const double LowestDrawn = 16;

    public const double MoonHeight = 48;
    public const double MoonBearing = 215;

    /// <summary>Degrees above the horizon; a little below zero when the sun is down.</summary>
    public static double Height(TimeSpan timeOfDay)
    {
        var hour = timeOfDay.TotalHours % 24;
        var along = (hour - Sunrise) / (Sunset - Sunrise);
        return along is < -0.1 or > 1.1 ? -18 : Highest * Math.Sin(Math.PI * along);
    }

    /// <summary>The compass direction the sun is in: it goes round 15 degrees an hour.</summary>
    public static double Bearing(TimeSpan timeOfDay) => 180 + (((timeOfDay.TotalHours % 24) - Noon) * 15);

    /// <summary>How warm the sunlight is: 0 for white midday light, 1 for the deep orange of a sun on the horizon.</summary>
    public static double Warmth(TimeSpan timeOfDay) => 1 - Math.Clamp((Height(timeOfDay) - 4) / 30, 0, 1);

    /// <summary>The light in the sky at a time of day.</summary>
    public static SkyLight At(TimeSpan timeOfDay)
    {
        var height = Height(timeOfDay);
        if (height > 1.5)
        {
            // The sun comes up to full strength over its first few degrees.
            var strength = Math.Clamp((height - 1.5) / 10, 0, 1);
            return new SkyLight(Math.Max(height, LowestDrawn), Bearing(timeOfDay), strength * strength * (3 - (2 * strength)), false);
        }

        // The moon takes over once the sun has gone, coming up as the last daylight goes.
        var night = 1 - Daylight.At(timeOfDay);
        return new SkyLight(MoonHeight, MoonBearing, Math.Clamp((1.5 - height) / 4, 0, 1) * night, true);
    }
}
