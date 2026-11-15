using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation;

/// <summary>
/// Firework shows: the player plans up to four 30-second shows at times of their choosing, lets them
/// off from a launch pad, and fences off the safe zone round it. A good show in the dark cheers
/// everyone up; a show with no fence upsets people and can hurt someone; a show with something in
/// its safe zone is called off.
/// </summary>
public sealed partial class FestivalGame
{
    private readonly List<FireworkShow> _shows = new();
    private readonly List<FireworkResult> _fireworkResults = new();

    /// <summary>The shows planned for today, in time order.</summary>
    public IReadOnlyList<FireworkShow> FireworkShows => _shows;

    /// <summary>What happened at each show that has gone off (or been called off) today.</summary>
    public IReadOnlyList<FireworkResult> FireworkResults => _fireworkResults;

    /// <summary>How many people fireworks have hurt today.</summary>
    public int FireworkInjuries => _fireworkResults.Sum(r => r.Hurt);

    /// <summary>A show starts: which one, and how it went. The view lets the fireworks off.</summary>
    public event Action<FireworkShow, FireworkResult>? FireworksStarted;

    /// <summary>True if a show's time has come and gone, so it can no longer be changed.</summary>
    public bool HasGoneOff(FireworkShow show) => _fireworkResults.Any(r => r.Minute == show.Minute);

    /// <summary>True while fireworks can be planned: the same times things can be built.</summary>
    public bool CanPlanFireworks => CanBuild;

    /// <summary>Whether a launch pad could go with its corner on a tile, counting the money.</summary>
    public BuildCheck CheckLaunchPad(LaunchPadKind kind, int x, int y) => Afford(Site.CheckLaunchPad(kind, x, y));

    /// <summary>Puts the launch pad down and pays its hire.</summary>
    public BuildCheck BuildLaunchPad(LaunchPadKind kind, int x, int y)
    {
        var check = CheckLaunchPad(kind, x, y);
        if (check.Ok)
        {
            Spend(Fireworks.Line, check.Cost);
            Site.AddLaunchPad(kind, x, y);
        }

        return check;
    }

    /// <summary>Puts the Heras fence round the safe zone, or takes it down for the money back.</summary>
    public BuildCheck FenceSafeZone(bool fenced)
    {
        if (!CanBuild)
        {
            return BuildCheck.No("Nothing can be built just now.");
        }

        if (Site.LaunchPad is not { } pad)
        {
            return BuildCheck.No("Put down a launch pad first.");
        }

        if (!fenced)
        {
            if (!pad.Fenced)
            {
                return BuildCheck.No("The safe zone has no fence.");
            }

            Site.SetPadFence(false);
            Spend(Fireworks.Line, -pad.FenceCost);
            return new BuildCheck(true, -pad.FenceCost, null);
        }

        var check = Afford(Site.CheckPadFence());
        if (check.Ok)
        {
            Spend(Fireworks.Line, check.Cost);
            Site.SetPadFence(true);
        }

        return check;
    }

    /// <summary>The time the next free slot for a show would be, or null if no more fit.</summary>
    public int? NextShowMinute()
    {
        if (_shows.Count >= Fireworks.MostShows)
        {
            return null;
        }

        for (var m = 21 * 60 + 30; m <= Fireworks.LatestMinute; m += Fireworks.MinuteStep)
        {
            if (CanShowAt(m, -1))
            {
                return m;
            }
        }

        for (var m = Fireworks.EarliestMinute; m <= Fireworks.LatestMinute; m += Fireworks.MinuteStep)
        {
            if (CanShowAt(m, -1))
            {
                return m;
            }
        }

        return null;
    }

    private bool CanShowAt(int minute, int except) =>
        minute is >= Fireworks.EarliestMinute and <= Fireworks.LatestMinute && minute % Fireworks.MinuteStep == 0
        && !_shows.Where((s, i) => i != except).Any(s => Math.Abs(s.Minute - minute) < Fireworks.MinuteStep)
        && (Phase != Phase.Day || Day + TimeSpan.FromMinutes(minute) > Now);

    /// <summary>Adds an empty show at a time. Returns its place in the list, or -1 if it cannot go then.</summary>
    public int AddFireworkShow(int minute)
    {
        if (!CanPlanFireworks || _shows.Count >= Fireworks.MostShows || !CanShowAt(minute, -1))
        {
            return -1;
        }

        var show = new FireworkShow(minute, new List<FireworkCue>());
        _shows.Add(show);
        _shows.Sort((a, b) => a.Minute.CompareTo(b.Minute));
        return _shows.IndexOf(show);
    }

