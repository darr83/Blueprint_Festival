using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation;

/// <summary>What the camping field is doing once the music has stopped.</summary>
public enum CampTime
{
    /// <summary>The festival day itself, or no camping.</summary>
    None,

    /// <summary>After the music: some campers sit up round fires, most sleep, and things go wrong in the dark.</summary>
    Night,

    /// <summary>The morning after: campers queue for showers and toilets, buy breakfast, pack up and go.</summary>
    Morning,
}

/// <summary>One thing that happened in the camping field overnight or in the morning, and how it ended.</summary>
public sealed record CampLogLine(DateTime At, string Text, string Outcome, DayTone Tone);

/// <summary>A fire burning in the camping field, in metres: in a fire pit, or one campers lit by their tents.</summary>
public sealed record CampFire(double X, double Y, bool InPit);

/// <summary>The camping field's night and morning as written to a save file.</summary>
public sealed record CampLifeSave(CampTime Time, double Quiet, double Safe, double Warm, List<CampLogLine> Log, DateTime? QuietUntil, bool SecuritySent,
    List<int> Packed, List<int> Left, int Campers, int Breakfasts, int BreakfastTakings, int ShowerQueue, int ToiletQueue);

/// <summary>
/// Camp life: the camping field after the music and the morning after. Campers go back to their
/// tents instead of vanishing. Some sit up round fires and party, most sleep. In the dark, fires
/// are left burning, tents are broken into and loud parties keep people awake, unless camp stewards
/// and security are walking the tents. A good night lifts how campers feel. In the morning they
/// queue for the showers and toilets, buy breakfast, pack up and go, and a few leave their tent
/// behind for the tidy-up bill. The night and morning run sixty times faster than the day.
/// </summary>
public sealed partial class FestivalGame
{
    /// <summary>Game seconds that pass in one tick of the night and the morning: a minute, so they run sixty times faster than the day.</summary>
    public const int CampSecondsATick = 60;

    /// <summary>How often, in game minutes, something can happen in the camping field.</summary>
    public const int CampStepMinutes = 10;

    /// <summary>When campers wake (7am the next day), counted from the start of the festival day.</summary>
    public static readonly TimeSpan MorningStarts = TimeSpan.FromHours(31);

    /// <summary>When the last camper has gone (noon the next day), counted from the start of the festival day.</summary>
    public static readonly TimeSpan CampersGone = TimeSpan.FromHours(36);

    /// <summary>How near, in metres, a camp steward (or security) has to be to put out a fire or quieten a party.</summary>
    public const double StewardReach = 35;

    /// <summary>How near, in metres, security has to be to stop a thief.</summary>
    public const double NightSecurityReach = 45;

    /// <summary>How many campers sit round one fire pit.</summary>
    public const int SeatsAFirePit = 12;

    /// <summary>What it costs to clear each tent left behind.</summary>
    public const int TentLeftCost = 9;

    /// <summary>How far a loud party carries, in metres.</summary>
    public const double PartyCarries = 25;

    private readonly List<CampLogLine> _campLog = new();
    private readonly HashSet<int> _packedUp = new();
    private readonly HashSet<int> _leftTents = new();
    private readonly Dictionary<int, (double X, double Y)> _sentTo = new();

    /// <summary>What the camping field is doing now.</summary>
    public CampTime CampTime { get; private set; }

    /// <summary>How quiet the camp is sleeping, from 0 to 1.</summary>
    public double CampQuiet { get; private set; } = 1;

    /// <summary>How safe the camp is, from 0 to 1.</summary>
    public double CampSafe { get; private set; } = 1;

    /// <summary>How warm and dry the camp is sleeping, from 0 to 1. Set by the weather.</summary>
    public double CampWarm { get; private set; } = 1;

    /// <summary>Everything that has happened in the camping field tonight and this morning, oldest first.</summary>
    public IReadOnlyList<CampLogLine> CampLog => _campLog;

    /// <summary>Until when the camp has been asked for quiet.</summary>
    public DateTime? QuietUntil { get; private set; }

    /// <summary>True once every member of security has been sent to walk the camping field tonight.</summary>
    public bool SecuritySentToCamp { get; private set; }

