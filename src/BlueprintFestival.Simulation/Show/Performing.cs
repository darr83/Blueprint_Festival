namespace BlueprintFestival.Simulation.Show;

/// <summary>The drums and cymbals of a kit, as the drummer hits them.</summary>
public enum Drum
{
    HiHat,
    Snare,
    HighTom,
    LowTom,
    FloorTom,
    Crash,
}

/// <summary>What is happening on a stage while an act is on it.</summary>
public enum StageMoment
{
    /// <summary>Nobody on, or the act stopped.</summary>
    Quiet,

    /// <summary>A song is playing.</summary>
    Song,

    /// <summary>The few seconds between one song and the next: the crowd stops dancing to clap and cheer.</summary>
    BetweenSongs,

    /// <summary>The set is over and the act takes its applause, the crowd clapping and cheering before they go.</summary>
    Applause,
}

/// <summary>
/// How an act plays along with its own music: when the drummer hits each drum, when the guitar
/// is strummed, how the songs are spaced, and how the end of a set goes. Counted in beats of the
/// song (see <see cref="BeatInfo.BeatAt"/>), with the first beat of the song taken as the first
/// beat of a bar of four.
/// </summary>
public static class Performing
{
    /// <summary>How long the stage is quiet between one song and the next, in real seconds, while the crowd claps and cheers.</summary>
    public const double SongGapSeconds = 4;

    /// <summary>How long the last song takes to fade away at the end of a set, in real seconds.</summary>
    public const double FadeOutSeconds = 7;

    /// <summary>How long the act stays on to take the applause after the end of a set, in game minutes.</summary>
    public const int BowMinutes = 3;

    /// <summary>How many in a hundred of those watching stay to the end of the set to clap, rather than slipping away early.</summary>
    public const int StayToTheEnd = 85;

    /// <summary>How many beats in a bar.</summary>
    public const int BeatsABar = 4;

    /// <summary>Every fourth bar ends with a fill round the toms.</summary>
    public const int BarsAFill = 4;

    /// <summary>
    /// What a drum stick hits on one eighth of a bar (two to a beat, counted from 0 at the start
    /// of the song), or null if that stick does not hit then. The right stick keeps time on the
    /// hi-hat, with a crash at the start of every fourth bar; the left hits the snare on the second
    /// and fourth beats; and the last half of every fourth bar is a fill down the toms.
    /// </summary>
    public static Drum? Hit(long eighth, bool right)
    {
        var inBar = (int)(((eighth % 8) + 8) % 8);
        var bar = Math.Floor(eighth / 8.0);
        var fill = ((((long)bar % BarsAFill) + BarsAFill) % BarsAFill) == BarsAFill - 1 && inBar >= 4;
        if (fill)
        {
            // Both sticks in turn, high tom to floor tom.
            Drum[] round = { Drum.HighTom, Drum.HighTom, Drum.FloorTom, Drum.LowTom };
            var drum = round[inBar - 4];
            return (inBar % 2 == 0) == right ? drum : null;
        }

        if (right)
        {
            return inBar == 0 && ((((long)bar % BarsAFill) + BarsAFill) % BarsAFill) == 0 ? Drum.Crash : Drum.HiHat;
        }

        return inBar is 2 or 6 ? Drum.Snare : null;
    }

    /// <summary>True on the beats the bass drum is kicked: the first and third of each bar.</summary>
    public static bool Kick(long eighth) => (((eighth % 8) + 8) % 8) is 0 or 4;

    /// <summary>
    /// Where a drum stick is at a moment: how high it is lifted (0 as it strikes, 1 at the top of
    /// its swing), the drum it last hit, the drum it is coming down on next, and how far it has got
    /// from one to the other (0 to 1).
    /// </summary>
    public static (double Lift, Drum From, Drum To, double Across) Stick(double beat, bool right)
    {
        var eighths = beat * 2;
        var at = (long)Math.Floor(eighths);
        var last = at;
        var lastDrum = Hit(last, right);
        while (lastDrum is null && at - last < 8)
        {
            last--;
            lastDrum = Hit(last, right);
        }

        var next = at + 1;
        var nextDrum = Hit(next, right);
        while (nextDrum is null && next - at < 8)
        {
            next++;
            nextDrum = Hit(next, right);
        }

        var through = Math.Clamp((eighths - last) / (next - last), 0, 1);
        // The stick comes up quickly after a hit and drops on the next: higher for a longer gap.
        var height = Math.Min(1, 0.55 + (0.15 * (next - last)));
        var lift = Math.Sin(Math.PI * Math.Pow(through, 0.8)) * height;
        var from = lastDrum ?? Drum.HiHat;
        var to = nextDrum ?? from;
        return (lift, from, to, Smooth(Math.Clamp((through - 0.2) / 0.6, 0, 1)));
    }

    /// <summary>How long ago, in beats, a drum was last hit by either stick (a large number if not lately).</summary>
    public static double SinceHit(double beat, Drum drum)
    {
        var at = (long)Math.Floor(beat * 2);
        for (var e = at; e > at - 16; e--)
        {
            if (Hit(e, true) == drum || Hit(e, false) == drum)
            {
                return beat - (e / 2.0);
            }
        }

        return 99;
    }

    /// <summary>How long ago, in beats, the bass drum was last kicked.</summary>
    public static double SinceKick(double beat)
    {
        var at = (long)Math.Floor(beat * 2);
        for (var e = at; e > at - 8; e--)
        {
            if (Kick(e))
            {
                return beat - (e / 2.0);
            }
        }

        return 99;
    }

    /// <summary>
    /// Where the strumming hand is, from 1 (above the strings) to -1 (below): it sweeps down across
    /// the strings exactly on each beat and back up across them halfway between.
    /// </summary>
    public static double Strum(double beat) => -Math.Sin(Math.PI * 2 * beat);

    /// <summary>
    /// How loud the last song is as a set ends, from 1 down to 0: it fades away over the last
    /// <see cref="FadeOutSeconds"/> real seconds before the set's end.
    /// </summary>
    public static double FadeLevel(double realSecondsLeft) => Math.Clamp(realSecondsLeft / FadeOutSeconds, 0, 1);

    /// <summary>How many real seconds a stretch of game time takes to pass at a speed (forever when paused).</summary>
    public static double RealSeconds(TimeSpan game, Core.GameSpeed speed) =>
        speed == Core.GameSpeed.Paused ? double.PositiveInfinity : game.TotalSeconds / (Core.GameClock.GameSecondsPerRealSecond * (int)speed);

    /// <summary>
    /// How long someone who watched a set to the end stays clapping and cheering once it is over,
    /// in game minutes: everyone is a little different, from one minute to the whole of the bow.
    /// </summary>
    public static double ApplauseMinutes(int party)
    {
        unchecked
        {
            var x = (uint)(party * 2654435761u);
            x ^= x >> 15;
            return 1 + ((x % 1000) / 1000.0 * (BowMinutes - 1));
        }
    }

    /// <summary>True for those who stay to the end of a set they are watching, rather than slip away early.</summary>
    public static bool StaysToTheEnd(int party)
    {
        unchecked
        {
            var x = (uint)((party * 40503) ^ 0x5bd1e995);
            x ^= x >> 13;
            x *= 0x5bd1e995;
            x ^= x >> 15;
            return x % 100 < StayToTheEnd;
        }
    }

    private static double Smooth(double t) => t * t * (3 - (2 * t));
}
