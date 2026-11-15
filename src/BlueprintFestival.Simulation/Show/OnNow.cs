using System.Globalization;

namespace BlueprintFestival.Simulation.Show;

/// <summary>One stage's line in the top bar: a word or two before the act ("On now"), the act, and when.</summary>
public readonly record struct StageLine(string Stage, string Kicker, string Name, string Tail)
{
    /// <summary>The whole line as one string, as a tooltip or a test reads it.</summary>
    public string Text
    {
        get
        {
            var line = Kicker.Length == 0 ? Name : Tail.Length == 0 ? $"{Kicker}: {Name}" : $"{Kicker}: {Name} {Tail}";
            return Stage.Length == 0 ? line : $"{Stage} · {line}";
        }
    }
}

/// <summary>
/// Who is on each stage, or on next, for the pill in the middle of the top bar. With one stage it
/// is one line as it always was; with more, one line for each stage that has music booked,
/// "Stage 1" (the main stage) first.
/// </summary>
public static class OnNow
{
    private static readonly CultureInfo British = CultureInfo.GetCultureInfo("en-GB");

    /// <summary>The lines for every stage with music, the main stage first.</summary>
    public static IReadOnlyList<StageLine> Lines(FestivalGame game) => Lines(game, game.Now);

    /// <summary>The lines at a given time of day.</summary>
    public static IReadOnlyList<StageLine> Lines(FestivalGame game, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(game);
        var shows = new List<RunningOrder> { game.Show };
        if (game.SecondShow.Stage is not null)
        {
            shows.Add(game.SecondShow);
        }

        var lines = new List<StageLine>(shows.Count);
        for (var i = 0; i < shows.Count; i++)
        {
            var name = shows.Count > 1 && shows[i].Stage is { } stage ? StageCameras.Name(game, stage) : "";
            lines.Add(Line(shows[i], now, game.ActPaused) with { Stage = name });
        }

        return lines;
    }

    /// <summary>One stage's line, without the stage's name.</summary>
    public static StageLine Line(RunningOrder music, DateTime now, bool actPaused)
    {
        ArgumentNullException.ThrowIfNull(music);
        if (music.Stage is null)
        {
            return new StageLine("", "", "No stage, no music", "");
        }

        if (music.CalledOff is not null)
        {
            return new StageLine("", "", "The music has been called off", "");
        }

        if (music.Playing(now) is { } on)
        {
            return actPaused
                ? new StageLine("", "Stopped", on.Act.Name, "")
                : new StageLine("", "On now", on.Act.Name, $"until {on.End.ToString("HH:mm", British)}");
        }

        if (music.Next(now) is { } next)
        {
            // The half hour after a set, while people go for a drink and the next act sets up.
            return new StageLine("", music.Changeover(now) ? "Stage empty. Next" : "Next", next.Act.Name, $"at {next.Start.ToString("HH:mm", British)}");
        }

        return new StageLine("", "", "The music has finished", "");
    }
}