    /// <summary>Moves a show to another time. Returns its new place in the list, or -1 if it cannot move there.</summary>
    public int MoveFireworkShow(int index, int minute)
    {
        if (!Editable(index) || !CanShowAt(minute, index))
        {
            return -1;
        }

        var show = _shows[index] with { Minute = minute };
        _shows[index] = show;
        _shows.Sort((a, b) => a.Minute.CompareTo(b.Minute));
        return _shows.IndexOf(show);
    }

    /// <summary>Cancels a show that has not gone off, and gives the money for its fireworks back.</summary>
    public bool RemoveFireworkShow(int index)
    {
        if (!Editable(index))
        {
            return false;
        }

        Spend(Fireworks.Line, -_shows[index].Cost);
        _shows.RemoveAt(index);
        return true;
    }

    private bool Editable(int index) => CanPlanFireworks && index >= 0 && index < _shows.Count && !HasGoneOff(_shows[index]);

    /// <summary>Buys a firework and puts it in a show, a number of seconds in.</summary>
    public BuildCheck AddFirework(int show, FireworkKind kind, double at)
    {
        if (!Editable(show))
        {
            return BuildCheck.No("That show can't be changed now.");
        }

        var cost = Fireworks.Of(kind).Cost;
        if (cost > Money)
        {
            return new BuildCheck(false, cost, $"That costs £{cost:N0} and there is £{Money:N0} in the bank.");
        }

        Spend(Fireworks.Line, cost);
        _shows[show].Cues.Add(new FireworkCue(kind, Fireworks.Clamp(kind, at)));
        return new BuildCheck(true, cost, null);
    }

    /// <summary>Moves a firework to go off at another second.</summary>
    public bool MoveFirework(int show, int cue, double at)
    {
        if (!Editable(show) || cue < 0 || cue >= _shows[show].Cues.Count)
        {
            return false;
        }

        var c = _shows[show].Cues[cue];
        _shows[show].Cues[cue] = c with { At = Fireworks.Clamp(c.Kind, at) };
        return true;
    }

    /// <summary>Takes a firework out of a show and gives its money back.</summary>
    public bool RemoveFirework(int show, int cue)
    {
        if (!Editable(show) || cue < 0 || cue >= _shows[show].Cues.Count)
        {
            return false;
        }

        Spend(Fireworks.Line, -Fireworks.Of(_shows[show].Cues[cue].Kind).Cost);
        _shows[show].Cues.RemoveAt(cue);
        return true;
    }

    /// <summary>Swaps a show's fireworks for the example show for the pad that is down, paying the difference.</summary>
    public BuildCheck UseExampleShow(int show)
    {
        if (!Editable(show))
        {
            return BuildCheck.No("That show can't be changed now.");
        }

        var cues = Site.LaunchPad?.Kind == LaunchPadKind.Big ? Fireworks.Example() : Fireworks.SmallExample();
        var extra = cues.Sum(c => Fireworks.Of(c.Kind).Cost) - _shows[show].Cost;
        if (extra > Money)
        {
            return new BuildCheck(false, extra, $"That costs £{extra:N0} more and there is £{Money:N0} in the bank.");
        }

        Spend(Fireworks.Line, extra);
        _shows[show].Cues.Clear();
        _shows[show].Cues.AddRange(cues);
        return new BuildCheck(true, extra, null);
    }

    /// <summary>What the crowd will think of a show, let off from the pad that is down (a small one if there is none).</summary>
    public ShowRating RateFireworks(int show) => Fireworks.Rate(_shows[show].Cues, Site.LaunchPad?.Kind == LaunchPadKind.Big);

    /// <summary>
    /// What would happen if a show went off right now, in words: called off, held up with no fence,
    /// or safe. Null if it would go off safely.
    /// </summary>
    public (FireworkOutcome Outcome, string Why) SafetyCheck(FireworkShow show)
    {
        if (Site.LaunchPad is not { } pad)
        {
            return (FireworkOutcome.CalledOff, "There is no launch pad. Put one down from the Fireworks button on the build bar.");
        }

        if (show.Cues.Count == 0)
        {
            return (FireworkOutcome.CalledOff, "There are no fireworks in the show.");
        }

        if (Site.WhatIsIn(pad.Zone) is { } inTheWay)
        {
            return (FireworkOutcome.CalledOff, $"The safe zone has {inTheWay} in it, so the fire officer won't let it go off.");
        }

        return pad.Fenced
            ? (FireworkOutcome.Safe, "Fenced off and clear.")
            : (FireworkOutcome.NoFence, "The safe zone has no fence: people will wander in, the show will be held up, and someone could get hurt.");
    }

