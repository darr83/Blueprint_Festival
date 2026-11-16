using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation;

/// <summary>What sort of thing a line in the day's log is about.</summary>
public enum DayNoteKind
{
    Gates,
    Ill,
    Hurt,
    Trouble,
    LostChild,
    Crush,
    Choice,
    Weather,
    Fireworks,
    CalledOff,

    /// <summary>Someone has been sick on the ground.</summary>
    Sick,

    /// <summary>News from the camping field, such as the fire officer's visit.</summary>
    Camp,
}

/// <summary>How a line in the day's log stands: still going on, ended well, ended badly, or just news.</summary>
public enum DayTone
{
    Open,
    Good,
    Bad,
    Plain,
}

/// <summary>One line in the day's log: when, what happened, and how it ended (or where it has got to).</summary>
public sealed class DayNote
{
    public DayNote(int id, DateTime at, DayNoteKind kind, string what, string outcome, DayTone tone, int incident = -1)
    {
        (Id, At, Kind, What, Outcome, Tone, Incident) = (id, at, kind, what, outcome, tone, incident);
    }

    public int Id { get; }

    public DateTime At { get; }

    public DayNoteKind Kind { get; }

    public string What { get; }

    /// <summary>What has happened since: "First aider on the way", "Treated", and so on.</summary>
    public string Outcome { get; internal set; }

    public DayTone Tone { get; internal set; }

    /// <summary>The incident this line follows, or -1.</summary>
    public int Incident { get; }

    /// <summary>True for things that went wrong for someone: taken ill, hurt, trouble, a lost child.</summary>
    public bool IsIncident => Kind is DayNoteKind.Ill or DayNoteKind.Hurt or DayNoteKind.Trouble or DayNoteKind.LostChild or DayNoteKind.Crush;
}

/// <summary>How happy the crowd was at one moment of the day.</summary>
public readonly record struct MoodPoint(DateTime At, int Happy);

/// <summary>
/// The day's log, kept for the event day panel: every incident as it happens and how it ended,
/// crushes and what the player chose, the weather turning, fireworks, and the crowd's happiness
/// every quarter of an hour.
/// </summary>
public sealed partial class FestivalGame
{
    /// <summary>How often the crowd's happiness is written down, in game seconds.</summary>
    public const int MoodEvery = 15 * 60;

    /// <summary>How far away something can be and still be "by" it, in metres.</summary>
    public const double ByReach = 14;

    private readonly List<DayNote> _notes = new();
    private readonly List<MoodPoint> _mood = new();
    private int _nextNote;
    private bool _wasRaining;

    /// <summary>Everything logged today, newest first.</summary>
    public IReadOnlyList<DayNote> DayLog => Enumerable.Reverse(_notes).ToList();

    /// <summary>The crowd's happiness through the day, every quarter of an hour while anyone is on site.</summary>
    public IReadOnlyList<MoodPoint> MoodLine => _mood;

    private DayNote Note(DayNoteKind kind, string what, string outcome, DayTone tone, int incident = -1)
    {
        var note = new DayNote(_nextNote++, Clock.Now, kind, what, outcome, tone, incident);
        _notes.Add(note);
        return note;
    }

    /// <summary>Writes how an incident has ended, or where it has got to.</summary>
    private void Settle(Incident incident, string outcome, DayTone tone)
    {
        if (_notes.LastOrDefault(n => n.Incident == incident.Id) is { } note)
        {
            (note.Outcome, note.Tone) = (outcome, tone);
        }
    }

    /// <summary>Adds an incident to the day and to the log.</summary>
    private void Raise(Incident incident, DayNoteKind kind, string what)
    {
        _incidents.Add(incident);
        Note(kind, $"{what} {Whereabouts(incident.X, incident.Y)}".TrimEnd(), "Waiting for help", DayTone.Open, incident.Id);
    }

    /// <summary>Where a spot is, in words: "by the bar tent", "at the front of Stage 1", or "on the field".</summary>
    public string Whereabouts(double x, double y)
    {
        string? best = null;
        var nearest = ByReach;
        var stages = RunningOrder.InOrder(Site);
        for (var i = 0; i < stages.Count; i++)
        {
            var (fx, fy, _, _) = RunningOrder.Front(stages[i]);
            var d = Distance(fx, fy, x, y);
            if (d < nearest * 1.5)
            {
                (best, nearest) = ($"at the front of {StageName(i, stages.Count)}", d / 1.5);
            }
        }

        foreach (var stall in Site.Stalls)
        {
            var (sx, sy, _, _) = SiteLayout.Front(stall.Rect, stall.Facing);
            var d = Distance(sx, sy, x, y);
            if (d < nearest)
            {
                (best, nearest) = ($"by the {Catalogue.Of(stall.Kind).Name.ToLowerInvariant()}", d);
            }
        }

        return best ?? (Field.Field.Contains(FieldMap.TileAt(x, y)) ? "on the field" : "");
    }

    /// <summary>"Stage 1", "Stage 2"; or just "the stage" when there is only one.</summary>
    public static string StageName(int place, int stages) => stages <= 1 ? "the stage" : $"Stage {place + 1}";

    /// <summary>Once a second: notes the happiness every quarter hour, and the rain starting and stopping.</summary>
    private void TickDayLog(DateTime now)
    {
        if (!Planned || Phase != Phase.Day)
        {
            return;
        }

        if (Clock.TickCount % MoodEvery == 0 && !Crowd.GatesShut && Crowd.OnSite > 0)
        {
            _mood.Add(new MoodPoint(now, Crowd.Happiness));
        }

        if (Clock.TickCount % 60 == 15)
        {
            var raining = RainNow > 0;
            if (raining != _wasRaining)
            {
                _wasRaining = raining;
                if (raining)
                {
                    Note(DayNoteKind.Weather, Weather.Kind == WeatherKind.Rain ? "Rain started" : "Showers started", GroundCovered > 0 ? "Ground cover down" : "The ground will get muddy", DayTone.Plain);
                }
                else
                {
                    Note(DayNoteKind.Weather, "The rain stopped", "", DayTone.Plain);
                }
            }
        }
    }

    private DayLogSave SaveDayLog() => new(
        _notes.Select(n => new DayNoteSave(n.Id, n.At, n.Kind, n.What, n.Outcome, n.Tone, n.Incident)).ToList(),
        _mood.ToList(), _nextNote, _wasRaining);

    private void LoadDayLog(DayLogSave? save)
    {
        if (save is null)
        {
            return;
        }

        _notes.AddRange(save.Notes.Select(n => new DayNote(n.Id, n.At, n.Kind, n.What, n.Outcome, n.Tone, n.Incident)));
        _mood.AddRange(save.Mood);
        (_nextNote, _wasRaining) = (save.NextNote, save.WasRaining);
    }
}

public sealed record DayNoteSave(int Id, DateTime At, DayNoteKind Kind, string What, string Outcome, DayTone Tone, int Incident);

public sealed record DayLogSave(List<DayNoteSave> Notes, List<MoodPoint> Mood, int NextNote, bool WasRaining);