    /// <summary>How many people were in their tents when the night began.</summary>
    public int NightCampers { get; private set; }

    /// <summary>How many breakfasts campers have bought this morning, and for how much.</summary>
    public int BreakfastsSold { get; private set; }

    public int BreakfastTakings { get; private set; }

    /// <summary>How long, in minutes, campers wait for a shower and for a toilet this morning.</summary>
    public int ShowerQueueMinutes { get; private set; }

    public int ToiletQueueMinutes { get; private set; }

    /// <summary>Tents left behind, still standing, to be cleared before the field goes back.</summary>
    public int TentsLeft => _leftTents.Count;

    /// <summary>How many campers have packed up and gone home this morning.</summary>
    public int CampersGoneHome => Crowd.Parties.Where(p => _packedUp.Contains(p.Id) || _leftTents.Contains(p.Id)).Sum(p => p.Members.Count);

    /// <summary>How many campers are sitting up round fires just now.</summary>
    public int CampersUp => CampTime == CampTime.Night ? (int)Math.Round(NightCampers * UpShare(Clock.Now)) : 0;

    /// <summary>How many campers are asleep in their tents just now.</summary>
    public int CampersAsleep => CampTime switch
    {
        CampTime.Night => NightCampers - CampersUp,
        CampTime.Morning => Crowd.Parties.Where(p => InTent(p) && Clock.Now < WakesAt(p)).Sum(p => p.Members.Count),
        _ => 0,
    };

    /// <summary>The fires burning in the camping field now: the fire pits first, then any campers lit by their tents.</summary>
    public IReadOnlyList<CampFire> CampFires => CampTime == CampTime.Night ? FiresFor(CampersUp) : Array.Empty<CampFire>();

    /// <summary>True if a tile is in a camping field.</summary>
    public bool InCamp(TilePoint tile) => Field.CampFields.Any(r => r.Contains(tile));

    /// <summary>The share of campers still up at a time of night: about a third as the music stops, nobody by half past three.</summary>
    private double UpShare(DateTime now)
    {
        var hours = (now - (Day + TimeSpan.FromHours(23))).TotalHours;
        var share = Math.Clamp(0.42 - (0.12 * hours), 0, 1);
        if (Weather.RainAt(now) > 0.1)
        {
            share *= 0.5;
        }

        if (QuietUntil is { } quiet && now < quiet)
        {
            share *= 0.7;
        }

        return share;
    }

    private bool InTent(Party p) => p.Camper && p.Ticketed == true && !_packedUp.Contains(p.Id) && !_leftTents.Contains(p.Id);

    /// <summary>The fires for this many campers sitting up: a fire pit for every twelve, then one by the tents for every ten more.</summary>
    private List<CampFire> FiresFor(int up)
    {
        var fires = new List<CampFire>();
        if (up <= 0)
        {
            return fires;
        }

        var pits = Site.Stalls.Where(s => s.Kind == StallKind.FirePit).ToList();
        var inPits = Math.Min(pits.Count, (int)Math.Ceiling(up / (double)SeatsAFirePit));
        foreach (var pit in pits.Take(inPits))
        {
            var (x, y) = MiddleOf(pit.Rect);
            fires.Add(new CampFire(x, y, true));
        }

        var rest = Math.Max(0, up - (pits.Count * SeatsAFirePit));
        var tents = Crowd.Pitched.ToList();
        for (var i = 0; i < (int)Math.Ceiling(rest / 10.0) && tents.Count > 0; i++)
        {
            var (pitch, _) = tents[(int)((uint)((i + 1) * 2654435761u) % tents.Count)];
            var (x, y) = FieldMap.CentreOf(pitch);
            fires.Add(new CampFire(x + 2.2, y + 1.2, false));
        }

        return fires;
    }

    private static (double X, double Y) MiddleOf(TileRect r) =>
        ((r.X + (r.Width / 2.0)) * FieldMap.TileMetres, (r.Y + (r.Height / 2.0)) * FieldMap.TileMetres);