    /// <summary>Once a second on the day: lets off any show whose time has come.</summary>
    private void TickFireworks(DateTime now)
    {
        if (Phase != Phase.Day || _shows.Count == 0)
        {
            return;
        }

        foreach (var show in _shows)
        {
            if (!HasGoneOff(show) && now >= Day + show.Time)
            {
                LetOff(show, now);
            }
        }
    }

    private void LetOff(FireworkShow show, DateTime now)
    {
        var (outcome, why) = SafetyCheck(show);
        var bigPad = Site.LaunchPad?.Kind == LaunchPadKind.Big;
        var rating = Fireworks.Rate(show.Cues, bigPad);
        var hurt = 0;
        switch (outcome)
        {
            case FireworkOutcome.CalledOff:
                Crowd.Upset(0, 0, 1e6, Fireworks.CalledOffMood);
                break;
            case FireworkOutcome.NoFence:
                Crowd.Upset(0, 0, 1e6, Fireworks.NoFenceMood);
                var dice = Core.GameRandom.FromSeed(Seed, 7000 + show.Minute);
                if (dice.NextDouble() < Fireworks.HurtChance(bigPad) && HurtByFireworks(now))
                {
                    hurt = 1;
                    why = "The safe zone had no fence: people wandered in, the show was held up, and someone was burned.";
                }

                break;
            default:
                // Fireworks are seen from everywhere, but hardly at all while it is still light.
                var lift = rating.Mood * (1 - Light);
                foreach (var party in Crowd.Parties.Where(p => p.IsOnSite && p.State != PartyState.Unwell))
                {
                    party.Mood = Math.Min(1, party.Mood + lift);
                }

                if (Light > 0.25)
                {
                    why = "It was still light, so people could hardly see them.";
                }

                break;
        }

        var result = new FireworkResult(show.Minute, outcome, outcome == FireworkOutcome.CalledOff ? 0 : rating.Stars, hurt, why);
        _fireworkResults.Add(result);
        FireworksStarted?.Invoke(show, result);
    }

    /// <summary>Someone near the launch pad is burned. They need first aid like anyone else hurt. False if nobody is near.</summary>
    private bool HurtByFireworks(DateTime now)
    {
        if (Site.LaunchPad is not { } pad)
        {
            return false;
        }

        var (cx, cy) = FieldMap.CentreOf(new TilePoint(pad.X + (pad.Rect.Width / 2), pad.Y + (pad.Rect.Height / 2)));
        var near = Crowd.Parties
            .Where(p => p.State is PartyState.Watching or PartyState.Standing or PartyState.Strolling or PartyState.Leaving
                && ((p.X - cx) * (p.X - cx)) + ((p.Y - cy) * (p.Y - cy)) <= HurtReach * HurtReach)
            .OrderBy(p => ((p.X - cx) * (p.X - cx)) + ((p.Y - cy) * (p.Y - cy)))
            .FirstOrDefault();
        if (near is null)
        {
            return false;
        }

        Crowd.TakeIll(near);
        _incidents.Add(new Incident(_nextIncident++, IncidentKind.Ill, near.X, near.Y, near.Id, now));
        Crowd.Upset(near.X, near.Y, 8, 0.04);
        return true;
    }

    /// <summary>How far from the middle of the pad, in metres, someone can be burned at a show with no fence.</summary>
    public const double HurtReach = 40;

    private FireworksSave? SaveFireworks() => _shows.Count == 0 && _fireworkResults.Count == 0 ? null
        : new FireworksSave(_shows.Select(s => s with { Cues = s.Cues.ToList() }).ToList(), _fireworkResults.ToList());

    private void RestoreFireworks(FireworksSave? save)
    {
        _shows.Clear();
        _fireworkResults.Clear();
        if (save is null)
        {
            return;
        }

        _shows.AddRange((save.Shows ?? new()).Select(s => s with { Cues = s.Cues ?? new() }));
        _shows.Sort((a, b) => a.Minute.CompareTo(b.Minute));
        _fireworkResults.AddRange(save.Results ?? new());
    }
}

/// <summary>The day's firework shows as written to a save file.</summary>
public sealed record FireworksSave(List<FireworkShow>? Shows, List<FireworkResult>? Results);
