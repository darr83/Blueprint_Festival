using System.Globalization;
using System.Text;

namespace BlueprintFestival.Simulation.Show;

/// <summary>One chorus of a song: when it starts and when it ends, in seconds into the song.</summary>
public readonly record struct Chorus(double Start, double End)
{
    public double Length => End - Start;
}

/// <summary>
/// The choruses of one song, marked by the player in the chorus marker (on the main menu), so the
/// crowd can sing along with them. Saved in a small text file beside the song, named after it with
/// ".chorus" on the end, so "01 Hay Fever.mp3" has "01 Hay Fever.mp3.chorus". A song with no
/// chorus file has no sing-along. The file can be edited by hand too.
/// </summary>
public sealed record Choruses(IReadOnlyList<Chorus> All)
{
    /// <summary>What goes on the end of a song's file name to name its chorus file.</summary>
    public const string Ending = ".chorus";

    /// <summary>The shortest a chorus can be, in seconds; a quicker pair of taps is taken as a slip.</summary>
    public const double Shortest = 2;

    /// <summary>How long the crowd takes to come in at the start of a chorus, in seconds.</summary>
    public const double ComeInSeconds = 1.2;

    /// <summary>How long the singing takes to die away after the chorus ends, in seconds.</summary>
    public const double DieAwaySeconds = 1.8;

    public static readonly Choruses None = new(Array.Empty<Chorus>());

    /// <summary>The chorus file for a song file.</summary>
    public static string FileFor(string song) => song + Ending;

    public bool Any => All.Count > 0;

    /// <summary>
    /// How much the crowd is singing at a moment in the song, from 0 to 1: coming in over the first
    /// second or so of a chorus, all together through it, and dying away just after it ends.
    /// </summary>
    public double SingingAt(double seconds)
    {
        var most = 0.0;
        foreach (var chorus in All)
        {
            double level;
            if (seconds < chorus.Start)
            {
                continue;
            }

            if (seconds <= chorus.End)
            {
                level = Math.Min(1, (seconds - chorus.Start) / ComeInSeconds);
            }
            else
            {
                level = Math.Min(1, chorus.Length / ComeInSeconds) * (1 - ((seconds - chorus.End) / DieAwaySeconds));
            }

            most = Math.Max(most, level);
        }

        return Math.Clamp(most, 0, 1);
    }

    /// <summary>How long the chorus going on at a moment (or just ended) has been going, in seconds; -1 if none.</summary>
    public double SecondsInto(double seconds)
    {
        foreach (var chorus in All)
        {
            if (seconds >= chorus.Start && seconds <= chorus.End + DieAwaySeconds)
            {
                return seconds - chorus.Start;
            }
        }

        return -1;
    }

    /// <summary>These choruses with one more, kept in order; one that overlaps another is joined to it.</summary>
    public Choruses With(double start, double end)
    {
        if (end < start)
        {
            (start, end) = (end, start);
        }

        start = Math.Max(0, start);
        if (end - start < Shortest || !double.IsFinite(start) || !double.IsFinite(end))
        {
            return this;
        }

        var list = All.Append(new Chorus(start, end)).OrderBy(c => c.Start).ToList();
        var joined = new List<Chorus>();
        foreach (var chorus in list)
        {
            if (joined.Count > 0 && chorus.Start <= joined[^1].End)
            {
                joined[^1] = new Chorus(joined[^1].Start, Math.Max(joined[^1].End, chorus.End));
            }
            else
            {
                joined.Add(chorus);
            }
        }

        return new Choruses(joined);
    }

    /// <summary>These choruses without one of them.</summary>
    public Choruses Without(int index) =>
        index < 0 || index >= All.Count ? this : new Choruses(All.Where((_, i) => i != index).ToList());

    /// <summary>A time as minutes and seconds, such as 1:05.4.</summary>
    public static string Clock(double seconds)
    {
        seconds = Math.Max(0, seconds);
        var minutes = (int)(seconds / 60);
        return string.Create(CultureInfo.InvariantCulture, $"{minutes}:{seconds - (minutes * 60):00.0}");
    }

    /// <summary>The text of the chorus file.</summary>
    public string Write()
    {
        var text = new StringBuilder();
        text.AppendLine("# The choruses of this song, marked in Blueprint Festival so the crowd sings along.");
        text.AppendLine("# One chorus a line: when it starts and when it ends, in minutes:seconds.");
        text.AppendLine("# Change the times, add a line, or delete this file to have no sing-along.");
        foreach (var chorus in All)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"chorus = {Clock(chorus.Start)} - {Clock(chorus.End)}");
        }

        return text.ToString();
    }

    /// <summary>Reads a chorus file. Lines it cannot make sense of are left out.</summary>
    public static Choruses Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var choruses = None;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var equals = line.IndexOf('=');
            if (equals >= 0)
            {
                line = line[(equals + 1)..];
            }

            var parts = line.Split(new[] { " - ", "-", " to ", "\t" }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && Seconds(parts[0]) is { } start && Seconds(parts[1]) is { } end)
            {
                choruses = choruses.With(start, end);
            }
        }

        return choruses;
    }

    /// <summary>A time written as seconds (75.5) or minutes and seconds (1:15.5). Null if it is not one.</summary>
    public static double? Seconds(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        text = text.Trim();
        var colon = text.IndexOf(':');
        if (colon < 0)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var s) && double.IsFinite(s) && s >= 0 ? s : null;
        }

        return int.TryParse(text[..colon], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
            && double.TryParse(text[(colon + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            && double.IsFinite(seconds) && seconds >= 0 && seconds < 60
            ? (minutes * 60) + seconds
            : null;
    }
}

/// <summary>
/// The chorus marker's taps: the first tap marks where a chorus starts, the next where it ends.
/// Undo takes back the last tap.
/// </summary>
public sealed class ChorusMarking
{
    private readonly Stack<Choruses> _before = new();

    public ChorusMarking(Choruses start) => Marks = start;

    /// <summary>The choruses marked so far.</summary>
    public Choruses Marks { get; private set; }

    /// <summary>Where the chorus being marked started, if its end has not been tapped yet.</summary>
    public double? Started { get; private set; }

    /// <summary>True when there is something to undo.</summary>
    public bool CanUndo => Started is not null || _before.Count > 0;

    /// <summary>
    /// A tap at a moment in the song: starts a chorus, or ends the one started. Returns true when a
    /// chorus has just been added (so it can be saved). Two taps too close together end nothing.
    /// </summary>
    public bool Tap(double seconds)
    {
        if (Started is not { } start)
        {
            Started = seconds;
            return false;
        }

        if (seconds - start < Choruses.Shortest)
        {
            return false;
        }

        _before.Push(Marks);
        Marks = Marks.With(start, seconds);
        Started = null;
        return true;
    }

    /// <summary>Takes back the last tap. Returns true when the marks themselves changed.</summary>
    public bool Undo()
    {
        if (Started is not null)
        {
            Started = null;
            return false;
        }

        if (_before.Count == 0)
        {
            return false;
        }

        Marks = _before.Pop();
        return true;
    }

    /// <summary>Removes one marked chorus.</summary>
    public void Remove(int index)
    {
        if (index >= 0 && index < Marks.All.Count)
        {
            _before.Push(Marks);
            Marks = Marks.Without(index);
        }
    }

    /// <summary>Takes away every chorus of the song.</summary>
    public void Clear()
    {
        if (Marks.Any)
        {
            _before.Push(Marks);
        }

        Marks = Choruses.None;
        Started = null;
    }
}
