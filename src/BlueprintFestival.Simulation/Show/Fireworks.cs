namespace BlueprintFestival.Simulation.Show;

/// <summary>The kinds of firework the player can put in a show.</summary>
public enum FireworkKind
{
    Fountain,
    GroundMine,
    RomanCandle,
    CrackleCake,
    Rocket,
    PeonyShell,
    GoldWillow,
    FinaleBarrage,
}

/// <summary>How high a firework goes: its row on the mixer.</summary>
public enum FireworkHeight
{
    Ground,
    MidAir,
    HighSky,
}

/// <summary>
/// What there is to know about a kind of firework: its name, how high it goes, how many seconds it
/// lasts, what it costs, and whether it needs the big launch pad.
/// </summary>
public sealed record FireworkInfo(FireworkKind Kind, string Name, FireworkHeight Height, double Seconds, int Cost, bool BigPadOnly, string About);

/// <summary>One firework in a show, and how many seconds into the show it goes off.</summary>
public sealed record FireworkCue(FireworkKind Kind, double At)
{
    public double Ends => At + Fireworks.Of(Kind).Seconds;
}

/// <summary>A show the player has planned: when it goes off, in minutes after midnight, and what is in it.</summary>
public sealed record FireworkShow(int Minute, List<FireworkCue> Cues)
{
    public TimeSpan Time => TimeSpan.FromMinutes(Minute);

    public int Cost => Cues.Sum(c => Fireworks.Of(c.Kind).Cost);
}

/// <summary>One thing the crowd will think of a show: good or bad, and in words.</summary>
public sealed record RatingNote(bool Good, string Text);

/// <summary>What the crowd will think of a show: one to five stars, and why.</summary>
public sealed record ShowRating(int Stars, IReadOnlyList<RatingNote> Notes)
{
    /// <summary>How much happier everyone who sees it gets, from 0 to 1, if it goes off safely in the dark.</summary>
    public double Mood => Stars * Fireworks.MoodPerStar;
}

/// <summary>How a show went on the day.</summary>
public enum FireworkOutcome
{
    /// <summary>Fenced off and clear: it went off as planned.</summary>
    Safe,

    /// <summary>No fence round the safe zone: people wandered in, it was held up, and someone may have been hurt.</summary>
    NoFence,

    /// <summary>It never went off: no launch pad, nothing in it, or something stood in the safe zone.</summary>
    CalledOff,
}

/// <summary>What happened when a show's time came.</summary>
public sealed record FireworkResult(int Minute, FireworkOutcome Outcome, int Stars, int Hurt, string Why);

/// <summary>
/// The fireworks: eight kinds, and the rules for a 30-second show. The player lays fireworks along a
/// timeline in three rows, ground, mid air and high sky. The crowd likes variety, no dead gaps, a
/// big finish, and not more going off at once than the launch pad can take.
/// </summary>
public static class Fireworks
{
    public static readonly IReadOnlyList<FireworkInfo> All = new[]
    {
        new FireworkInfo(FireworkKind.Fountain, "Fountain", FireworkHeight.Ground, 6, 10, false, "A spray of gold sparks from the ground."),
        new FireworkInfo(FireworkKind.GroundMine, "Ground mine", FireworkHeight.Ground, 1.5, 12, false, "A burst of pink stars thrown up from the ground."),
        new FireworkInfo(FireworkKind.RomanCandle, "Roman candle", FireworkHeight.MidAir, 5, 20, false, "Balls of coloured fire, one after another."),
        new FireworkInfo(FireworkKind.CrackleCake, "Crackle cake", FireworkHeight.MidAir, 8, 60, false, "A box of shots that crackle and pop in mid air."),
        new FireworkInfo(FireworkKind.Rocket, "Rocket", FireworkHeight.HighSky, 2, 15, false, "Whistles up and bursts in blue and white."),
        new FireworkInfo(FireworkKind.PeonyShell, "Peony shell", FireworkHeight.HighSky, 3, 40, true, "A big round pink burst, high up."),
        new FireworkInfo(FireworkKind.GoldWillow, "Gold willow", FireworkHeight.HighSky, 4, 45, true, "Long gold trails that droop and hang."),
        new FireworkInfo(FireworkKind.FinaleBarrage, "Finale barrage", FireworkHeight.HighSky, 4, 120, true, "Everything at once, to end the show."),
    };