    /// <summary>A roll from 0 to 1 for something in the camp, the same every time for the same moment and the same question.</summary>
    private double CampRoll(DateTime now, int salt)
    {
        var minutes = (uint)(now - Day).TotalMinutes;
        var h = (minutes * 2654435761u) ^ ((uint)salt * 2246822519u) ^ ((uint)Seed * 3266489917u);
        h ^= h >> 15;
        h *= 668265263u;
        h ^= h >> 13;
        return (h % 100000) / 100000.0;
    }

    /// <summary>The music is over and campers are in their tents: the night begins.</summary>
    private void StartCampNight(DateTime now)
    {
        CampTime = CampTime.Night;
        NightCampers = Crowd.InTents;
        CampQuiet = CampSafe = 1;
        CampWarm = WarmTonight(now);
        Note(DayNoteKind.Camp, "The camp settles for the night", $"{NightCampers:N0} campers in their tents", DayTone.Plain);
        LogCamp(now, $"{NightCampers:N0} campers back at their tents", "", DayTone.Plain);
    }

    /// <summary>How warm and dry a night it is: a cool night or rain on the tents makes it worse.</summary>
    private double WarmTonight(DateTime now)
    {
        var warm = Weather.High >= 22 ? 1.0 : Weather.High >= 17 ? 0.85 : Weather.High >= 13 ? 0.65 : 0.45;
        return Math.Clamp(warm - (0.5 * Weather.RainAt(now)), 0, 1);
    }

    private void LogCamp(DateTime at, string text, string outcome, DayTone tone)
    {
        _campLog.Add(new CampLogLine(at, text, outcome, tone));
        if (_campLog.Count > 60)
        {
            _campLog.RemoveAt(0);
        }
    }

    /// <summary>Everyone on watch in the camping field tonight: security and camp stewards posted there, and any security sent.</summary>
    private IEnumerable<StaffMember> OnWatch(bool securityOnly) => _staff.Where(s =>
        (s.Kind == StaffKind.Security || (!securityOnly && s.Kind == StaffKind.CampSteward))
        && (InCamp(s.Tile) || (s.Kind == StaffKind.Security && SecuritySentToCamp)));

    private bool Watched(double x, double y, double reach, bool securityOnly) =>
        OnWatch(securityOnly).Any(s => Distance(s.X, s.Y, x, y) <= reach);

    private void Upset(double x, double y, double reach, double by)
    {
        foreach (var party in Crowd.Parties)
        {
            if (InTent(party) && party.Pitch is { } pitch)
            {
                var (px, py) = FieldMap.CentreOf(pitch);
                if (Distance(px, py, x, y) <= reach)
                {
                    party.Mood = Math.Clamp(party.Mood - by, 0, 1);
                }
            }
        }
    }

    private int TentsNear(double x, double y, double reach) => Crowd.Parties.Count(p =>
        InTent(p) && p.Pitch is { } pitch && Distance(FieldMap.CentreOf(pitch).X, FieldMap.CentreOf(pitch).Y, x, y) <= reach);

    /// <summary>One tick of the night or the morning. Things happen every ten game minutes.</summary>
    private void TickCampLife(DateTime now)
    {
        WalkTheCamp(now);
        if ((int)(now - Day).TotalMinutes % CampStepMinutes == 0)
        {
            if (CampTime == CampTime.Night)
            {
                NightStep(now);
            }
            else
            {
                MorningStep(now);
            }
        }

        if (CampTime == CampTime.Night && now >= Day + MorningStarts)
        {
            EndCampNight(now);
        }
        else if (CampTime == CampTime.Morning && (now >= Day + CampersGone || !Crowd.Parties.Any(InTent)))
        {
            EndCampMorning(now);
        }
    }

    /// <summary>Security and camp stewards walk slow loops round their spot in the camp.</summary>
    private void WalkTheCamp(DateTime now)
    {
        var minutes = (now - Day).TotalMinutes;
        foreach (var s in OnWatch(false))
        {
            var (cx, cy) = _sentTo.TryGetValue(s.Id, out var spot) ? spot : (s.PostX, s.PostY);
            var angle = (minutes * 0.04) + (s.Id * 1.7);
            const double loop = 9;
            s.X = cx + (loop * Math.Cos(angle));
            s.Y = cy + (loop * Math.Sin(angle));
            s.Heading = (Math.Atan2(Math.Cos(angle), -Math.Sin(angle)) * 180 / Math.PI) + 90;
        }
    }

