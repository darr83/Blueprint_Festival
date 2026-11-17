using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation;

/// <summary>
/// The dev panel's tools: changing the money, the acts, the clock, the crowd, the weather, and
/// starting trouble, all while the festival is running, to try things out. None of it happens in
/// a normal game. Each returns false (or 0) where it could not be done just now.
/// </summary>
public sealed partial class FestivalGame
{
    /// <summary>The most the dev panel lets the bank hold, so it never runs over.</summary>
    public const int DevMostMoney = 100_000_000;

    /// <summary>Adds money to the bank (or takes it away, never below nothing). Kept out of the accounts.</summary>
    public void DevAddMoney(int amount) => Money = (int)Math.Clamp((long)Money + amount, 0, DevMostMoney);

    /// <summary>Empties the bank, to try running out of money.</summary>
    public void DevEmptyBank() => Money = 0;

    /// <summary>The set on a stage the dev panel acts on: the one playing, or else the next to come.</summary>
    public Slot? DevSetOn(int stage)
    {
        if (stage < 0 || stage >= Shows.Count)
        {
            return null;
        }

        var show = Shows[stage];
        return show.Stage is null ? null : show.Playing(Now) ?? show.Next(Now);
    }

    /// <summary>
    /// Puts an artist on a stage in place of the act playing there now, or of the next act if the
    /// stage is between sets. They keep the set's times. False with nothing booked on that stage.
    /// </summary>
    public bool DevSwapAct(int stage, Artist artist)
    {
        ArgumentNullException.ThrowIfNull(artist);
        if (DevSetOn(stage) is not { } slot)
        {
            return false;
        }

        return Shows[stage].Swap(slot.Index, ActGenerator.Of(artist, slot.Headliner));
    }

    /// <summary>Ends the set playing on a stage now. False if nothing is playing there.</summary>
    public bool DevEndSet(int stage)
    {
        if (Phase != Phase.Day || stage < 0 || stage >= Shows.Count || Shows[stage].Playing(Now) is not { } slot)
        {
            return false;
        }

        return Shows[stage].CutShort(slot.Index, Now);
    }

    /// <summary>
    /// Hurries the day on to a time of day, the way skipping to the next act does: every second
    /// still happens, as fast as the computer can. False if that time has passed or it is not the day.
    /// </summary>
    public bool DevJumpTo(TimeSpan timeOfDay)
    {
        var to = Day + timeOfDay;
        if (Phase != Phase.Day || to <= Now)
        {
            return false;
        }

        SkippingTo = to;
        _skipFromCrush = CrushBuilding;
        return true;
    }

    /// <summary>The time of day of the next firework show still to go off today, or null.</summary>
    public TimeSpan? NextFireworksAt => FireworkShows.Where(s => !HasGoneOff(s) && Day + s.Time > Now).Select(s => (TimeSpan?)s.Time).FirstOrDefault();

    /// <summary>Adds people to the crowd, arriving over the next few minutes. Returns how many.</summary>
    public int DevAddPeople(int people) => Phase == Phase.Day ? Crowd.AddPeople(people, Now) : 0;

    /// <summary>Brings the crowd up to the festival's licence. Returns how many more are coming.</summary>
    public int DevFillToLicence() => DevAddPeople(Licence - Crowd.Expected);

    /// <summary>Sends everyone home, and nobody else comes today. Returns how many were here.</summary>
    public int DevSendAllHome() => Phase == Phase.Day ? Crowd.SendAllHome(Now) : 0;

    /// <summary>
    /// Sets the weather from now on: showers and rain start this hour, fine and hot weather is dry
    /// for the rest of the day. The ground stays as wet as it is.
    /// </summary>
    public void DevSetWeather(WeatherKind kind)
    {
        var rain = new double[24];
        var hour = Phase == Phase.Day ? Now.Hour : 12;
        // The hours already gone keep the rain they had.
        for (var h = 0; h < hour && h < 24; h++)
        {
            rain[h] = Weather.Rain.Length == 24 ? Weather.Rain[h] : 0;
        }

        switch (kind)
        {
            case WeatherKind.Showers:
                rain[hour] = 0.6;
                if (hour + 3 < 24)
                {
                    rain[hour + 3] = 0.5;
                }

                break;
            case WeatherKind.Rain:
                for (var h = hour; h < Math.Min(24, hour + 5); h++)
                {
                    rain[h] = 0.85;
                }

                break;
        }

        var high = kind switch { WeatherKind.Hot => 29, WeatherKind.Rain => 14, WeatherKind.Showers => 17, _ => 21 };
        SetWeather(new DayWeather(kind, high, rain));
    }

