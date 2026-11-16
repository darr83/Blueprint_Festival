using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;

namespace BlueprintFestival.Simulation.Show;

/// <summary>Where a set has got to on the day.</summary>
public enum SetState
{
    Played,
    OnNow,
    Next,
    Later,
    Cancelled,
}

/// <summary>One set in the event day panel's running order.</summary>
public sealed record SetLine(DateTime Start, DateTime End, string Act, Genre Genre, int Stars, bool Headliner, SetState State);

/// <summary>One stage in the event day panel: its sets, and how many are watching now.</summary>
public sealed record StageDay(string Name, string Kind, int Watching, IReadOnlyList<SetLine> Sets);

/// <summary>A line of money in or out.</summary>
public sealed record MoneyLine(string What, int Amount);

/// <summary>
/// Everything the event day panel ("The day so far") shows, worked out from the game in one go so
/// the panel only has to lay it out: the headline numbers, the running order of every stage, how
/// people feel through the day, injuries and first aid, the day's log, money, and what is worth a look.
/// </summary>
public sealed class DayReport
{
    /// <summary>Below this many in a hundred content, a need is worth a look.</summary>
    public const int Worrying = 65;

    private DayReport()
    {
    }

    public DateTime Now { get; private init; }

    public int OnSite { get; private init; }

    /// <summary>Tickets sold ahead, or how many are expected when tickets are sold at the gate.</summary>
    public int Sold { get; private init; }

    public int Happy { get; private init; }

    /// <summary>How much happiness has moved since the last reading about two hours ago (0 early in the day).</summary>
    public int HappyChange { get; private init; }

    public int Fed { get; private init; }

    public int Watered { get; private init; }

    public int Toilets { get; private init; }

    public int WentHomeUnhappy { get; private init; }

    public int GaveUpQueuing { get; private init; }

    public IReadOnlyList<MoodPoint> MoodLine { get; private init; } = Array.Empty<MoodPoint>();

    public IReadOnlyList<StageDay> Stages { get; private init; } = Array.Empty<StageDay>();

    /// <summary>Firework shows still to come today.</summary>
    public IReadOnlyList<TimeSpan> FireworksToCome { get; private init; } = Array.Empty<TimeSpan>();

    public IReadOnlyList<DayNote> Log { get; private init; } = Array.Empty<DayNote>();

    /// <summary>How many incidents there have been today: people taken ill or hurt, trouble, lost children, crushes.</summary>
    public int Incidents { get; private init; }

    /// <summary>Incidents still going on.</summary>
    public int OpenIncidents { get; private init; }

    public int TakenIll { get; private init; }

    public int HurtInCrush { get; private init; }

    public int HurtByFireworks { get; private init; }

    public int Hurt => HurtInCrush + HurtByFireworks + HurtInFights;

    public int Treated { get; private init; }

    public int Untreated { get; private init; }

    public int TroubleSorted { get; private init; }

    public int Fights { get; private init; }

    public int ThrownOut { get; private init; }

    public int HurtInFights { get; private init; }

    public int PassedOut { get; private init; }

    /// <summary>How many times someone has been sick on the ground, and how many of those puddles are still there.</summary>
    public int TimesSick { get; private init; }

    public int SickWaiting { get; private init; }

    /// <summary>Grown-ups on site who are merry, tipsy and drunk just now.</summary>
    public (int Merry, int Tipsy, int Drunk) Drinkers { get; private init; }

    public int DrinksRefused { get; private init; }

    public int BedsUsed { get; private init; }

    public int Beds { get; private init; }

    public IReadOnlyList<MoneyLine> In { get; private init; } = Array.Empty<MoneyLine>();

    public IReadOnlyList<MoneyLine> Out { get; private init; } = Array.Empty<MoneyLine>();

    public int TotalIn => In.Sum(l => l.Amount);

    public int TotalOut => Out.Sum(l => l.Amount);