    private void NightStep(DateTime now)
    {
        var up = CampersUp;
        var before = (int)Math.Round(NightCampers * UpShare(now.AddMinutes(-CampStepMinutes)));
        var bedtime = up < before;
        var quiet = QuietUntil is { } q && now < q;
        CampWarm = WarmTonight(now);

        // Fires left burning when campers go to bed: fire pits now and then, fires by the tents far more often.
        var fires = FiresFor(before);
        for (var i = 0; i < fires.Count; i++)
        {
            var fire = fires[i];
            var chance = (bedtime ? 0.012 : 0.002) * (fire.InPit ? 1 : 3);
            if (CampRoll(now, 100 + i) >= chance)
            {
                continue;
            }

            var where = fire.InPit ? "Fire pit left burning" : "Fire left burning by the tents";
            if (Watched(fire.X, fire.Y, StewardReach, false))
            {
                LogCamp(now, where, "put out by a steward", DayTone.Good);
                continue;
            }

            CampSafe = Math.Max(0, CampSafe - 0.12);
            Upset(fire.X, fire.Y, 20, 0.04);
            if (!fire.InPit && CampRoll(now, 300 + i) < 0.3)
            {
                CampSafe = Math.Max(0, CampSafe - 0.1);
                Upset(fire.X, fire.Y, 4, 0.25);
                LogCamp(now, "A tent caught light from a fire", "nobody watching", DayTone.Bad);
                Note(DayNoteKind.Camp, "A tent caught light", "From a fire left burning by the tents", DayTone.Bad);
            }
            else
            {
                LogCamp(now, where, "no steward", DayTone.Bad);
            }
        }

        // Thieves go round the tents while people sleep.
        var asleep = Crowd.Parties.Where(InTent).ToList();
        if (asleep.Count > 0 && CampRoll(now, 7) < Math.Min(0.3, asleep.Count / 2500.0))
        {
            var victim = asleep[(int)(CampRoll(now, 8) * asleep.Count)];
            var (x, y) = FieldMap.CentreOf(victim.Pitch!.Value);
            if (Watched(x, y, NightSecurityReach, true))
            {
                LogCamp(now, "Someone trying tent zips", "caught by security", DayTone.Good);
            }
            else
            {
                CampSafe = Math.Max(0, CampSafe - 0.1);
                victim.Mood = Math.Clamp(victim.Mood - 0.15, 0, 1);
                var what = CampRoll(now, 9) < 0.5 ? "phone" : CampRoll(now, 9) < 0.8 ? "wallet" : "speaker";
                LogCamp(now, $"Tent broken into, {what} taken", "no security near", DayTone.Bad);
            }
        }

        // A loud party round one of the fires keeps the tents near it awake.
        if (up > 0 && fires.Count > 0 && CampRoll(now, 11) < Math.Min(0.35, up / 700.0) * (quiet ? 0.3 : 1))
        {
            var fire = fires[(int)(CampRoll(now, 12) * fires.Count)];
            var near = TentsNear(fire.X, fire.Y, PartyCarries);
            if (Watched(fire.X, fire.Y, StewardReach, false))
            {
                LogCamp(now, "Loud party round a fire", "quietened by a steward", DayTone.Good);
            }
            else if (near > 0)
            {
                CampQuiet = Math.Max(0, CampQuiet - 0.1);
                Upset(fire.X, fire.Y, PartyCarries, 0.03);
                LogCamp(now, $"Loud party keeping {near:N0} tents awake", "nobody asked them to stop", DayTone.Bad);
            }
        }

        // A quiet stretch lets the camp settle again.
        CampQuiet = Math.Min(1, CampQuiet + 0.015);
        CampSafe = Math.Min(1, CampSafe + 0.01);
    }

    /// <summary>Asks the whole camp to keep it down for an hour: parties are rarer and people go to bed sooner.</summary>
    public void AskForQuiet()
    {
        if (CampTime != CampTime.Night)
        {
            return;
        }

        QuietUntil = Clock.Now.AddHours(1);
        LogCamp(Clock.Now, "Stewards asked the camp for quiet", "for the next hour", DayTone.Plain);
    }