    /// <summary>Sets how wet the ground is, from 0 (dry) to 1 (sodden).</summary>
    public void DevSetWetness(double wetness) => Wetness = Math.Clamp(wetness, 0, 1);

    /// <summary>Packs the crowd into a crush round a spot, in metres. Returns how many people were packed in.</summary>
    public int DevCrushAt(double x, double y) =>
        Phase == Phase.Day ? Crowd.PackInto(x, y, DevCrushPeople, Now + TimeSpan.FromMinutes(DevCrushMinutes)) : 0;

    /// <summary>How many people the dev panel packs into a crush, at most.</summary>
    public const int DevCrushPeople = 220;

    /// <summary>How many game minutes a crush made on the dev panel holds before people can move again.</summary>
    public const int DevCrushMinutes = 12;

    /// <summary>Someone near a spot is hurt and needs first aid. False if nobody is about.</summary>
    public bool DevHurtAt(double x, double y)
    {
        var nearest = Crowd.Parties.Where(Crowd.OutAndAbout).OrderBy(p => Distance(p.X, p.Y, x, y)).FirstOrDefault();
        if (Phase != Phase.Day || nearest is null)
        {
            return false;
        }

        Crowd.TakeIll(nearest);
        Raise(new Incident(_nextIncident++, IncidentKind.Ill, nearest.X, nearest.Y, nearest.Id, Now), DayNoteKind.Hurt, "Someone hurt");
        return true;
    }

    /// <summary>Drops litter all round a spot, in metres. Returns how many pieces.</summary>
    public int DevLitterAt(double x, double y, int pieces = 40)
    {
        var dropped = 0;
        for (var i = 0; i < pieces; i++)
        {
            // Scattered on a spiral out to about eight metres.
            var r = 8 * Math.Sqrt((i + 0.5) / pieces);
            var angle = i * 2.39996;
            var (px, py) = (x + (r * Math.Cos(angle)), y + (r * Math.Sin(angle)));
            if (Field.IsWalkable(FieldMap.TileAt(px, py)))
            {
                _litter.Add(new LitterPiece(Math.Round(px, 2), Math.Round(py, 2)));
                dropped++;
            }
        }

        return dropped;
    }

    /// <summary>
    /// Tidies up the trouble the dev panel can make: lets a packed crowd go, clears the crush
    /// warning, and takes away all the litter and sick. People hurt still wait for first aid, and
    /// fights still need security. Returns true if there was anything to clear.
    /// </summary>
    public bool DevClearTrouble()
    {
        var any = Crowd.ReleaseHeld(Now) > 0 || _redMinutes.Count > 0 || _litter.Count > 0 || _sick.Count > 0;
        _redMinutes.Clear();
        _litter.Clear();
        _sick.Clear();
        return any;
    }

    /// <summary>While true, no firework show goes off. Set from the dev panel; not saved.</summary>
    public bool FireworksHeld { get; set; }

    /// <summary>
    /// Lets off an example show from the launch pad now, as if one had been planned for this
    /// minute. Free. False with no launch pad, or when it is not the day.
    /// </summary>
    public bool DevLaunchFireworks()
    {
        if (Phase != Phase.Day || Site.LaunchPad is null)
        {
            return false;
        }

        var cues = Site.LaunchPad.Kind == LaunchPadKind.Big ? Fireworks.Example() : Fireworks.SmallExample();
        LetOff(new FireworkShow((int)Now.TimeOfDay.TotalMinutes, cues), Now);
        return true;
    }
}