    public int UpSoFar => TotalIn - TotalOut;

    public int Bank { get; private init; }

    /// <summary>Short notes on whatever needs the player now, worst first. Empty on a good day.</summary>
    public IReadOnlyList<string> WorthALook { get; private init; } = Array.Empty<string>();

    public static DayReport Of(FestivalGame game)
    {
        ArgumentNullException.ThrowIfNull(game);
        var now = game.Now;
        var crowd = game.Crowd;
        var log = game.DayLog;
        var mood = game.MoodLine;
        var happy = crowd.Happiness;
        var earlier = mood.LastOrDefault(p => p.At <= now.AddHours(-2));
        var beds = game.Site.Stalls.Where(s => FestivalGame.HasBeds(s.Kind)).Sum(s => Catalogue.Of(s.Kind).Servers);
        return new DayReport
        {
            Now = now,
            OnSite = crowd.OnSite,
            Sold = game.SoldAhead ? game.TicketsSold : Math.Max(crowd.Expected, crowd.ThroughGates),
            Happy = happy,
            HappyChange = earlier.At == default ? 0 : happy - earlier.Happy,
            Fed = crowd.Content(Need.Food),
            Watered = crowd.Content(Need.Drink),
            Toilets = crowd.Content(Need.Toilet),
            WentHomeUnhappy = crowd.WentHomeUnhappy,
            GaveUpQueuing = crowd.GaveUpQueuing,
            MoodLine = mood,
            Stages = StagesOf(game),
            FireworksToCome = game.FireworkShows.Where(s => !game.HasGoneOff(s)).Select(s => s.Time).OrderBy(t => t).ToList(),
            Log = log,
            Incidents = log.Count(n => n.IsIncident),
            OpenIncidents = log.Count(n => n.IsIncident && n.Tone == DayTone.Open),
            TakenIll = log.Count(n => n.Kind == DayNoteKind.Ill),
            HurtInCrush = game.CrushInjuries,
            HurtByFireworks = game.FireworkInjuries,
            Treated = game.Treated,
            Untreated = game.Untreated,
            TroubleSorted = game.TroubleSorted,
            Fights = game.Fights,
            ThrownOut = game.ThrownOut,
            HurtInFights = game.FightInjuries,
            PassedOut = game.PassedOut,
            TimesSick = game.TimesSick,
            SickWaiting = game.Sick.Count,
            Drinkers = game.Drinkers,
            DrinksRefused = game.DrinksRefused,
            Beds = beds,
            BedsUsed = Math.Max(0, beds - game.FreeBeds),
            In = game.Income.GroupBy(l => l.What).Select(g => new MoneyLine(g.Key, g.Sum(l => l.Amount))).Where(l => l.Amount != 0).OrderByDescending(l => l.Amount).ToList(),
            Out = game.Spending.GroupBy(l => SpendingGroup(l.What)).Select(g => new MoneyLine(g.Key, g.Sum(l => l.Amount))).Where(l => l.Amount != 0).OrderByDescending(l => l.Amount).ToList(),
            Bank = game.Money,
            WorthALook = Worries(game, beds),
        };
    }

    /// <summary>Folds the many spending lines into a few: acts, the field, staff, adverts, and the kit.</summary>
    public static string SpendingGroup(string what) => what switch
    {
        "Act fees" => "Acts",
        "Field rent and licence" or "Camping field" or "Field repairs" or "Scouts' litter pick" => "Field",
        "Staff wages" or "Security overtime" => "Staff",
        "Advertising" => "Adverts",
        "Shuttle buses" => "Buses",
        _ => "Kit and building",
    };