    public static FireworkInfo Of(FireworkKind kind) => All[(int)kind];

    /// <summary>How long a show is, in seconds.</summary>
    public const double ShowSeconds = 30;

    /// <summary>Fireworks go off on the half second.</summary>
    public const double Step = 0.5;

    /// <summary>The longest stretch with nothing in the sky the crowd does not mind.</summary>
    public const double LongestGap = 2;

    /// <summary>How many kinds a show needs for the crowd to call it varied.</summary>
    public const int VarietyKinds = 4;

    /// <summary>How happy each star makes everyone who sees the show.</summary>
    public const double MoodPerStar = 0.02;

    /// <summary>How much happiness a show with no fence round it costs everyone.</summary>
    public const double NoFenceMood = 0.06;

    /// <summary>How much happiness a show that is called off costs everyone.</summary>
    public const double CalledOffMood = 0.08;

    /// <summary>The chance that someone is hurt at a show with no fence round it, from a small pad and a big one.</summary>
    public static double HurtChance(bool bigPad) => bigPad ? 0.6 : 0.4;

    /// <summary>The earliest a show can be, in minutes after midnight: 8pm. It is not dark until about half past nine.</summary>
    public const int EarliestMinute = 20 * 60;

    /// <summary>The latest a show can start, in minutes after midnight: quarter past eleven, before everyone is sent home.</summary>
    public const int LatestMinute = (23 * 60) + 15;

    /// <summary>Show times go in steps of this many minutes.</summary>
    public const int MinuteStep = 15;

    /// <summary>The most shows in one day.</summary>
    public const int MostShows = 4;

    /// <summary>The line fireworks go under in the accounts.</summary>
    public const string Line = "Fireworks";

    /// <summary>How many fireworks a launch pad can let off at once.</summary>
    public static int AtOnce(bool bigPad) => bigPad ? 5 : 3;

    /// <summary>Where a firework starting at a time would go: on the half second, and not running past the end of the show.</summary>
    public static double Clamp(FireworkKind kind, double at)
    {
        var latest = ShowSeconds - Math.Min(Of(kind).Seconds, 2);
        return Math.Clamp(Math.Round(at / Step) * Step, 0, latest);
    }

    /// <summary>How many fireworks are going off at a moment in a show.</summary>
    public static int GoingOff(IEnumerable<FireworkCue> cues, double second) => cues.Count(c => second >= c.At && second < c.Ends);

    /// <summary>The places in the list of the cues that are part of too many going off at once for the pad.</summary>
    public static IReadOnlyCollection<int> TooMany(IReadOnlyList<FireworkCue> cues, bool bigPad)
    {
        var over = new HashSet<int>();
        for (var t = 0.0; t < ShowSeconds; t += Step)
        {
            var on = Enumerable.Range(0, cues.Count).Where(i => t >= cues[i].At && t < cues[i].Ends).ToList();
            if (on.Count > AtOnce(bigPad))
            {
                over.UnionWith(on);
            }
        }

        return over;
    }

    /// <summary>The longest stretch of the show with nothing going off, in seconds.</summary>
    public static double Gap(IReadOnlyList<FireworkCue> cues)
    {
        double gap = 0, longest = 0;
        for (var t = 0.0; t < ShowSeconds; t += Step)
        {
            gap = GoingOff(cues, t) > 0 ? 0 : gap + Step;
            longest = Math.Max(longest, gap);
        }

        return longest;
    }

    /// <summary>On average how many fireworks are going off in the last five seconds.</summary>
    public static double Finish(IReadOnlyList<FireworkCue> cues)
    {
        var total = 0;
        var steps = 0;
        for (var t = ShowSeconds - 5; t < ShowSeconds; t += Step)
        {
            total += GoingOff(cues, t);
            steps++;
        }

        return (double)total / steps;
    }

