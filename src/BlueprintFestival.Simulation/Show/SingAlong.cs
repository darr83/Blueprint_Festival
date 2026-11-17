namespace BlueprintFestival.Simulation.Show;

/// <summary>How the crowd sings along, set from the dev panel.</summary>
public enum SingingSetting
{
    /// <summary>Nobody sings along.</summary>
    Off,

    /// <summary>As the game plays it: the happier the crowd, the more sing, and now and then the act leaves a chorus to them.</summary>
    Normal,

    /// <summary>Every chorus is left to the crowd: the act and the music drop out and only the crowd sings.</summary>
    JustTheCrowd,
}

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
        if (Setting == SingingSetting.Off)
        {
            return 0;
        }

        var mood = Math.Clamp((happiness - SingsFrom) / 45.0, 0, 1);
        var following = Math.Clamp((Math.Log10(Math.Max(fans, 1)) - 2.5) / 4.0, 0, 1);
        var share = mood * (0.45 + (0.55 * following));
        // With every chorus left to them, enough of the crowd sings to carry it, however they feel.
        return Setting == SingingSetting.JustTheCrowd ? Math.Max(share, JustTheCrowdShare) : share;
    }

    /// <summary>How the crowd sings along just now. Normal unless changed on the dev panel; not saved.</summary>
    public static SingingSetting Setting { get; set; } = SingingSetting.Normal;

    /// <summary>How keen the crowd is to sing, at least, while every chorus is left to them.</summary>
    public const double JustTheCrowdShare = 0.6;

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
    public static double Loudness(int singers) => singers <= 0 ? 0 : Math.Clamp(Math.Sqrt(singers / 350.0), 0.12, 1);

    /// <summary>How often a chorus is left to the crowd: the artist and the music stop for a few seconds and only the crowd sings.</summary>
    public const double LeftToTheCrowdChance = 0.15;

    /// <summary>The shortest and longest the artist leaves the singing to the crowd, in seconds.</summary>
    public const double LeftShortest = 5;

    /// <inheritdoc cref="LeftShortest"/>
    public const double LeftLongest = 10;

    /// <summary>How long the music takes to drop out, and to come back in, in seconds.</summary>
    public const double LeftFadeSeconds = 0.6;

    /// <summary>
    /// Whether the artist and the band stop in this chorus and leave it to the crowd, and when: about one
    /// chorus in seven, for five to ten seconds, once the whole crowd has come in and ending a second before
    /// the chorus does. Null if not, or if the chorus is too short. The same roll always gives the same answer.
    /// </summary>
    /// <param name="roll">A number picked at random each time the song is played, so it is not always the same chorus.</param>
    /// <param name="chorus">The chorus.</param>
    public static Chorus? LeftToTheCrowd(int roll, Chorus chorus)
    {
        if (Setting == SingingSetting.Off)
        {
            return null;
        }

        if (Setting == SingingSetting.JustTheCrowd)
        {
            // The whole chorus, from the first word to the last.
            return chorus.End - chorus.Start >= 1 ? new Chorus(chorus.Start, chorus.End) : null;
        }

        if ((Roll(roll, 5) % 1000) / 1000.0 >= LeftToTheCrowdChance)
        {
            return null;
        }

        var from = chorus.Start + SpreadSeconds;
        var to = chorus.End - 1;
        if (to - from < LeftShortest)
        {
            return null;
        }

        var length = Math.Min(LeftShortest + ((Roll(roll, 6) % 1000) / 1000.0 * (LeftLongest - LeftShortest)), to - from);
        var start = from + ((Roll(roll, 7) % 1000) / 1000.0 * (to - from - length));
        return new Chorus(start, start + length);
    }

    /// <summary>
    /// How far the music has dropped out, from 0 (playing) to 1 (only the crowd), this many seconds into
    /// the song, for the stretches left to the crowd. The song keeps going underneath, so it comes back in time.
    /// </summary>
    public static double LeftLevel(IEnumerable<Chorus> left, double seconds)
    {
        ArgumentNullException.ThrowIfNull(left);
        var level = 0.0;
        foreach (var stretch in left)
        {
            var into = Math.Min(seconds - stretch.Start, stretch.End - seconds);
            level = Math.Max(level, Math.Clamp(into / LeftFadeSeconds, 0, 1));
        }

        return level;
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