    /// <summary>Sends every member of security to walk the camping field for the rest of the night.</summary>
    public void SendSecurityToCamp()
    {
        if (CampTime != CampTime.Night || SecuritySentToCamp)
        {
            return;
        }

        SecuritySentToCamp = true;
        var camps = Field.CampFields.ToList();
        var guards = _staff.Where(s => s.Kind == StaffKind.Security && !InCamp(s.Tile)).ToList();
        var tents = Crowd.Pitched.ToList();
        for (var i = 0; i < guards.Count && (tents.Count > 0 || camps.Count > 0); i++)
        {
            // Spread out across the tents, so they cover as much of the camp as they can.
            (double, double) spot = tents.Count > 0
                ? FieldMap.CentreOf(tents[(int)((long)i * tents.Count / guards.Count)].Pitch)
                : MiddleOf(camps[i % camps.Count]);
            _sentTo[guards[i].Id] = spot;
        }

        LogCamp(Clock.Now, guards.Count > 0 ? $"{guards.Count:N0} security sent to walk the camp" : "No security to send", "", guards.Count > 0 ? DayTone.Plain : DayTone.Bad);
    }

    /// <summary>Morning comes: a good night cheers the campers up, and a bad one lets them down.</summary>
    private void EndCampNight(DateTime now)
    {
        var night = (CampQuiet + CampSafe + CampWarm) / 3;
        foreach (var party in Crowd.Parties.Where(InTent))
        {
            party.Mood = Math.Clamp(party.Mood + ((night - 0.7) * 0.15), 0, 1);
        }

        var (word, tone) = night >= 0.8 ? ("A good night", DayTone.Good) : night >= 0.6 ? ("A fair night", DayTone.Plain) : ("A bad night", DayTone.Bad);
        Note(DayNoteKind.Camp, $"{word} in the camp", $"Quiet {night * 100:0}%: {_campLog.Count(l => l.Tone == DayTone.Bad):N0} things went wrong, {_campLog.Count(l => l.Tone == DayTone.Good):N0} sorted", tone);
        LogCamp(now, $"{word} in the camp. Campers wake up", "", tone);
        _campNight = night;

        if (MoreDaysToCome)
        {
            // Another day of music to come: the campers stay, and the morning is for getting ready.
            CampTime = CampTime.None;
            _sentTo.Clear();
            BetweenDays(now);
            return;
        }

        CampTime = CampTime.Morning;
        foreach (var s in _staff)
        {
            (s.X, s.Y, s.Heading) = (s.PostX, s.PostY, s.PostHeading);
        }

        _sentTo.Clear();
        MorningQueues();
    }

    /// <summary>When a party wakes: between seven and half past nine, earlier on a wet morning.</summary>
    private DateTime WakesAt(Party p) => Day + MorningStarts + TimeSpan.FromMinutes((uint)(p.Id * 2654435761u) % (Weather.Wet ? 120 : 150));

    /// <summary>When a party has packed up and gone: after the toilets, a shower for some, breakfast and packing.</summary>
    private DateTime LeavesAt(Party p)
    {
        var showers = (uint)(p.Id * 40503u) % 100 < 40 ? ShowerQueueMinutes + 8 : 0;
        return WakesAt(p) + TimeSpan.FromMinutes(35 + ((uint)(p.Id * 2246822519u) % 40) + showers + (ToiletQueueMinutes / 2));
    }