    private static List<StageDay> StagesOf(FestivalGame game)
    {
        var now = game.Now;
        var shows = game.Shows.Where(s => s.Stage is not null && s.Booked && s.Slots.Count > 0).ToList();
        var lines = new List<StageDay>();
        for (var i = 0; i < shows.Count; i++)
        {
            var show = shows[i];
            var next = show.Next(now);
            var playing = show.Playing(now);
            var sets = show.Slots.Select(s => new SetLine(s.Start, s.End, s.Act.Name, s.Act.Genre, s.Act.Rating, s.Headliner,
                s.CutShort || (show.CalledOff is { } off && s.End > off && s.Start < off) ? SetState.Cancelled
                : show.CalledOff is { } off2 && s.Start >= off2 ? SetState.Cancelled
                : playing?.Index == s.Index ? SetState.OnNow
                : s.End <= now ? SetState.Played
                : next?.Index == s.Index ? SetState.Next
                : SetState.Later)).ToList();
            var watching = playing is null ? 0 : game.Crowd.Parties.Where(p => p.IsOnSite && p.State == PartyState.Watching && p.Watch == playing.Index).Sum(p => p.Members.Count);
            lines.Add(new StageDay(FestivalGame.StageName(i, shows.Count) is "the stage" ? "The stage" : FestivalGame.StageName(i, shows.Count), Catalogue.Of(show.Stage!.Kind).Name, watching, sets));
        }

        return lines;
    }

    private static List<string> Worries(FestivalGame game, int beds)
    {
        var worries = new List<string>();
        if (game.Phase != Phase.Day)
        {
            return worries;
        }

        if (game.SeriousIncident)
        {
            worries.Add("The day has been called off after people were hurt in crushes");
            return worries;
        }

        var crowd = game.Crowd;
        if (crowd.GatesShut && game.Now.TimeOfDay >= TimeSpan.FromHours(9))
        {
            worries.Add("The gates are still shut: people are waiting outside");
        }

        var status = game.CrowdStatus;
        if (status != Crowding.Comfortable && crowd.OnSite > 0)
        {
            var (x, y) = game.Density.WorstAt;
            var where = game.Whereabouts(x, y);
            worries.Add(status == Crowding.CrushRisk
                ? $"Crush risk {where}".TrimEnd()
                : $"Getting packed {where}".TrimEnd());
        }

        var waiting = game.DayLog.Count(n => n.IsIncident && n.Kind != DayNoteKind.Crush && n.Tone == DayTone.Open && n.Outcome == "Waiting for help");
        if (waiting > 0)
        {
            worries.Add(waiting == 1 ? "Someone is waiting for help: nobody free to go" : $"{waiting} people are waiting for help: nobody free to go");
        }

        if (game.Sick.Count > 0 && !game.Staff.Any(s => s.Kind == StaffKind.Cleaner))
        {
            worries.Add(game.Sick.Count == 1 ? "Someone has been sick and there is no cleaner to mop it up" : $"{game.Sick.Count} lots of sick on the ground and no cleaner to mop it up");
        }
        else if (game.Sick.Count >= 4)
        {
            worries.Add($"{game.Sick.Count} lots of sick on the ground: the cleaners cannot keep up");
        }

        if (game.Drinkers.Drunk >= Math.Max(8, crowd.OnSite / 25))
        {
            worries.Add($"{game.Drinkers.Drunk} people are drunk: a bar can be told not to serve them");
        }

        if (beds > 0 && game.FreeBeds <= 0)
        {
            worries.Add("Every first aid bed is taken");
        }

        if (crowd.OnSite > 0)
        {
            foreach (var (need, words) in new[] { (Need.Toilet, "Toilets"), (Need.Drink, "Drinks"), (Need.Food, "Food") })
            {
                var content = crowd.Content(need);
                if (content < Worrying)
                {
                    worries.Add($"{words}: only {content}% of people are getting what they need");
                }
            }
        }

        if (game.MuddyPatches > 0)
        {
            worries.Add(game.MuddyPatches == 1 ? "A patch of the field is slippery with mud" : $"{game.MuddyPatches} patches of the field are slippery with mud");
        }

        return worries;
    }
}