    /// <summary>What the crowd will think of a show let off from a small or big pad.</summary>
    public static ShowRating Rate(IReadOnlyList<FireworkCue> cues, bool bigPad)
    {
        if (cues.Count == 0)
        {
            return new ShowRating(0, new[] { new RatingNote(false, "Nothing on the timeline yet.") });
        }

        var notes = new List<RatingNote>();
        var stars = 5;
        var kinds = cues.Select(c => c.Kind).Distinct().Count();
        if (kinds >= VarietyKinds)
        {
            notes.Add(new RatingNote(true, $"Plenty of variety ({kinds} kinds)."));
        }
        else
        {
            stars--;
            notes.Add(new RatingNote(false, $"Only {kinds} kind{(kinds == 1 ? "" : "s")} of firework: it looks samey. Use {VarietyKinds} or more."));
        }

        var gap = Gap(cues);
        if (gap <= LongestGap)
        {
            notes.Add(new RatingNote(true, "No dead gaps in the sky."));
        }
        else
        {
            stars -= gap > 5 ? 2 : 1;
            notes.Add(new RatingNote(false, $"A {gap:0.#} second gap where nothing happens."));
        }

        if (Finish(cues) >= 2)
        {
            notes.Add(new RatingNote(true, "Builds to a big finish."));
        }
        else
        {
            stars--;
            notes.Add(new RatingNote(false, "The last 5 seconds are thin. People love a finale."));
        }

        var pad = bigPad ? "big" : "small";
        if (TooMany(cues, bigPad).Count > 0)
        {
            stars--;
            notes.Add(new RatingNote(false, $"More than {AtOnce(bigPad)} at once is too much for the {pad} launch pad."));
        }
        else
        {
            notes.Add(new RatingNote(true, $"Never more than {AtOnce(bigPad)} at once, fine for the {pad} launch pad."));
        }

        var big = cues.Count(c => Of(c.Kind).BigPadOnly);
        if (big > 0 && !bigPad)
        {
            stars--;
            notes.Add(new RatingNote(false, $"{big} big firework{(big == 1 ? " needs" : "s need")} the big launch pad. On a small one {(big == 1 ? "it stays" : "they stay")} in the box."));
        }

        return new ShowRating(Math.Max(1, stars), notes);
    }

    /// <summary>A show to start from: 15 fireworks, five stars from the big pad.</summary>
    public static List<FireworkCue> Example() => new()
    {
        new(FireworkKind.Fountain, 0), new(FireworkKind.Rocket, 1.5), new(FireworkKind.RomanCandle, 3.5), new(FireworkKind.GroundMine, 7),
        new(FireworkKind.Rocket, 8.5), new(FireworkKind.PeonyShell, 10.5), new(FireworkKind.CrackleCake, 12.5), new(FireworkKind.GoldWillow, 16.5),
        new(FireworkKind.Rocket, 20), new(FireworkKind.GroundMine, 21), new(FireworkKind.PeonyShell, 22.5), new(FireworkKind.Fountain, 24),
        new(FireworkKind.FinaleBarrage, 26), new(FireworkKind.Rocket, 27), new(FireworkKind.GroundMine, 28),
    };

    /// <summary>A show for the small pad: only the fireworks it can take, five stars.</summary>
    public static List<FireworkCue> SmallExample() => new()
    {
        new(FireworkKind.Fountain, 0), new(FireworkKind.Rocket, 1.5), new(FireworkKind.RomanCandle, 3.5), new(FireworkKind.GroundMine, 7),
        new(FireworkKind.Rocket, 8.5), new(FireworkKind.CrackleCake, 10.5), new(FireworkKind.Rocket, 14), new(FireworkKind.Fountain, 16),
        new(FireworkKind.RomanCandle, 19), new(FireworkKind.Rocket, 22), new(FireworkKind.Fountain, 24), new(FireworkKind.Rocket, 26),
        new(FireworkKind.GroundMine, 27), new(FireworkKind.Rocket, 28),
    };
}