    /// <summary>Works out this morning's shower and toilet queues from how many campers there are and what is in the camp.</summary>
    private void MorningQueues()
    {
        var campers = Crowd.Parties.Where(InTent).Sum(p => p.Members.Count);
        var inCamp = Site.Stalls.Where(s => InCamp(new TilePoint(s.X, s.Y))).ToList();
        // Shower and toilet visits spread over the two and a half hours campers are getting up.
        var showersAnHour = inCamp.Where(s => s.Kind == StallKind.ShowerBlock).Sum(s => Catalogue.Of(s.Kind).Servers) * 3600.0 / Catalogue.Of(StallKind.ShowerBlock).SecondsPerPerson;
        var showering = campers * 0.4 / 2.5;
        ShowerQueueMinutes = showersAnHour <= 0 ? 0 : QueueMinutes(showering, showersAnHour);
        var looKinds = new[] { StallKind.Toilets, StallKind.ToiletBlock };
        var loosAnHour = inCamp.Where(s => looKinds.Contains(s.Kind)).Sum(s => Catalogue.Of(s.Kind).Servers * 3600.0 / Catalogue.Of(s.Kind).SecondsPerPerson);
        var loos = campers * 0.9 / 2.5;
        ToiletQueueMinutes = loosAnHour <= 0 ? 0 : QueueMinutes(loos, loosAnHour);
        if (showersAnHour <= 0)
        {
            LogCamp(Clock.Now, "No showers in the camp", "campers go home unwashed", DayTone.Bad);
        }

        if (loosAnHour <= 0)
        {
            LogCamp(Clock.Now, "No toilets in the camp", "campers walk to the arena", DayTone.Bad);
        }

        if (BreakfastPrice() is null)
        {
            LogCamp(Clock.Now, "No breakfast in the camp", "put up a camp shop or a food stall", DayTone.Bad);
        }
    }

    /// <summary>How long a queue gets, in minutes, when this many people an hour want what this many an hour can have.</summary>
    private static int QueueMinutes(double wanting, double served) =>
        wanting <= served ? 2 : (int)Math.Min(120, Math.Round(2 + (75 * ((wanting / served) - 1))));

    /// <summary>Where breakfast is sold in the camp, and for how much: the camp shop's breakfast pack, or any food stall in a camping field.</summary>
    private int? BreakfastPrice()
    {
        var inCamp = Site.Stalls.Where(s => InCamp(new TilePoint(s.X, s.Y))).ToList();
        if (inCamp.FirstOrDefault(s => s.Kind == StallKind.CampShop) is { } shop)
        {
            var menu = Catalogue.MenuOf(StallKind.CampShop);
            var item = Enumerable.Range(0, menu.Count).FirstOrDefault(i => menu[i].Name.StartsWith("Breakfast", StringComparison.Ordinal));
            return shop.PriceOf(item);
        }

        return inCamp.FirstOrDefault(s => Catalogue.Of(s.Kind).Serves == BlueprintFestival.Simulation.Site.Need.Food) is { } food ? food.PriceOf(0) : null;
    }

    private void MorningStep(DateTime now)
    {
        var breakfast = BreakfastPrice();
        var wanting = new Dictionary<int, bool>();
        foreach (var party in Crowd.Parties.Where(InTent).ToList())
        {
            if (now < LeavesAt(party))
            {
                continue;
            }

            var showered = (uint)(party.Id * 40503u) % 100 < 40;
            var feel = 0.0;
            if (showered && (ShowerQueueMinutes == 0 || ShowerQueueMinutes > 20))
            {
                feel -= 0.03;
            }

            if (Crowd.KindOf(party) == CampKind.Glamping && (ShowerQueueMinutes == 0 || ShowerQueueMinutes > 10))
            {
                // Glamping guests paid for comfort, and a queue for the showers is not it.
                feel -= GlampersKeptWaiting;
            }

            if (ToiletQueueMinutes == 0 || ToiletQueueMinutes > 10)
            {
                feel -= 0.03;
            }

            if ((uint)(party.Id * 3266489917u) % 100 < 55)
            {
                if (breakfast is { } price)
                {
                    var paid = price * party.Members.Count;
                    BreakfastsSold += party.Members.Count;
                    BreakfastTakings += paid;
                    Earn("Breakfast for campers", paid);
                }
                else
                {
                    feel -= 0.02;
                }
            }

            party.Mood = Math.Clamp(party.Mood + feel, 0, 1);

            // Most take the tent home. A wet morning, a bad night or a bad day makes some give up and leave it.
            var leave = 0.03 + (Weather.Wet ? 0.07 : 0) + (party.Mood < 0.4 ? 0.06 : 0) + (CampSafe < 0.5 ? 0.02 : 0);
            if ((uint)(party.Id * 1103515245u) % 1000 < leave * 1000)
            {
                _leftTents.Add(party.Id);
            }
            else
            {
                _packedUp.Add(party.Id);
                Crowd.StrikeTent(party.Id);
            }
        }

    }

