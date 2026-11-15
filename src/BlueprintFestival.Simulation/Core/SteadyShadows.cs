namespace BlueprintFestival.Simulation.Core;

/// <summary>
/// Keeps shadows still on screen. A shadow is drawn from a picture of the field taken from the sun;
/// if the sun turns a hair every frame, or the patch of ground in that picture grows a hair, every
/// shadow edge redraws on slightly different pixels and the shadows seem to wobble. So the sun is
/// moved in small, rare steps, and the shadowed patch only changes size in whole steps.
/// </summary>
public static class SteadyShadows
{
    /// <summary>The sun moves in steps this many degrees apart: a person's evening shadow shifts about 3 cm a step, once a game minute.</summary>
    public const double SunStepDegrees = 0.25;

    /// <summary>The shadowed patch of ground grows and shrinks in steps of this many metres.</summary>
    public const double ReachStepMetres = 8;

    /// <summary>The same light, with its height and bearing put on the nearest step.</summary>
    public static SkyLight Stepped(SkyLight light) =>
        light with { Height = Snap(light.Height, SunStepDegrees), Bearing = Snap(light.Bearing, SunStepDegrees) };

    /// <summary>
    /// How far either side of the point the site camera looks at shadows are drawn, in metres, for a
    /// view so many metres high seen from so many degrees up: the ground on screen plus room for tall
    /// things, rounded up to a whole step.
    /// </summary>
    public static double Reach(double viewMetres, double pitchDegrees)
    {
        var reach = (viewMetres * 0.5 / Math.Tan(pitchDegrees * Math.PI / 180)) + 40;
        return Math.Ceiling(reach / ReachStepMetres) * ReachStepMetres;
    }

    private static double Snap(double value, double step) => Math.Round(value / step) * step;
}
