namespace BlueprintFestival.Simulation.Show;

/// <summary>
/// Who in the crowd sings along in a chorus, and how loud they are. A happy crowd sings and a fed-up
/// one does not; an act with a big following gets more of the field singing. The people at the front
/// come in first and it spreads back through the crowd over a few seconds. Some throw both arms up and
/// sway; the rest keep dancing and sing as they go. The same person always does the same.
/// </summary>
public static class SingAlong
{
    /// <summary>How happy the crowd must be (out of 100) before anyone sings.</summary>
    public const int SingsFrom = 35;

    /// <summary>How far back from the front of the stage (metres) the singing takes to spread to the back.</summary>
    public const double FrontToBack = 45;

    /// <summary>How long the singing takes to spread from the front to the back, in seconds.</summary>
    public const double SpreadSeconds = 3;

    /// <summary>
    /// How keen the crowd is to sing, from 0 to 1: nobody below <see cref="SingsFrom"/> happiness, rising to
    /// everyone who is happy enough; and more of them for an act with a big following.
    /// </summary>
    public static double Share(int happiness, int fans)
    {
        var mood = Math.Clamp((happiness - SingsFrom) / 45.0, 0, 1);
        var following = Math.Clamp((Math.Log10(Math.Max(fans, 1)) - 2.5) / 4.0, 0, 1);
        return mood * (0.45 + (0.55 * following));
    }

    /// <summary>
    /// Whether one person is singing: the keener the crowd, the more do, the front first. Someone at
    /// the back joins a few seconds into the chorus, as the singing spreads back to them.
    /// </summary>
    /// <param name="person">Who they are.</param>
    /// <param name="fromFront">How far they are from the front of the stage, in metres.</param>
    /// <param name="secondsInto">How long the chorus has been going, in seconds (below 0 for no chorus).</param>
    /// <param name="share">How keen the crowd is (see <see cref="Share"/>).</param>
    public static bool Sings(int person, double fromFront, double secondsInto, double share)
    {
        if (secondsInto < 0 || share <= 0)
        {
            return false;
        }

        var back = Math.Clamp(fromFront / FrontToBack, 0, 1);
        var joins = (back * SpreadSeconds) + ((Roll(person, 2) % 100) / 100.0 * 0.7);
        if (secondsInto < joins)
        {
            return false;
        }

        // The front rows are keenest: there everyone sings once the crowd is keen at all.
        var keen = (Roll(person, 1) % 1000) / 1000.0 * (0.4 + (0.6 * back));
        return keen < share;
    }

    /// <summary>True for those who throw both arms up and sway as they sing; the rest keep dancing.</summary>
    public static bool ArmsUp(int person) => Roll(person, 3) % 10 >= 3;

    /// <summary>How open someone's mouth is as they sing, from 0 to 1, moving with the beat.</summary>
    public static double Mouth(int person, double beat)
    {
        var syllable = Math.Abs(Math.Sin((beat + ((Roll(person, 4) % 10) / 20.0)) * Math.PI));
        return 0.45 + (0.5 * syllable);
    }

    /// <summary>How loud the crowd singing is, from 0 to 1, for this many people singing.</summary>
    public static double Loudness(int singers) => singers <= 0 ? 0 : Math.Clamp(Math.Sqrt(singers / 350.0), 0.2, 1);

    /// <summary>Which of the crowd's singing sounds a song gets, the same one every time for the same song.</summary>
    public static int SoundFor(string song, int sounds)
    {
        ArgumentNullException.ThrowIfNull(song);
        if (sounds <= 1)
        {
            return 0;
        }

        var hash = 17;
        foreach (var c in song)
        {
            hash = unchecked((hash * 31) + c);
        }

        return (int)((uint)hash % (uint)sounds);
    }

    private static int Roll(int person, int question)
    {
        unchecked
        {
            var x = ((uint)person * 83492791u) ^ ((uint)question * 2654435761u);
            x ^= x >> 13;
            x *= 0x5bd1e995;
            x ^= x >> 15;
            return (int)(x & 0x7fffffff);
        }
    }
}