    /// <summary>Everyone has packed up and gone (or given up and left the tent): the tidy-up begins.</summary>
    private void EndCampMorning(DateTime now)
    {
        foreach (var party in Crowd.Parties.Where(InTent).ToList())
        {
            _packedUp.Add(party.Id);
            Crowd.StrikeTent(party.Id);
        }

        Note(DayNoteKind.Camp, "Campers packed up and went home", TentsLeft > 0 ? $"{TentsLeft:N0} tents left behind, £{TentLeftCost} each to clear" : "Every tent taken home", TentsLeft > 0 ? DayTone.Bad : DayTone.Good);
        if (BreakfastsSold > 0)
        {
            Note(DayNoteKind.Camp, "Breakfast in the camp", $"{BreakfastsSold:N0} sold for £{BreakfastTakings:N0}", DayTone.Good);
        }

        CampTime = CampTime.None;
        EndDay();
    }

    /// <summary>Runs the rest of the night straight away, to the morning.</summary>
    public void SkipCampNight() => RunCampWhile(() => CampTime == CampTime.Night);

    /// <summary>Runs the rest of the morning straight away, to the tidy-up.</summary>
    public void SkipCampMorning() => RunCampWhile(() => CampTime == CampTime.Morning);

    private void RunCampWhile(Func<bool> still)
    {
        // A day and a half at most, so nothing can loop for ever.
        for (var i = 0; i < 36 * 60 && Phase == Phase.Day && still(); i++)
        {
            Clock.Jump(TimeSpan.FromMinutes(1));
            TickCampLife(Clock.Now);
        }
    }

    /// <summary>Clears the tents left behind (the scouts do it with the litter). Returns what it cost.</summary>
    private int ClearLeftTents()
    {
        var cost = TentsLeft * TentLeftCost;
        foreach (var id in _leftTents)
        {
            Crowd.StrikeTent(id);
        }

        _leftTents.Clear();
        return cost;
    }

    private void ResetCampLife()
    {
        CampTime = CampTime.None;
        CampQuiet = CampSafe = CampWarm = 1;
        _campLog.Clear();
        _packedUp.Clear();
        _leftTents.Clear();
        _sentTo.Clear();
        QuietUntil = null;
        SecuritySentToCamp = false;
        NightCampers = BreakfastsSold = BreakfastTakings = ShowerQueueMinutes = ToiletQueueMinutes = 0;
    }

    private CampLifeSave? SaveCampLife() => CampTime == CampTime.None && _campLog.Count == 0 && TentsLeft == 0 ? null
        : new CampLifeSave(CampTime, CampQuiet, CampSafe, CampWarm, _campLog.ToList(), QuietUntil, SecuritySentToCamp,
            _packedUp.ToList(), _leftTents.ToList(), NightCampers, BreakfastsSold, BreakfastTakings, ShowerQueueMinutes, ToiletQueueMinutes);

    private void RestoreCampLife(CampLifeSave? save)
    {
        ResetCampLife();
        if (save is null)
        {
            return;
        }

        CampTime = Phase == Phase.Day && Enum.IsDefined(save.Time) ? save.Time : CampTime.None;
        (CampQuiet, CampSafe, CampWarm) = (Math.Clamp(save.Quiet, 0, 1), Math.Clamp(save.Safe, 0, 1), Math.Clamp(save.Warm, 0, 1));
        _campLog.AddRange(save.Log ?? new());
        QuietUntil = save.QuietUntil;
        NightCampers = Math.Max(0, save.Campers);
        (BreakfastsSold, BreakfastTakings, ShowerQueueMinutes, ToiletQueueMinutes) = (save.Breakfasts, save.BreakfastTakings, save.ShowerQueue, save.ToiletQueue);
        foreach (var id in save.Packed ?? new())
        {
            _packedUp.Add(id);
            Crowd.StrikeTent(id);
        }

        foreach (var id in save.Left ?? new())
        {
            _leftTents.Add(id);
        }

        if (save.SecuritySent && CampTime == CampTime.Night)
        {
            SendSecurityToCamp();
            _campLog.RemoveAt(_campLog.Count - 1);
        }
    }
}
